using BSFM.Dados;
using BSFM.Seguranca;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Filtros;

/// <summary>
/// Exige sessão válida <b>e</b> assinatura da requisição em qualquer controller
/// ou action marcada. É o substituto do "confiar no id que veio na URL".
///
/// Ordem de verificação:
///   1. token (quem é o usuário);
///   2. sessão viva no banco (permite revogar antes de expirar);
///   3. assinatura HMAC (esta requisição é dele e não foi alterada).
///
/// A identidade resolvida fica em <c>HttpContext.Items["usuarioId"]</c> —
/// controllers nunca devem aceitar id de usuário vindo da rota ou do corpo.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SessaoObrigatoriaAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext contexto)
    {
        var http = contexto.HttpContext;
        var servicos = http.RequestServices;

        var emissor = servicos.GetRequiredService<EmissorSessao>();
        var verificador = servicos.GetRequiredService<VerificadorAssinatura>();
        var segredo = servicos.GetRequiredService<SegredoMestre>();
        var db = servicos.GetRequiredService<BsfmDbContext>();

        // ---- 1. Token -----------------------------------------------------
        var cabecalho = http.Request.Headers.Authorization.ToString();
        var token = cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? cabecalho["Bearer ".Length..].Trim()
            : null;

        var sessaoToken = emissor.Validar(token);
        if (!sessaoToken.Ok)
        {
            contexto.Result = new UnauthorizedObjectResult(new { erro = sessaoToken.Erro });
            return;
        }

        var claims = sessaoToken.Claims!;

        // ---- 2. Sessão viva ----------------------------------------------
        var sessao = await db.Sessoes
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Jti == claims.Jti, http.RequestAborted);

        if (sessao is null)
        {
            contexto.Result = new UnauthorizedObjectResult(new { erro = "sessao_desconhecida" });
            return;
        }

        if (!sessao.EstaAtiva(DateTime.UtcNow))
        {
            contexto.Result = new UnauthorizedObjectResult(new { erro = "sessao_encerrada" });
            return;
        }

        if (sessao.UsuarioId != claims.UsuarioId)
        {
            contexto.Result = new UnauthorizedObjectResult(new { erro = "sessao_divergente" });
            return;
        }

        // ---- 3. Assinatura da requisição ---------------------------------
        http.Request.EnableBuffering();
        byte[] corpo;
        using (var memoria = new MemoryStream())
        {
            await http.Request.Body.CopyToAsync(memoria, http.RequestAborted);
            corpo = memoria.ToArray();
        }
        http.Request.Body.Position = 0;

        // Chave derivada do jti: cada sessão assina com a sua, e revogar a
        // sessão revoga a chave — sem segredo global circulando no cliente.
        var chaveDaSessao = Base64Url.Decodificar(ChaveSessao.Derivar(segredo.Valor, claims.Jti));

        // O caminho assinado inclui a query string: o cliente assina exatamente o
        // que envia (ex.: /api/eu/analises?limite=60). Validar só Path fazia toda
        // requisição autenticada com filtro falhar como "assinatura_invalida".
        var caminhoAssinado = $"{http.Request.Path.Value}{http.Request.QueryString.Value}";

        var assinatura = verificador.ValidarCom(
            chaveDaSessao,
            http.Request.Method,
            caminhoAssinado,
            http.Request.Headers["X-BSFM-Timestamp"],
            http.Request.Headers["X-BSFM-Nonce"],
            corpo,
            http.Request.Headers["X-BSFM-Signature"]);

        if (!assinatura.Ok)
        {
            contexto.Result = new UnauthorizedObjectResult(new { erro = assinatura.Erro });
            return;
        }

        http.Items["usuarioId"] = claims.UsuarioId;
        http.Items["sessaoId"] = sessao.Id;
        http.Items["jti"] = claims.Jti;
    }
}

/// <summary>Atalhos tipados para o que o filtro publica.</summary>
public static class ContextoUsuario
{
    public static int UsuarioId(this HttpContext http) => (int)http.Items["usuarioId"]!;
    public static Guid SessaoId(this HttpContext http) => (Guid)http.Items["sessaoId"]!;
    public static string Jti(this HttpContext http) => (string)http.Items["jti"]!;
}
