using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BSFM.Seguranca;

// ============================================================================
// Prova executável da camada de segurança do BSFM.
//
// Repositório em memória de propósito: o objetivo aqui é demonstrar o contrato
// de autenticação e a verificação por hash de cada requisição, não substituir o
// EF Core. Quando o BSFMappClone for migrado, só a implementação de repositório
// muda — os endpoints e as regras permanecem.
// ============================================================================

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:5099");

var app = builder.Build();

// ---------------------------------------------------------------------------
// Configuração — em produção vem de variável de ambiente, nunca do código.
// ---------------------------------------------------------------------------
string Segredo(string nome, string padraoDesenvolvimento) =>
    Environment.GetEnvironmentVariable(nome) ?? padraoDesenvolvimento;

var emissor = new EmissorSessao(Segredo("BSFM_JWT_SECRET",
    "dev-jwt-secret-com-no-minimo-32-bytes-para-hmac!"));

var verificador = new VerificadorAssinatura(Segredo("BSFM_HMAC_SECRET",
    "dev-hmac-secret-com-no-minimo-32-bytes-aqui!!"));

var repositorio = new RepositorioMemoria();

// ---------------------------------------------------------------------------
// Verificação de assinatura por requisição, aplicada a todo /api/eu/*
// ---------------------------------------------------------------------------
app.Use(async (contexto, proximo) =>
{
    if (!contexto.Request.Path.StartsWithSegments("/api/eu"))
    {
        await proximo();
        return;
    }

    // 1. Sessão — quem é o usuário.
    var cabecalho = contexto.Request.Headers.Authorization.ToString();
    var token = cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? cabecalho["Bearer ".Length..].Trim()
        : null;

    var sessao = emissor.Validar(token);
    if (!sessao.Ok)
    {
        await Responder(contexto, 401, new { erro = sessao.Erro });
        return;
    }

    // 2. Assinatura — que esta requisição é dele e não foi alterada.
    contexto.Request.EnableBuffering();
    byte[] corpo;
    using (var memoria = new MemoryStream())
    {
        await contexto.Request.Body.CopyToAsync(memoria);
        corpo = memoria.ToArray();
    }
    contexto.Request.Body.Position = 0;

    var resultado = verificador.Validar(
        contexto.Request.Method,
        contexto.Request.Path.Value,
        contexto.Request.Headers["X-BSFM-Timestamp"],
        contexto.Request.Headers["X-BSFM-Nonce"],
        corpo,
        contexto.Request.Headers["X-BSFM-Signature"]);

    if (!resultado.Ok)
    {
        await Responder(contexto, 401, new { erro = resultado.Erro });
        return;
    }

    contexto.Items["usuarioId"] = sessao.Claims!.UsuarioId;
    contexto.Items["jti"] = sessao.Claims!.Jti;

    await proximo();
});

// ---------------------------------------------------------------------------
// Autenticação
// ---------------------------------------------------------------------------
app.MapPost("/api/auth/login", (LoginReq req) =>
{
    var usuario = repositorio.PorEmail(req.Email);

    // Mesma resposta para e-mail inexistente e senha errada: não enumera contas.
    if (usuario is null || !SenhaHasher.Verificar(req.Senha ?? "", usuario.SenhaHash))
        return Results.Json(new { erro = "credenciais_invalidas" }, statusCode: 401);

    if (usuario.TotpHabilitado)
    {
        if (!Totp.Verificar(usuario.SegredoTotp, req.CodigoTotp))
            return Results.Json(new { erro = "codigo_totp_invalido" }, statusCode: 401);
    }

    var token = emissor.Emitir(usuario.Id);
    var refresh = repositorio.EmitirRefresh(usuario.Id);

    return Results.Ok(new
    {
        token,
        refreshToken = refresh,
        expiraEmSegundos = 7200,
        usuario = new
        {
            id = usuario.Id,
            nome = usuario.Nome,
            email = usuario.Email,
            imc = usuario.Imc,
            tmb = usuario.Tmb,
            pesoMeta = usuario.PesoMeta,
        },
    });
});

app.MapPost("/api/auth/refresh", (RefreshReq req) =>
{
    var novoRefresh = repositorio.RotacionarRefresh(req.RefreshToken);
    if (novoRefresh is null)
        return Results.Json(new { erro = "refresh_invalido" }, statusCode: 401);

    var usuarioId = repositorio.DonoDoRefresh(novoRefresh);
    if (usuarioId is null)
        return Results.Json(new { erro = "refresh_invalido" }, statusCode: 401);

    return Results.Ok(new
    {
        token = emissor.Emitir(usuarioId.Value),
        refreshToken = novoRefresh,
        expiraEmSegundos = 7200,
    });
});

app.MapPost("/api/auth/logout", (LogoutReq req) =>
{
    repositorio.RevogarRefresh(req.RefreshToken);
    return Results.NoContent();
});

app.MapPost("/api/auth/esqueci-senha", (EsqueceuReq req) =>
{
    var usuario = repositorio.PorEmail(req.Email);

    // Resposta idêntica exista ou não a conta. O código vai só para o "e-mail".
    if (usuario is not null)
    {
        var codigo = Totp.GerarCodigo(usuario.SegredoTotp);
        Console.WriteLine($"[EMAIL] codigo para {usuario.Email}: {codigo}");
    }

    return Results.Json(
        new { mensagem = "Se o e-mail existir, enviamos um código de verificação." },
        statusCode: 202);
});

