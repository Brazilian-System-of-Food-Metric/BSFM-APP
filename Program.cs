using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using ClassesBSFM;
using PonteBanco;
using System.Linq;
using BSFM.Services;
using BSFM.CoreAnalytics.Backend.Services;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Mvc;

// Correção para trabalhar com datas no PostgreSQL (comum no Railway/Neon)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Configuração de logging para o Render (console)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Configuração da Porta para o Render (usa a variável PORT fornecida pela plataforma)
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
Console.WriteLine($"[INIT] Iniciando servidor na porta {port}");
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Configurações de Serviços
builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddHostedService<LimpezaAnalisesService>(); 
builder.Services.AddSingleton<BSFM.Services.YoloInferenceService>();
builder.Services.AddHttpClient<BSFM.Services.UsdaNutritionService>();
builder.Services.AddDbContext<PonteDB>();

// ====== NOVO: Registro do NutriBrainService (Groq) para feedback IA nos pratos ======
builder.Services.AddScoped<BSFM.CoreAnalytics.Backend.Services.ContextInjectorService>();
builder.Services.AddHttpClient<BSFM.CoreAnalytics.Backend.Services.NutriBrainService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();

// Habilita CORS ANTES de qualquer rota (ordem importante!)
app.UseCors("PermitirSite");

// Middleware para garantir que OPTIONS (preflight CORS) funcione
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 204;
        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
        return;
    }
    await next();
});

// Tenta conectar ao banco com retry (importante para o Render que pode demorar)
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<PonteDB>();
    Console.WriteLine("[POSTGRES] Tentando conectar ao banco de dados...");
    
    int tentativas = 0;
    int maxTentativas = 3;
    while (tentativas < maxTentativas)
    {
        try
        {
            db.Database.EnsureCreated();
            Console.WriteLine("[POSTGRES] Banco de dados conectado e tabelas criadas com sucesso!");
            
            // ====== MIGRAÇÕES AUTOMÁTICAS PARA TABELAS EXISTENTES ======
            try
            {
                // Migração 1: Adicionar coluna Diabetes na tabela Usuarios (se não existir)
                var cmd1 = db.Database.GetDbConnection().CreateCommand();
                cmd1.CommandText = @"
                    DO $$ 
                    BEGIN 
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'Usuarios' AND column_name = 'Diabetes'
                        ) THEN
                            ALTER TABLE ""Usuarios"" ADD COLUMN ""Diabetes"" VARCHAR(50) NOT NULL DEFAULT 'Não informado';
                        END IF;
                    END $$;
                ";
                cmd1.ExecuteNonQuery();
                Console.WriteLine("[MIGRATION] Coluna 'Diabetes' verificada/criada na tabela Usuarios.");

                // Migração 2: Adicionar coluna Intolerancia na tabela Usuarios (se não existir)
                var cmd2 = db.Database.GetDbConnection().CreateCommand();
                cmd2.CommandText = @"
                    DO $$ 
                    BEGIN 
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'Usuarios' AND column_name = 'Intolerancia'
                        ) THEN
                            ALTER TABLE ""Usuarios"" ADD COLUMN ""Intolerancia"" TEXT NOT NULL DEFAULT '';
                        END IF;
                    END $$;
                ";
                cmd2.ExecuteNonQuery();
                Console.WriteLine("[MIGRATION] Coluna 'Intolerancia' verificada/criada na tabela Usuarios.");

                // Migração 3: Adicionar colunas de feedback IA na tabela AnalisesIA (se não existirem)
                var cmd3 = db.Database.GetDbConnection().CreateCommand();
                cmd3.CommandText = @"
                    DO $$ 
                    BEGIN 
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'AnalisesIA' AND column_name = 'PodeConsumir'
                        ) THEN
                            ALTER TABLE ""AnalisesIA"" ADD COLUMN ""PodeConsumir"" BOOLEAN DEFAULT NULL;
                        END IF;
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'AnalisesIA' AND column_name = 'PontuacaoSaude'
                        ) THEN
                            ALTER TABLE ""AnalisesIA"" ADD COLUMN ""PontuacaoSaude"" INTEGER NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'AnalisesIA' AND column_name = 'AnaliseEmRelacaoAMeta'
                        ) THEN
                            ALTER TABLE ""AnalisesIA"" ADD COLUMN ""AnaliseEmRelacaoAMeta"" TEXT NOT NULL DEFAULT '';
                        END IF;
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'AnalisesIA' AND column_name = 'DicaBSFM'
                        ) THEN
                            ALTER TABLE ""AnalisesIA"" ADD COLUMN ""DicaBSFM"" TEXT NOT NULL DEFAULT '';
                        END IF;
                    END $$;
                ";
                cmd3.ExecuteNonQuery();
                Console.WriteLine("[MIGRATION] Colunas de feedback IA verificadas/criadas na tabela AnalisesIA.");
            }
            catch (Exception migEx)
            {
                Console.WriteLine($"[MIGRATION AVISO] Migrações automáticas: {migEx.Message}");
                // Não trava o startup se as migrações falharem
            }

            break;
        }
        catch (Exception ex)
        {
            tentativas++;
            Console.WriteLine($"[POSTGRES] Tentativa {tentativas}/{maxTentativas} falhou: {ex.Message}");
            if (tentativas < maxTentativas)
            {
                Console.WriteLine("[POSTGRES] Aguardando 2 segundos para nova tentativa...");
                Thread.Sleep(2000);
            }
            else
            {
                Console.WriteLine("[POSTGRES] ERRO CRÍTICO: Não foi possível conectar ao banco após todas as tentativas.");
                Console.WriteLine("[POSTGRES] O servidor vai iniciar mesmo assim. As funcionalidades que dependem de banco podem falhar.");
            }
        }
    }
}

