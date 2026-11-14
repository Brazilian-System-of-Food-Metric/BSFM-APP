using BSFM.Nutricao;
using BSFM.Nutricao.Visao;
using BSFM.Api.Middleware;
using System.Text.Json.Serialization;
using BSFM.Api.Servicos;
using BSFM.Dados;
using BSFM.Dados.Entidades;
using BSFM.Seguranca;
using Microsoft.EntityFrameworkCore;

// Sem ASPNETCORE_ENVIRONMENT explícito, o padrão do ASP.NET é Production — o que
// ativaria a trava de segredo em desenvolvimento. Aqui o padrão é Development;
// produção só acontece quando alguém declara isso (o Render declara).
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);

var porta = Environment.GetEnvironmentVariable("PORT") ?? "5100";
// 0.0.0.0 é obrigatório em container/Render: 127.0.0.1 não é alcançável de fora.
builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");

// ---------------------------------------------------------------------------
// Segredos: vêm do ambiente. O padrão existe só para desenvolvimento local.
// ---------------------------------------------------------------------------
string Segredo(string nome, string padrao) => Environment.GetEnvironmentVariable(nome) ?? padrao;

var segredoJwt = Segredo("BSFM_JWT_SECRET", "dev-jwt-secret-com-no-minimo-32-bytes-para-hmac!");
var segredoHmac = Segredo("BSFM_HMAC_SECRET", "dev-hmac-secret-com-no-minimo-32-bytes-aqui!!");

// Trava de produção: os padrões acima são públicos (estão no código). Subir em
// produção com eles permitiria forjar token e assinatura. Falhar aqui é melhor
// que servir tráfego com segredo conhecido.
if (builder.Environment.IsProduction())
{
    var faltando = new List<string>();
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BSFM_JWT_SECRET"))) faltando.Add("BSFM_JWT_SECRET");
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BSFM_HMAC_SECRET"))) faltando.Add("BSFM_HMAC_SECRET");

    if (faltando.Count > 0)
    {
        throw new InvalidOperationException(
            $"Produção sem segredo configurado: {string.Join(", ", faltando)}. " +
            "Defina as variáveis no serviço (no Render, use generateValue: true) e reinicie.");
    }
}

builder.Services.AddSingleton(new EmissorSessao(segredoJwt));
builder.Services.AddSingleton(new SegredoMestre(segredoHmac));
builder.Services.AddSingleton(new VerificadorAssinatura(segredoHmac));

// ---------------------------------------------------------------------------
// Banco: Postgres em produção (DATABASE_URL), SQLite em desenvolvimento.
// ---------------------------------------------------------------------------
builder.Services.AddScoped(sp =>
{
    // A fábrica centraliza a leitura de DATABASE_URL/BSFM_BD_PROVIDER.
    return FabricaContexto.Criar();
});

builder.Services.AddScoped<ServicoSessao>();
builder.Services.AddScoped<ServicoAuditoria>();
builder.Services.AddSingleton<IEnviadorCodigo, EnviadorCodigoConsole>();

// ---------------------------------------------------------------------------
// Reconhecimento de alimento por imagem.
//
// Sem chave configurada, o provedor é o "indisponível": a rota responde 503 com
// motivo, em vez de fingir que analisou. A busca por nome não depende disto.
// ---------------------------------------------------------------------------
var chaveGemini = Environment.GetEnvironmentVariable("BSFM_GEMINI_API_KEY");
var modeloGemini = Environment.GetEnvironmentVariable("BSFM_GEMINI_MODEL");

// Base TACO versionada: carregada uma vez, com fallback para a semente embutida.
builder.Services.AddSingleton<ICatalogoNutricional>(_ =>
{
    var caminho = Environment.GetEnvironmentVariable("BSFM_CATALOGO")
        ?? Path.Combine(AppContext.BaseDirectory, "dados", "taco.csv");
    return new CatalogoNutricional(caminho);
});

builder.Services.AddHttpClient<VisaoGemini>(cliente =>
{
    cliente.Timeout = TimeSpan.FromSeconds(25);
});