app.MapPost("/api/auth/redefinir-senha", (RedefinirReq req) =>
{
    var usuario = repositorio.PorEmail(req.Email);
    if (usuario is null)
        return Results.Json(new { erro = "codigo_invalido" }, statusCode: 400);

    if (!Totp.Verificar(usuario.SegredoTotp, req.Codigo))
        return Results.Json(new { erro = "codigo_invalido" }, statusCode: 400);

    repositorio.TrocarSenha(usuario.Id, req.NovaSenha ?? "");
    repositorio.RevogarTodosRefreshes(usuario.Id);

    return Results.Ok(new { mensagem = "Senha atualizada. Faça login novamente." });
});

// ---------------------------------------------------------------------------
// Área autenticada — a identidade vem do token, nunca da URL
// ---------------------------------------------------------------------------
app.MapGet("/api/eu/perfil", (HttpContext contexto) =>
{
    var usuario = repositorio.PorId((int)contexto.Items["usuarioId"]!);
    if (usuario is null)
        return Results.Json(new { erro = "usuario_inexistente" }, statusCode: 404);

    return Results.Ok(new
    {
        id = usuario.Id,
        nome = usuario.Nome,
        email = usuario.Email,
        imc = usuario.Imc,
        tmb = usuario.Tmb,
        gastoTotal = usuario.GastoTotal,
        pesoMeta = usuario.PesoMeta,
        jti = contexto.Items["jti"],
    });
});

app.MapGet("/saude", () => Results.Ok(new { status = "ok", servico = "bsfm-seguranca" }));

app.Run();

static Task Responder(HttpContext contexto, int status, object corpo)
{
    contexto.Response.StatusCode = status;
    return contexto.Response.WriteAsJsonAsync(corpo);
}

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------
record LoginReq(string? Email, string? Senha, string? CodigoTotp);
record RefreshReq(string? RefreshToken);
record LogoutReq(string? RefreshToken);
record EsqueceuReq(string? Email);
record RedefinirReq(string? Email, string? Codigo, string? NovaSenha);

// ---------------------------------------------------------------------------
// Repositório em memória (substituível por EF Core sem mudar o contrato)
// ---------------------------------------------------------------------------
sealed class RepositorioMemoria
{
    private readonly ConcurrentDictionary<int, UsuarioMemoria> _usuarios = new();
    private readonly ConcurrentDictionary<string, (int UsuarioId, DateTimeOffset Expira)> _refreshes = new();
    private int _proximoId;

    public RepositorioMemoria()
    {
        // Ana: login só com senha (2FA desligado).
        Criar("Ana Souza", "ana@bsfm.dev", "SenhaForte#2026", Totp.GerarSegredo(), totpObrigatorio: false);
        // Bia: login exige segundo fator.
        Criar("Bia Lima", "bia@bsfm.dev", "SenhaForte#2026", Totp.GerarSegredo(), totpObrigatorio: true);
    }

    private void Criar(string nome, string email, string senha, string segredoTotp, bool totpObrigatorio)
    {
        var id = Interlocked.Increment(ref _proximoId);
        _usuarios[id] = new UsuarioMemoria(
            id, nome, email, SenhaHasher.Gerar(senha), segredoTotp, totpObrigatorio,
            Imc: 24.1, Tmb: 1410.5, GastoTotal: 1692.6, PesoMeta: 68.0);

        Console.WriteLine($"[SEED] {email} / {senha} / totp={(totpObrigatorio ? "obrigatorio" : "opcional")} / segredo={segredoTotp}");
    }

    public UsuarioMemoria? PorEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var alvo = email.Trim().ToLowerInvariant();
        return _usuarios.Values.FirstOrDefault(u => u.Email == alvo);
    }

    public UsuarioMemoria? PorId(int id) => _usuarios.GetValueOrDefault(id);

    public string EmitirRefresh(int usuarioId)
    {
        var token = Base64Url.Codificar(RandomNumberGenerator.GetBytes(32));
        _refreshes[token] = (usuarioId, DateTimeOffset.UtcNow.AddDays(30));
        return token;
    }

    public int? DonoDoRefresh(string? token)
        => token is not null && _refreshes.TryGetValue(token, out var v) && v.Expira > DateTimeOffset.UtcNow
            ? v.UsuarioId
            : null;

    /// <summary>Rotaciona: o token antigo é removido e um novo é emitido.</summary>
    public string? RotacionarRefresh(string? token)
    {
        if (token is null) return null;
        if (!_refreshes.TryRemove(token, out var registro)) return null;
        if (registro.Expira <= DateTimeOffset.UtcNow) return null;
        return EmitirRefresh(registro.UsuarioId);
    }

    public void RevogarRefresh(string? token)
    {
        if (token is not null) _refreshes.TryRemove(token, out _);
    }

    public void RevogarTodosRefreshes(int usuarioId)
    {
        foreach (var par in _refreshes)
            if (par.Value.UsuarioId == usuarioId)
                _refreshes.TryRemove(par.Key, out _);
    }

    public void TrocarSenha(int usuarioId, string novaSenha)
    {
        if (_usuarios.TryGetValue(usuarioId, out var atual))
            _usuarios[usuarioId] = atual with { SenhaHash = SenhaHasher.Gerar(novaSenha) };
    }
}

sealed record UsuarioMemoria(
    int Id,
    string Nome,
    string Email,
    string SenhaHash,
    string SegredoTotp,
    bool TotpHabilitado,
    double Imc,
    double Tmb,
    double GastoTotal,
    double PesoMeta);