// --- COMANDOS PARA O SITE FUNCIONAR ---
app.UseDefaultFiles(); // Faz o sistema procurar pelo index.html ou login.html automaticamente
app.UseStaticFiles();  // Importante: Entrega arquivos dentro da pasta 'wwwroot'

// Rota para a Página Inicial (Fallback caso o DefaultFiles não pegue)
app.MapGet("/", (IWebHostEnvironment env) => 
    Results.File(Path.Combine(env.WebRootPath ?? "wwwroot", "index.html"), "text/html"));

// --- SUAS ROTAS DE API ---

// Rota para gerar APK automaticamente
app.MapGet("/api/gerar-apk", async (HttpContext context, IWebHostEnvironment env) => {
    try {
        var apkUrl = await GerarAPKAutomaticamente(context, env);
        return Results.Redirect(apkUrl);
    } catch (Exception ex) {
        return Results.Problem($"Erro ao gerar APK: {ex.Message}");
    }
});

// Função para gerar APK automaticamente
async Task<string> GerarAPKAutomaticamente(HttpContext context, IWebHostEnvironment env) {
    var siteUrl = $"{context.Request.Scheme}://{context.Request.Host}";
    
    // Usar PWABuilder API para gerar APK diretamente
    var pwabuilderUrl = $"https://www.pwabuilder.com/api/apiappx?url={Uri.EscapeDataString(siteUrl)}&name=BSFM&package=com.bsfm.nutricao&version=1.0.0";
    
    return pwabuilderUrl;
}

app.MapPost("/solicitar-codigo", (SolicitacaoEmail req) => {
    try {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PonteDB>();
        var email = req.Email.Trim().ToLower();
        
        if (db.Usuarios.AsNoTracking().Any(u => u.Email.ToLower() == email))
            return Results.Json(new { mensagem = "E-mail já cadastrado!" }, statusCode: 400);

        // 2. Gera o Token de 6 dígitos
        string token = new Random().Next(100000, 999999).ToString();

        // 3. CHAMA O SERVIÇO DE E-MAIL (Aqui ele envia para o Mailtrap)
        EmailService.EnviarToken(email, token);
        return Results.Ok(new { mensagem = "Código enviado!", tokenParaJs = token });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /solicitar-codigo: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados. Verifique se a variável DATABASE_URL está configurada no Render." }, statusCode: 500);
    }
});

