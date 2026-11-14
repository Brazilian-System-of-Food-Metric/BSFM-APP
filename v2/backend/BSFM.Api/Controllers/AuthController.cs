using BSFM.Api.DTOs;
using BSFM.Api.Servicos;
using BSFM.Dados;
using BSFM.Dados.Entidades;
using BSFM.Seguranca;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Controllers;

/// <summary>
/// Autenticação. Substitui as rotas Minimal API onde o servidor devolvia o
/// código de verificação na resposta (`tokenParaJs`) e onde a redefinição de
/// senha não exigia token nenhum.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const int MaximoTentativas = 5;
    private static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

    private readonly BsfmDbContext _db;
    private readonly ServicoSessao _sessoes;
    private readonly ServicoAuditoria _auditoria;
    private readonly IEnviadorCodigo _enviador;
    private readonly ILogger<AuthController> _log;

    public AuthController(
        BsfmDbContext db,
        ServicoSessao sessoes,
        ServicoAuditoria auditoria,
        IEnviadorCodigo enviador,
        ILogger<AuthController> log)
    {
        _db = db;
        _sessoes = sessoes;
        _auditoria = auditoria;
        _enviador = enviador;
        _log = log;
    }

    // -----------------------------------------------------------------------
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginEntrada entrada, CancellationToken ct)
    {
        var ipHash = ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString());
        var email = (entrada.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(entrada.Senha))
            return RespostaCredenciaisInvalidas();

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        // E-mail inexistente e senha errada produzem a MESMA resposta.
        if (usuario is null)
        {
            await _auditoria.RegistrarAsync("login_falha", null, "Usuario", null,
                "{\"motivo\":\"email_desconhecido\"}", ipHash, ct);
            return RespostaCredenciaisInvalidas();
        }

        var agora = DateTime.UtcNow;

        if (usuario.BloqueadoAte is not null && usuario.BloqueadoAte > agora)
        {
            await _auditoria.RegistrarAsync("login_bloqueado", usuario.ID, "Usuario", usuario.ID.ToString(),
                null, ipHash, ct);
            return RespostaCredenciaisInvalidas();
        }

        if (!ServicoSenha.Verificar(entrada.Senha, usuario.SenhaHash))
        {
            usuario.TentativasFalhas++;

            if (usuario.TentativasFalhas >= MaximoTentativas)
            {
                usuario.BloqueadoAte = agora.Add(DuracaoBloqueio);
                usuario.TentativasFalhas = 0;
            }

            usuario.AtualizadoEm = agora;
            await _db.SaveChangesAsync(ct);

            await _auditoria.RegistrarAsync("login_falha", usuario.ID, "Usuario", usuario.ID.ToString(),
                "{\"motivo\":\"senha_invalida\"}", ipHash, ct);

            return RespostaCredenciaisInvalidas();
        }

        if (usuario.TotpHabilitado && !Totp.Verificar(usuario.TotpSegredo ?? string.Empty, entrada.CodigoTotp))
        {
            await _auditoria.RegistrarAsync("login_falha", usuario.ID, "Usuario", usuario.ID.ToString(),
                "{\"motivo\":\"totp_invalido\"}", ipHash, ct);
            return RespostaCredenciaisInvalidas();
        }

        // Sucesso: zera tentativas e, se o hash estiver defasado, reidrata.
        usuario.TentativasFalhas = 0;
        usuario.BloqueadoAte = null;

        if (ServicoSenha.PrecisaRehash(usuario.SenhaHash))
        {
            usuario.SenhaHash = ServicoSenha.Gerar(entrada.Senha);
            usuario.SenhaAtualizadaEm = agora;
        }

        await _db.SaveChangesAsync(ct);

        var emitida = await _sessoes.CriarAsync(usuario, ipHash,
            ServicoSessao.HashIp(Request.Headers.UserAgent.ToString()), ct);

        await _auditoria.RegistrarAsync("login_sucesso", usuario.ID, "Usuario", usuario.ID.ToString(),
            null, ipHash, ct);

        return Ok(new LoginSaida(
            emitida.Token,
            emitida.RefreshToken,
            (int)(emitida.ExpiraEm - DateTime.UtcNow).TotalSeconds,
            emitida.ChaveAssinatura,
            MontarUsuario(usuario)));
    }

    // -----------------------------------------------------------------------
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshEntrada entrada, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entrada.RefreshToken))
            return Unauthorized(new { erro = "refresh_invalido" });

        var ipHash = ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString());
        var emitida = await _sessoes.RotacionarAsync(entrada.RefreshToken, ipHash, ct);

        if (emitida is null)
        {
            await _auditoria.RegistrarAsync("refresh_recusado", null, "RefreshToken", null, null, ipHash, ct);
            return Unauthorized(new { erro = "refresh_invalido" });
        }

        return Ok(new
        {
            token = emitida.Token,
            refreshToken = emitida.RefreshToken,
            expiraEmSegundos = (int)(emitida.ExpiraEm - DateTime.UtcNow).TotalSeconds,
            chaveAssinatura = emitida.ChaveAssinatura,
        });
    }

    // -----------------------------------------------------------------------
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutEntrada entrada, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entrada.RefreshToken))
            return NoContent();

        var refresh = await _sessoes.LocalizarRefreshAsync(entrada.RefreshToken, ct);
        if (refresh is not null)
            await _sessoes.RevogarSessaoAsync(refresh.SessaoId, "logout", ct);

        return NoContent();
    }

    // -----------------------------------------------------------------------
    [HttpPost("esqueci-senha")]
    public async Task<IActionResult> EsqueciSenha([FromBody] EsqueceuSenhaEntrada entrada, CancellationToken ct)
    {
        var ipHash = ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString());
        var email = (entrada.Email ?? string.Empty).Trim().ToLowerInvariant();

        var usuario = string.IsNullOrWhiteSpace(email)
            ? null
            : await _db.Usuarios.SingleOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        if (usuario is not null)
        {
            // O segredo existe para todo usuário (mesmo sem 2FA): é dele que sai
            // o código de 6 dígitos enviado por e-mail.
            if (string.IsNullOrEmpty(usuario.TotpSegredo))
            {
                usuario.TotpSegredo = Totp.GerarSegredo();
                await _db.SaveChangesAsync(ct);
            }

            var codigo = Totp.GerarCodigo(usuario.TotpSegredo);
            await _enviador.EnviarAsync(usuario.Email, codigo, ct);

            await _auditoria.RegistrarAsync("codigo_enviado", usuario.ID, "Usuario", usuario.ID.ToString(),
                null, ipHash, ct);
        }
        else
        {
            await _auditoria.RegistrarAsync("codigo_solicitado_inexistente", null, "Usuario", null,
                null, ipHash, ct);
        }

        // Resposta idêntica exista ou não a conta — e SEM o código.
        return Accepted(new { mensagem = "Se o e-mail existir, enviamos um código de verificação." });
    }

    // -----------------------------------------------------------------------
    [HttpPost("redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha([FromBody] RedefinirSenhaEntrada entrada, CancellationToken ct)
    {
        var ipHash = ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString());
        var email = (entrada.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(entrada.Codigo)
            || string.IsNullOrWhiteSpace(entrada.NovaSenha))
            return BadRequest(new { erro = "dados_incompletos" });

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        if (usuario is null)
        {
            await _auditoria.RegistrarAsync("redefinicao_falha", null, "Usuario", null,
                "{\"motivo\":\"email_desconhecido\"}", ipHash, ct);
            return BadRequest(new { erro = "codigo_invalido" });
        }

        if (!Totp.Verificar(usuario.TotpSegredo ?? string.Empty, entrada.Codigo))
        {
            await _auditoria.RegistrarAsync("redefinicao_falha", usuario.ID, "Usuario", usuario.ID.ToString(),
                "{\"motivo\":\"codigo_invalido\"}", ipHash, ct);
            return BadRequest(new { erro = "codigo_invalido" });
        }

        usuario.SenhaHash = ServicoSenha.Gerar(entrada.NovaSenha);
        usuario.SenhaAtualizadaEm = DateTime.UtcNow;
        usuario.TentativasFalhas = 0;
        usuario.BloqueadoAte = null;
        await _db.SaveChangesAsync(ct);

        // Trocar a senha derruba todas as sessões: inclusive a de quem roubou o token.
        await _sessoes.RevogarTodasDoUsuarioAsync(usuario.ID, "senha_redefinida", ct);

        await _auditoria.RegistrarAsync("senha_redefinida", usuario.ID, "Usuario", usuario.ID.ToString(),
            null, ipHash, ct);

        return Ok(new { mensagem = "Senha atualizada. Faça login novamente." });
    }

    // -----------------------------------------------------------------------
    private IActionResult RespostaCredenciaisInvalidas()
        => Unauthorized(new { erro = "credenciais_invalidas" });

    internal static UsuarioSaida MontarUsuario(Usuario u) => new(
        u.ID, u.Nome, u.Email,
        u.IMC, CalculadoraNutricional.ClassificarImc(u.IMC),
        u.TMB, u.GastoTotal,
        u.Peso, u.Altura, u.PesoMeta,
        u.CalcularIdade(), u.TotpHabilitado);
}