builder.Services.AddScoped<IVisaoAlimento>(sp =>
{
    if (string.IsNullOrWhiteSpace(chaveGemini))
        return new VisaoIndisponivel("chave_api_ausente");

    return new VisaoGemini(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(VisaoGemini)),
        chaveGemini, modeloGemini);
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// CORS explícito: apenas as origens declaradas. Substitui o AllowAnyOrigin.
var origens = (Environment.GetEnvironmentVariable("BSFM_ORIGENS_PERMITIDAS")
        ?? "http://localhost:5173,http://127.0.0.1:5173,http://127.0.0.1:5100")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddPolicy("Site", p => p
    .WithOrigins(origens)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// Erros padronizados antes de tudo que pode falhar.
app.UseTratamentoErros();
app.UseCors("Site");

// Front React compilado é copiado para wwwroot no build da imagem.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/saude", () => Results.Ok(new { status = "ok", servico = "bsfm-api", versao = "s2" }));

// ---------------------------------------------------------------------------
// Preparo do banco e seed de desenvolvimento
// ---------------------------------------------------------------------------
using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<BsfmDbContext>();
    var urlBanco = Environment.GetEnvironmentVariable("DATABASE_URL");
    var provedor = Environment.GetEnvironmentVariable("BSFM_BD_PROVIDER");

    var ehSqlite = string.IsNullOrWhiteSpace(urlBanco)
        || string.Equals(provedor, "sqlite", StringComparison.OrdinalIgnoreCase);

    if (ehSqlite)
    {
        // Desenvolvimento: SQLite sem servidor. As migrations são a autoridade
        // em Postgres; aqui basta o modelo.
        db.Database.EnsureCreated();
    }
    else
    {
        // Produção: migrations versionadas, nunca EnsureCreated.
        //
        // Uma falha aqui não derruba o serviço: o Render precisa do healthcheck
        // respondendo para a instância subir. O motivo fica no log, com a
        // remediação — o caso comum é e-mail duplicado bloqueando o índice único.
        try
        {
            db.Database.Migrate();
            app.Logger.LogInformation("Migrations aplicadas com sucesso.");
        }
        catch (Exception excecao)
        {
            app.Logger.LogError(excecao,
                "FALHA AO APLICAR MIGRATIONS. Causa comum: e-mails duplicados em Usuarios " +
                "bloqueando IX_Usuarios_Email_Unico. Rode o SQL de deduplicação do " +
                "DOC-03 (seção 1.6) e reinicie o serviço.");
        }
    }

    // Seed só em desenvolvimento: em produção os dados são reais.
    if (ehSqlite)
        Seed.Desenvolvimento(db);
}

app.Run();

/// <summary>Dados de desenvolvimento. Nunca executa em produção.</summary>
internal static class Seed
{
    public static void Desenvolvimento(BsfmDbContext db)
    {
        if (db.Usuarios.Any())
            return;

        var ana = new Usuario
        {
            Nome = "Ana Souza",
            Email = "ana@bsfm.dev",
            SenhaHash = SenhaHasher.Gerar("SenhaForte#2026"),
            Peso = 68,
            Altura = 1.68,
            PesoMeta = 64,
            Sexo = "Feminino",
            TipoPessoa = "Ativo",
            DataNascimento = new DateTime(1998, 5, 20),
            EmailVerificado = true,
            TotpSegredo = Totp.GerarSegredo(),
            TotpHabilitado = false,
            AceitouTermos = true,
            DataAceite = DateTime.UtcNow,
            VersaoTermos = "1.0",
        };

        var bia = new Usuario
        {
            Nome = "Bia Lima",
            Email = "bia@bsfm.dev",
            SenhaHash = SenhaHasher.Gerar("SenhaForte#2026"),
            Peso = 82,
            Altura = 1.72,
            PesoMeta = 75,
            Sexo = "Feminino",
            TipoPessoa = "Sedentário",
            DataNascimento = new DateTime(1995, 2, 10),
            EmailVerificado = true,
            TotpSegredo = Totp.GerarSegredo(),
            TotpHabilitado = true,
            AceitouTermos = true,
            DataAceite = DateTime.UtcNow,
            VersaoTermos = "1.0",
        };

        // Usuária com hash legado BCrypt: prova que a ponte de compatibilidade
        // funciona e que o hash é promovido para PBKDF2 no primeiro login.
        var carla = new Usuario
        {
            Nome = "Carla Dias",
            Email = "carla@bsfm.dev",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("SenhaForte#2026"),
            Peso = 60,
            Altura = 1.60,
            PesoMeta = 58,
            Sexo = "Feminino",
            TipoPessoa = "Sedentário",
            DataNascimento = new DateTime(2000, 8, 1),
            EmailVerificado = true,
            TotpSegredo = Totp.GerarSegredo(),
            TotpHabilitado = false,
            AceitouTermos = true,
            DataAceite = DateTime.UtcNow,
            VersaoTermos = "1.0",
        };

        foreach (var u in new[] { ana, bia, carla })
        {
            u.IMC = CalculadoraNutricional.CalcularImc(u.Peso, u.Altura);
            u.TMB = CalculadoraNutricional.CalcularTmb(u.Sexo, u.Peso, u.Altura, u.CalcularIdade());
            u.GastoTotal = CalculadoraNutricional.CalcularGastoTotal(u.TipoPessoa, u.TMB);
        }

        db.Usuarios.AddRange(ana, bia, carla);
        db.SaveChanges();

        db.ConsentimentosLGPD.AddRange(
            new ConsentimentoLGPD
            {
                UsuarioId = ana.ID, Finalidade = "dados_saude", VersaoTermo = "1.0",
                Concedido = true, Origem = "seed",
            },
            new ConsentimentoLGPD
            {
                UsuarioId = bia.ID, Finalidade = "dados_saude", VersaoTermo = "1.0",
                Concedido = true, Origem = "seed",
            });
        db.SaveChanges();

        Console.WriteLine($"[SEED] ana@bsfm.dev / SenhaForte#2026 / totp=opcional / segredo={ana.TotpSegredo}");
        Console.WriteLine($"[SEED] bia@bsfm.dev / SenhaForte#2026 / totp=obrigatorio / segredo={bia.TotpSegredo}");
    }
}