app.MapPost("/cadastrar-usuario-final", (Usuario usuarioVindoDoJs) => {
    try {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PonteDB>();
        // Cuidado: Certifique-se que o pacote BCrypt.Net-Next está no .csproj
        usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
        usuarioVindoDoJs.EmailVerificado = true; 
        new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
        db.Usuarios.Add(usuarioVindoDoJs);
        db.SaveChanges();
        return Results.Ok(new { mensagem = "Perfil Criado!" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /cadastrar-usuario-final: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados." }, statusCode: 500);
    }
});

app.MapPost("/login", (LoginDTO dadosLogin) => {
    try {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PonteDB>();
        var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
        if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash)) {
            return Results.Ok(new { 
                id = user.ID, 
                nome = user.Nome, 
                email = user.Email,
                imc = user.IMC, 
                tmb = user.TMB, 
                gasto = user.GastoTotal,
                peso = user.Peso,
                altura = user.Altura,
                pesoMeta = user.PesoMeta,
                dataNascimento = user.DataNascimento,
                idade = user.CalcularIdade()
            }); 
        }
        return Results.Json(new { mensagem = "E-mail ou senha incorretos." }, statusCode: 400);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /login: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados." }, statusCode: 500);
    }
});

// --- ROTA: ESQUECI MINHA SENHA (PASSO 1) ---
app.MapPost("/esqueci-senha", (EsqueceuSenhaDTO req) => {
    try {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PonteDB>();
        var email = req.Email.Trim().ToLower();

        // 1. Verifica se o usuário existe
        var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == email);
        if (user == null)
            return Results.Json(new { mensagem = "E-mail não encontrado em nossa base." }, statusCode: 404);

        // 2. Gera o Token de 6 dígitos
        string token = new Random().Next(100000, 999999).ToString();

        // 3. CHAMA O SERVIÇO DE E-MAIL (Aqui ele envia para o Mailtrap)
        EmailService.EnviarToken(email, token);

        // 4. Retorna para o JS para que ele possa comparar o token depois
        return Results.Ok(new { mensagem = "Código enviado com sucesso!", tokenParaJs = token });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /esqueci-senha: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados." }, statusCode: 500);
    }
});

