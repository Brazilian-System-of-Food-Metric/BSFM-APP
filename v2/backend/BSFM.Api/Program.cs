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
// Segredos: variável de ambiente > derivação de DATABASE_URL > aleatório.
// Nunca há valor público fixo: isso permitiria forjar token e assinatura.
var segredos = BSFM.Api.Servicos.SegredosDeExecucao.Resolver(
    builder.Environment.IsProduction(),
    Environment.GetEnvironmentVariable(BSFM.Api.Servicos.SegredosDeExecucao.VariavelJwt),
    Environment.GetEnvironmentVariable(BSFM.Api.Servicos.SegredosDeExecucao.VariavelHmac),
    Environment.GetEnvironmentVariable("DATABASE_URL"));

var segredoJwt = segredos.Jwt;
var segredoHmac = segredos.Hmac;

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
// Envio de código: Brevo quando há chave; console em desenvolvimento.
// Sem a chave em produção, o sintoma é "o código não chegou" — por isso o
// enviador de console registra um aviso explícito.
var brevoApiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY");
var emailRemetente = Environment.GetEnvironmentVariable("BSFM_EMAIL_REMETENTE") ?? "brazilian.sfm@gmail.com";
var emailRemetenteNome = Environment.GetEnvironmentVariable("BSFM_EMAIL_NOME") ?? "Portal BSFM";

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IEnviadorCodigo>(sp =>
{
    if (!string.IsNullOrWhiteSpace(brevoApiKey))
    {
        return new EnviadorCodigoBrevo(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(EnviadorCodigoBrevo)),
            brevoApiKey, emailRemetente, emailRemetenteNome,
            sp.GetRequiredService<ILogger<EnviadorCodigoBrevo>>());
    }

    var aviso = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Email");
    aviso.LogWarning(
        "BREVO_API_KEY ausente: códigos de verificação vão apenas para o console. " +
        "Configure a variável no serviço para enviar e-mail de verdade.");

    return new EnviadorCodigoConsole(sp.GetRequiredService<ILogger<EnviadorCodigoConsole>>());
});

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
        // Banco migrado da v1 pode ter Infinity/NaN em IMC/TMB. Sem isto, o
        // serializador lança ArgumentException e a tela recebe 400.
        o.JsonSerializerOptions.Converters.Add(new BSFM.Api.Json.DoubleFinitoConverter());
        o.JsonSerializerOptions.Converters.Add(new BSFM.Api.Json.DoubleNullableFinitoConverter());
    });

// CORS explícito: apenas as origens declaradas. Substitui o AllowAnyOrigin.
var origens = (Environment.GetEnvironmentVariable("BSFM_ORIGENS_PERMITIDAS")
        ?? "http://localhost:5173,http://127.0.0.1:5173,http://localhost:4173,http://127.0.0.1:4173,http://127.0.0.1:5100")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddPolicy("Site", p => p
    .WithOrigins(origens)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// Avisos sobre segredos aparecem no log do serviço, não derrubam o processo.
foreach (var aviso in segredos.Avisos)
    app.Logger.LogWarning("{AvisoSegredo}", aviso);

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
            // Banco já existente (v1): schema aditivo idempotente, sem recriar tabelas.
            // Banco novo: migration normal.
            var bancoExistente = BSFM.Api.Servicos.PreparadorSchema.ExecutarAditivo(db, app.Logger);

            if (!bancoExistente)
            {
                db.Database.Migrate();
                // Migrations antigas nao conhecem as colunas novas: aplica o aditivo.
                BSFM.Api.Servicos.PreparadorSchema.ExecutarAditivo(db, app.Logger);
                app.Logger.LogInformation("Banco novo: migrations aplicadas.");
            }
        }
        catch (Exception excecao)
        {
            app.Logger.LogError(excecao,
                "FALHA AO PREPARAR O SCHEMA. O serviço continua no ar, mas consultas que usam " +
                "colunas novas podem falhar até o schema ser corrigido.");
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