// --- ROTA: REDEFINIR SENHA (PASSO 2 - FINAL) ---
app.MapPost("/redefinir-senha", (RedefinicaoSenhaDTO req) => {
    try {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PonteDB>();
        var email = req.Email.Trim().ToLower();

        // 1. Localiza o usuário
        var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == email);
        if (user == null)
            return Results.Json(new { mensagem = "Usuário não identificado." }, statusCode: 404);

        // 2. Criptografa a nova senha e salva
        user.SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.NovaSenha);
        
        db.Usuarios.Update(user);
        db.SaveChanges();

        return Results.Ok(new { mensagem = "Senha atualizada com sucesso!" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /redefinir-senha: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados." }, statusCode: 500);
    }
});
// ============================================================
// ROTA: ANALISAR PRATO (YOLO + USDA + GROQ FEEDBACK IA)
// ============================================================
app.MapPost("/analisar-prato", async (
    [FromForm] IFormFile foto, 
    [FromForm] string porcao, 
    [FromForm] int usuarioId, 
    BSFM.Services.YoloInferenceService yolo, 
    BSFM.Services.UsdaNutritionService nutri, 
    PonteBanco.PonteDB db,
    BSFM.CoreAnalytics.Backend.Services.ContextInjectorService contextInjector,
    BSFM.CoreAnalytics.Backend.Services.NutriBrainService nutriBrain) =>
{
    // Validação de entrada: Evita erros se o usuário enviar sem foto
    if (foto == null || foto.Length == 0) 
        return Results.BadRequest(new { mensagem = "Nenhuma imagem foi recebida pelo servidor." });

    using var ms = new MemoryStream();
    await foto.CopyToAsync(ms);
    var imagemBytes = ms.ToArray();
    
    // 1. Chamar a IA (YOLO)
    var alimentosPt = yolo.DetectarAlimentos(imagemBytes);

    Console.WriteLine($"[IA RESULT] Itens encontrados: {(alimentosPt.Any() ? string.Join(", ", alimentosPt) : "NADA")}");

    if (alimentosPt == null || alimentosPt.Count == 0) 
    {
        Console.WriteLine("[IA AVISO] Nenhum alimento detectado.");
        return Results.Json(new { mensagem = "A Inteligência Artificial não conseguiu ver comida. Tente focar melhor e aproximar do prato." }, statusCode: 404);
    }

    double caloriasTotal = 0, protTotal = 0, carbTotal = 0, gordTotal = 0;
    bool aoMenosUmSucesso = false;

    // 2. Loop para cada alimento detectado
    foreach (var nomePt in alimentosPt)
    {
        string nomeEn = BSFM.Services.YoloInferenceService.Tradutor
                        .FirstOrDefault(x => x.Value.Equals(nomePt, StringComparison.OrdinalIgnoreCase)).Key 
                        ?? nomePt.Replace("'", "").Trim();

        var d = await nutri.BuscarNutrientes(nomeEn);
        
        if (d != null) 
        {
            double mult = porcao.ToLower() switch { "pequeno" => 0.75, "medio" => 1.0, "grande" => 1.8, _ => 1.0 };
            caloriasTotal += (d.Calorias100g * mult);
            protTotal += (d.Proteinas100g * mult);
            carbTotal += (d.Carbos100g * mult);
            gordTotal += (d.Gorduras100g * mult);
            aoMenosUmSucesso = true;
        }
        else {
             Console.WriteLine($"[AVISO USDA] Sem dados para o alimento: {nomeEn}");
        }
    }

    if (!aoMenosUmSucesso)
        return Results.Json(new { mensagem = $"Não conseguimos dados nutricionais para: {string.Join(", ", alimentosPt)}" }, statusCode: 404);

    // 3. PERSISTÊNCIA NO POSTGRESQL
    var analiseFinal = new ClassesBSFM.AnaliseIA {
        UsuarioID = usuarioId,
        Alimento = string.Join(", ", alimentosPt),
        Porcao = porcao,
        Calorias = Math.Round(caloriasTotal, 2),
        Proteinas = Math.Round(protTotal, 2),
        Carbos = Math.Round(carbTotal, 2),
        Gorduras = Math.Round(gordTotal, 2),
        DataAnalise = DateTime.Now
    };

    // 4. FEEDBACK DA IA NUTRICIONAL (Groq Llama 3)
    try
    {
        Console.WriteLine("[NUTRIBRAIN] Gerando feedback IA para o prato analisado...");
        
        // 4.1 Busca contexto do usuário
        var userContext = await contextInjector.BuildContextAsync(usuarioId);
        
        // 4.2 Monta o texto OCR simulado com os macros detectados
        var textoMacros = $@"
Alimentos detectados: {string.Join(", ", alimentosPt)}
Porção: {porcao}
Calorias: {Math.Round(caloriasTotal, 0)}kcal
Proteínas: {Math.Round(protTotal, 1)}g
Carboidratos: {Math.Round(carbTotal, 1)}g
Gorduras: {Math.Round(gordTotal, 1)}g
";
        
        // 4.3 Monta SystemPrompt personalizado
        var systemPrompt = contextInjector.BuildSystemPrompt(userContext);
        
        // 4.4 Envia para o Groq
        var feedback = await nutriBrain.AnalisarRotuloAsync(textoMacros, systemPrompt);
        
        // 4.5 Preenche os campos de feedback na análise
        analiseFinal.PodeConsumir = feedback.PodeConsumir;
        analiseFinal.PontuacaoSaude = feedback.PontuacaoSaude;
        analiseFinal.AnaliseEmRelacaoAMeta = feedback.AnaliseEmRelacaoAMeta ?? "";
        analiseFinal.DicaBSFM = feedback.DicaBSFM ?? "";
        
        Console.WriteLine($"[NUTRIBRAIN] Feedback gerado: Score={feedback.PontuacaoSaude}, PodeConsumir={feedback.PodeConsumir}");
    }
    catch (Exception ex)
    {
        // Se o Groq falhar, ainda salvamos a análise sem feedback
        Console.WriteLine($"[NUTRIBRAIN AVISO] Feedback IA não disponível: {ex.Message}");
        analiseFinal.PontuacaoSaude = 0;
        analiseFinal.AnaliseEmRelacaoAMeta = "Feedback da IA não disponível no momento.";
        analiseFinal.DicaBSFM = "Tente novamente mais tarde para obter uma análise personalizada.";
    }

    try {
        db.AnalisesIA.Add(analiseFinal);
        await db.SaveChangesAsync();
    } catch (Exception ex) {
        Console.WriteLine($"[ERRO BANCO] {ex.Message}");
    }

    return Results.Ok(new { 
        sucesso = true,
        dados = analiseFinal 
    });

}).DisableAntiforgery();

app.MapGet("/historico-analises/{usuarioId}", async (int usuarioId, PonteBanco.PonteDB db) => 
{
    try {
        var historico = await db.AnalisesIA
            .Where(a => a.UsuarioID == usuarioId)
            .OrderByDescending(a => a.DataAnalise)
            .ToListAsync();

        return Results.Ok(historico);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /historico-analises: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados." }, statusCode: 500);
    }
});

app.MapGet("/evolucao/{usuarioId}", async (int usuarioId, PonteBanco.PonteDB db) => {
    try {
        var logs = await db.Historicos
            .Where(h => h.UsuarioID == usuarioId)
            .OrderByDescending(h => h.DataRegistro)
            .ToListAsync();
        return Results.Ok(logs);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /evolucao: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados." }, statusCode: 500);
    }
});

// ROTA: Registrar nova medição (Peso/Altura)
app.MapPost("/atualizar-medicao", async (HistoricoProgresso novaMedicao, PonteBanco.PonteDB db) => {
    try {
        // 1. Calcula o IMC para o histórico
        novaMedicao.IMC = Math.Round(novaMedicao.Peso / (novaMedicao.Altura * novaMedicao.Altura), 2);
        novaMedicao.DataRegistro = DateTime.Now;

        // 2. Salva no Histórico
        db.Historicos.Add(novaMedicao);

        // 3. Importante: Atualiza o peso/altura atual na tabela de Usuario também (para o dashboard mudar)
        var user = await db.Usuarios.FindAsync(novaMedicao.UsuarioID);
        if (user != null) {
            user.Peso = novaMedicao.Peso;
            user.Altura = novaMedicao.Altura;
            new CalcularNutricional().RegistrarCalculos(user); // Recalcula IMC/TMB/Gasto
            db.Usuarios.Update(user);
        }

        await db.SaveChangesAsync();
        return Results.Ok(new { mensagem = "Medição registrada!", imc = novaMedicao.IMC, userAtualizado = user });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /atualizar-medicao: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados." }, statusCode: 500);
    }
});

app.MapPost("/definir-meta", async (MetaDTO dados, PonteBanco.PonteDB db) => {
    try {
        var user = await db.Usuarios.FindAsync(dados.UsuarioId);
        if (user == null) return Results.NotFound();
        
        user.PesoMeta = dados.PesoMeta;
        await db.SaveChangesAsync();
        
        return Results.Ok(new { pesoMeta = user.PesoMeta });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /definir-meta: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao conectar com o banco de dados." }, statusCode: 500);
    }
});

// ============================================================
// NOVAS ROTAS: USUÁRIO (perfil, data de nascimento, nome)
// ============================================================

// GET /usuario/{id} - Retorna dados completos do usuário
app.MapGet("/usuario/{id}", async (int id, PonteBanco.PonteDB db) => {
    try {
        var user = await db.Usuarios.FindAsync(id);
        if (user == null) return Results.NotFound(new { mensagem = "Usuário não encontrado." });
        
        return Results.Ok(new {
            id = user.ID,
            nome = user.Nome,
            email = user.Email,
            peso = user.Peso,
            altura = user.Altura,
            imc = user.IMC,
            tmb = user.TMB,
            gasto = user.GastoTotal,
            pesoMeta = user.PesoMeta,
            dataNascimento = user.DataNascimento,
            idade = user.CalcularIdade(),
            sexo = user.Sexo,
            tipoPessoa = user.TipoPessoa
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /usuario/{id}: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao buscar usuário." }, statusCode: 500);
    }
});

// PUT /usuario/atualizar-nome - Atualiza nome do usuário
app.MapPut("/usuario/atualizar-nome", async (AtualizarNomeDTO dto, PonteBanco.PonteDB db) => {
    try {
        var user = await db.Usuarios.FindAsync(dto.UsuarioId);
        if (user == null) return Results.NotFound();
        user.Nome = dto.Nome;
        await db.SaveChangesAsync();
        return Results.Ok(new { nome = user.Nome });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /usuario/atualizar-nome: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao atualizar nome." }, statusCode: 500);
    }
});

// PUT /usuario/atualizar-senha - Atualiza senha do usuário
app.MapPut("/usuario/atualizar-senha", async (AtualizarSenhaDTO dto, PonteBanco.PonteDB db) => {
    try {
        var user = await db.Usuarios.FindAsync(dto.UsuarioId);
        if (user == null) return Results.NotFound();
        user.SenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.NovaSenha);
        await db.SaveChangesAsync();
        return Results.Ok(new { mensagem = "Senha atualizada com sucesso!" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /usuario/atualizar-senha: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao atualizar senha." }, statusCode: 500);
    }
});

// PUT /usuario/atualizar-data-nascimento - Atualiza data de nascimento
app.MapPut("/usuario/atualizar-data-nascimento", async (AtualizarDataNascimentoDTO dto, PonteBanco.PonteDB db) => {
    try {
        var user = await db.Usuarios.FindAsync(dto.UsuarioId);
        if (user == null) return Results.NotFound();
        user.DataNascimento = dto.DataNascimento;
        user.Idade = user.CalcularIdade(); // Atualiza o campo Idade também
        await db.SaveChangesAsync();
        return Results.Ok(new { dataNascimento = user.DataNascimento, idade = user.Idade });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /usuario/atualizar-data-nascimento: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao atualizar data de nascimento." }, statusCode: 500);
    }
});

// ============================================================
// NOVAS ROTAS: CONSUMO DE ÁGUA
// ============================================================

// POST /registrar-agua - Registra consumo de água
app.MapPost("/registrar-agua", async (RegistrarAguaDTO dto, PonteBanco.PonteDB db) => {
    try {
        var consumo = new ConsumoAgua {
            UsuarioId = dto.UsuarioId,
            Ml = dto.Ml,
            DataRegistro = DateTime.Now
        };
        db.ConsumoAgua.Add(consumo);
        await db.SaveChangesAsync();
        return Results.Ok(new { mensagem = "Água registrada!", id = consumo.Id });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /registrar-agua: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao registrar água." }, statusCode: 500);
    }
});

// GET /agua-diario/{usuarioId} - Retorna consumo de água do dia
app.MapGet("/agua-diario/{usuarioId}", async (int usuarioId, PonteBanco.PonteDB db) => {
    try {
        var hoje = DateTime.Today;
        var consumos = await db.ConsumoAgua
            .Where(c => c.UsuarioId == usuarioId && c.DataRegistro >= hoje)
            .ToListAsync();
        var totalMl = consumos.Sum(c => c.Ml);
        return Results.Ok(new { totalMl, registros = consumos.Count });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /agua-diario: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao buscar consumo de água." }, statusCode: 500);
    }
});

// GET /agua-semanal/{usuarioId} - Retorna consumo de água da semana
app.MapGet("/agua-semanal/{usuarioId}", async (int usuarioId, PonteBanco.PonteDB db) => {
    try {
        var inicioSemana = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
        var consumos = await db.ConsumoAgua
            .Where(c => c.UsuarioId == usuarioId && c.DataRegistro >= inicioSemana)
            .ToListAsync();
        
        var dias = new List<string> { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" };
        var semana = dias.Select((dia, i) => {
            var data = inicioSemana.AddDays(i);
            var total = consumos.Where(c => c.DataRegistro.Date == data.Date).Sum(c => c.Ml);
            return new { dia, totalMl = total };
        }).ToList();

        var totalSemanal = consumos.Sum(c => c.Ml);
        return Results.Ok(new { semana, totalSemanal });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /agua-semanal: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao buscar consumo semanal." }, statusCode: 500);
    }
});

// ============================================================
// NOVA ROTA: REMOVER ÚLTIMO REGISTRO DE ÁGUA
// ============================================================

// DELETE /remover-ultima-agua/{usuarioId} - Remove o último registro de água do dia
app.MapDelete("/remover-ultima-agua/{usuarioId}", async (int usuarioId, PonteBanco.PonteDB db) => {
    try {
        var hoje = DateTime.Today;
        var ultimoRegistro = await db.ConsumoAgua
            .Where(c => c.UsuarioId == usuarioId && c.DataRegistro >= hoje)
            .OrderByDescending(c => c.DataRegistro)
            .FirstOrDefaultAsync();

        if (ultimoRegistro == null)
            return Results.Json(new { mensagem = "Nenhum registro de água hoje para desfazer." }, statusCode: 404);

        db.ConsumoAgua.Remove(ultimoRegistro);
        await db.SaveChangesAsync();
        return Results.Ok(new { mensagem = "Último registro de água removido!", removido = ultimoRegistro.Ml });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /remover-ultima-agua: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao remover registro de água." }, statusCode: 500);
    }
});

// ============================================================
// NOVAS ROTAS: REFEIÇÕES AGENDADAS (PRATOS DA SEMANA)
// ============================================================

// GET /refeicoes-semana/{usuarioId} - Lista refeições agendadas do usuário
app.MapGet("/refeicoes-semana/{usuarioId}", async (int usuarioId, PonteBanco.PonteDB db) => {
    try {
        var refeicoes = await db.RefeicoesAgendadas
            .Where(r => r.UsuarioId == usuarioId)
            .OrderBy(r => r.DiaSemana)
            .ThenBy(r => r.TipoRefeicao)
            .ToListAsync();

        // Se o usuário não tem refeições, copia as receitas padrão do usuário 0 (Sistema)
        if (!refeicoes.Any() && usuarioId != 0)
        {
            var receitasPadrao = await db.RefeicoesAgendadas
                .Where(r => r.UsuarioId == 0)
                .ToListAsync();

            if (receitasPadrao.Any())
            {
                foreach (var receita in receitasPadrao)
                {
                    db.RefeicoesAgendadas.Add(new RefeicaoAgendada
                    {
                        UsuarioId = usuarioId,
                        DiaSemana = receita.DiaSemana,
                        TipoRefeicao = receita.TipoRefeicao,
                        NomePrato = receita.NomePrato,
                        Ingredientes = receita.Ingredientes,
                        ModoPreparo = receita.ModoPreparo,
                        Calorias = receita.Calorias,
                        Proteinas = receita.Proteinas,
                        Carboidratos = receita.Carboidratos,
                        Gorduras = receita.Gorduras,
                        DataCriacao = DateTime.Now
                    });
                }
                await db.SaveChangesAsync();

                // Recarrega as refeições agora copiadas
                refeicoes = await db.RefeicoesAgendadas
                    .Where(r => r.UsuarioId == usuarioId)
                    .OrderBy(r => r.DiaSemana)
                    .ThenBy(r => r.TipoRefeicao)
                    .ToListAsync();
            }
        }

        return Results.Ok(refeicoes);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /refeicoes-semana: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao buscar refeições." }, statusCode: 500);
    }
});

// POST /salvar-refeicao-semana - Salva uma refeição agendada
app.MapPost("/salvar-refeicao-semana", async (RefeicaoAgendada refeicao, PonteBanco.PonteDB db) => {
    try {
        db.RefeicoesAgendadas.Add(refeicao);
        await db.SaveChangesAsync();
        return Results.Ok(new { mensagem = "Refeição salva!", id = refeicao.Id });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /salvar-refeicao-semana: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao salvar refeição." }, statusCode: 500);
    }
});

// DELETE /remover-refeicao-semana/{id} - Remove uma refeição agendada
app.MapDelete("/remover-refeicao-semana/{id}", async (int id, PonteBanco.PonteDB db) => {
    try {
        var refeicao = await db.RefeicoesAgendadas.FindAsync(id);
        if (refeicao == null) return Results.NotFound();
        db.RefeicoesAgendadas.Remove(refeicao);
        await db.SaveChangesAsync();
        return Results.Ok(new { mensagem = "Refeição removida!" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] /remover-refeicao-semana: {ex.Message}");
        return Results.Json(new { mensagem = "Erro ao remover refeição." }, statusCode: 500);
    }
});

// ============================================================
// NOVA ROTA: Análise de Rótulos (OCR + Groq Llama 3)
// POST /api/rotulo/analisar
// Body: { usuarioId: int, textoOcr: string }
// ============================================================
app.MapPost("/api/rotulo/analisar", async (
    BSFM.CoreAnalytics.Backend.Controllers.AnalisarRotuloRequest request,
    BSFM.CoreAnalytics.Backend.Services.ContextInjectorService contextInjector,
    BSFM.CoreAnalytics.Backend.Services.NutriBrainService nutriBrain,
    PonteBanco.PonteDB db,
    ILogger<Program> logger) =>
{
    // Validações
    if (request.UsuarioId <= 0)
        return Results.BadRequest(new { mensagem = "ID do usuário inválido." });

    if (string.IsNullOrWhiteSpace(request.TextoOcr))
        return Results.BadRequest(new { mensagem = "Texto OCR não pode estar vazio. Capture a foto do rótulo primeiro." });

    if (request.TextoOcr.Length < 10)
        return Results.BadRequest(new { mensagem = "Texto OCR muito curto. Tente uma foto mais nítida da tabela nutricional." });

    try
    {
        logger.LogInformation("[ROTULO] Iniciando análise para usuário {UsuarioId}", request.UsuarioId);

        // 1. Busca contexto do usuário
        var userContext = await contextInjector.BuildContextAsync(request.UsuarioId);

        // 2. Monta SystemPrompt personalizado
        var systemPrompt = contextInjector.BuildSystemPrompt(userContext);

        // 3. Envia para o Groq
        var resultado = await nutriBrain.AnalisarRotuloAsync(request.TextoOcr, systemPrompt);

        // 4. Salva no histórico
        try
        {
            var analise = new ClassesBSFM.AnaliseIA
            {
                UsuarioID = request.UsuarioId,
                Alimento = resultado.ProdutoDetectado ?? "Rótulo escaneado",
                Porcao = "N/A",
                Calorias = 0,
                Proteinas = 0,
                Carbos = 0,
                Gorduras = 0,
                DataAnalise = DateTime.Now,
                PodeConsumir = resultado.PodeConsumir,
                PontuacaoSaude = resultado.PontuacaoSaude,
                AnaliseEmRelacaoAMeta = resultado.AnaliseEmRelacaoAMeta ?? "",
                DicaBSFM = resultado.DicaBSFM ?? ""
            };

            db.AnalisesIA.Add(analise);
            await db.SaveChangesAsync();
            logger.LogInformation("[ROTULO] Análise salva no histórico com ID {AnaliseId}", analise.ID);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[ROTULO] Não foi possível salvar análise no histórico");
        }

        logger.LogInformation("[ROTULO] Análise concluída: {Produto} - Score: {Score}", 
            resultado.ProdutoDetectado, resultado.PontuacaoSaude);

        return Results.Ok(new
        {
            produtoDetectado = resultado.ProdutoDetectado,
            podeConsumir = resultado.PodeConsumir,
            pontuacaoSaude = resultado.PontuacaoSaude,
            analiseEmRelacaoAMeta = resultado.AnaliseEmRelacaoAMeta,
            dicaBSFM = resultado.DicaBSFM
        });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning(ex, "[ROTULO] Usuário não encontrado: {UsuarioId}", request.UsuarioId);
        return Results.NotFound(new { mensagem = ex.Message });
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "[ROTULO] Erro de comunicação com Groq");
        return Results.Json(new { mensagem = "Serviço de IA temporariamente indisponível. Tente novamente em alguns segundos." }, statusCode: 502);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[ROTULO] Erro interno ao analisar rótulo");
        return Results.Json(new { mensagem = "Erro interno ao analisar o rótulo. Tente novamente." }, statusCode: 500);
    }
});

app.Run(); // FINAL DO ARQUIVO

// Modelos de dados (DTOs)
public record LoginDTO(string Email, string Senha);
public record SolicitacaoEmail(string Email);
public record RedefinicaoSenha(string Email, string NovaSenha);
public record RedefinicaoFinal(string Email, string NovaSenha);
public record EsqueceuSenhaDTO(string Email);
public record RedefinicaoSenhaDTO(string Email, string NovaSenha);
public record MetaDTO(int UsuarioId, double PesoMeta);
public record AtualizarNomeDTO(int UsuarioId, string Nome);
public record AtualizarSenhaDTO(int UsuarioId, string NovaSenha);
public record AtualizarDataNascimentoDTO(int UsuarioId, DateTime DataNascimento);
public record RegistrarAguaDTO(int UsuarioId, double Ml);
