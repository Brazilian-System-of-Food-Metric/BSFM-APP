using BSFM.Dados;
using BSFM.Dados.Entidades;
using Microsoft.EntityFrameworkCore;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

// ============================================================================
// Verificação do schema: materializa o modelo num banco SQLite real e confere
// que tabelas, índices únicos e chaves estrangeiras existem de fato.
//
// Não substitui a migration de produção (Postgres), mas prova que o modelo é
// coerente e que o caminho de desenvolvimento (SQLite) sobe sem surpresa.
// ============================================================================

var passou = 0;
var falhou = 0;

void Verificar(string nome, bool ok, string? detalhe = null)
{
    if (ok) { passou++; Console.WriteLine($"  PASS  {nome}"); }
    else { falhou++; Console.WriteLine($"  FAIL  {nome}{(detalhe is null ? "" : $"  -> {detalhe}")}"); }
}

var arquivo = Path.Combine(Path.GetTempPath(), $"bsfm-schema-{Guid.NewGuid():N}.db");

var opcoes = new DbContextOptionsBuilder<BsfmDbContext>()
    .UseSqlite($"Data Source={arquivo}")
    .Options;

await using var db = new BsfmDbContext(opcoes);
await db.Database.EnsureCreatedAsync();

Console.WriteLine($"\nBanco de verificação: {arquivo}");

// ---------------------------------------------------------------------------
Console.WriteLine("\n[1] Tabelas criadas");
// ---------------------------------------------------------------------------
var tabelas = new List<string>();
await using (var cmd = db.Database.GetDbConnection().CreateCommand())
{
    await db.Database.OpenConnectionAsync();
    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
    await using var leitor = await cmd.ExecuteReaderAsync();
    while (await leitor.ReadAsync()) tabelas.Add(leitor.GetString(0));
}

var esperadas = new[]
{
    "Usuarios", "AnalisesIA", "Historicos", "ConsumoAgua", "RefeicoesAgendadas",
    "CronogramasSemanais", "RefeicoesDiarias", "Hospitais", "Metas", "Alimentos",
    "Sessoes", "RefreshTokens", "Auditoria", "ConsentimentosLGPD",
};

foreach (var t in esperadas)
    Verificar($"tabela {t}", tabelas.Contains(t), string.Join(",", tabelas));

Verificar("nenhuma tabela inesperada", tabelas.Count == esperadas.Length,
    string.Join(",", tabelas.Except(esperadas)));

// ---------------------------------------------------------------------------
Console.WriteLine("\n[2] Índices");
// ---------------------------------------------------------------------------
var indices = new List<string>();
await using (var cmd = db.Database.GetDbConnection().CreateCommand())
{
    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%'";
    await using var leitor = await cmd.ExecuteReaderAsync();
    while (await leitor.ReadAsync()) indices.Add(leitor.GetString(0));
}

var indicesEsperados = new[]
{
    "IX_Usuarios_Email_Unico", "IX_Sessoes_Jti_Unico", "IX_RefreshTokens_Hash_Unico",
    "IX_Hospitais_Cnes_Unico", "IX_AnalisesIA_Usuario_Data", "IX_Historicos_Usuario_Data",
    "IX_ConsumoAgua_Usuario_Data", "IX_RefeicoesAgendadas_Usuario_Dia",
    "IX_RefeicoesDiarias_Cronograma", "IX_Metas_Usuario_Ativa", "IX_Alimentos_Nome",
    "IX_Sessoes_Usuario_Expira", "IX_Sessoes_RevogadaEm",
    "IX_RefreshTokens_Sessao_Expira", "IX_Auditoria_Usuario_Data",
    "IX_Auditoria_Evento_Data", "IX_Consentimentos_Usuario_Finalidade",
    "IX_Hospitais_Uf_Cidade", "IX_Hospitais_Coordenadas", "IX_Usuarios_BloqueadoAte",
};

foreach (var i in indicesEsperados)
    Verificar($"índice {i}", indices.Contains(i));

// ---------------------------------------------------------------------------
Console.WriteLine("\n[3] Chaves estrangeiras com cascata");
// ---------------------------------------------------------------------------
async Task<string?> FkDe(string tabela, string coluna)
{
    await using var cmd = db.Database.GetDbConnection().CreateCommand();
    cmd.CommandText = $"PRAGMA foreign_key_list('{tabela}')";
    await using var leitor = await cmd.ExecuteReaderAsync();
    while (await leitor.ReadAsync())
    {
        // colunas: id, seq, table, from, to, on_update, on_delete, match
        if (leitor.GetString(3) == coluna)
            return $"{leitor.GetString(2)}:{leitor.GetString(6)}";
    }
    return null;
}

var fks = new (string Tabela, string Coluna, string Esperado)[]
{
    ("Sessoes", "UsuarioId", "Usuarios:CASCADE"),
    ("RefreshTokens", "SessaoId", "Sessoes:CASCADE"),
    ("ConsentimentosLGPD", "UsuarioId", "Usuarios:CASCADE"),
    ("AnalisesIA", "UsuarioID", "Usuarios:CASCADE"),
    ("Historicos", "UsuarioID", "Usuarios:CASCADE"),
    ("ConsumoAgua", "UsuarioId", "Usuarios:CASCADE"),
    ("RefeicoesDiarias", "CronogramaSemanalId", "CronogramasSemanais:CASCADE"),
};

foreach (var (tabela, coluna, esperado) in fks)
{
    var obtido = await FkDe(tabela, coluna);
    Verificar($"{tabela}.{coluna} -> {esperado}", obtido == esperado, obtido ?? "nenhuma");
}

// ---------------------------------------------------------------------------
Console.WriteLine("\n[4] Regras do modelo exercitadas");
// ---------------------------------------------------------------------------
var usuario = new Usuario
{
    Nome = "Ana Souza",
    Email = "ana@bsfm.dev",
    SenhaHash = "pbkdf2-sha256$100000$abc$def",
    DataNascimento = new DateTime(1998, 5, 20),
    Altura = 1.68,
    Peso = 68,
    TotpHabilitado = true,
    TotpSegredo = "GEZDGNBVGY3TQOJQ",
};

db.Usuarios.Add(usuario);
await db.SaveChangesAsync();
Verificar("usuário persistido ganha ID", usuario.ID > 0, $"{usuario.ID}");
Verificar("idade calculada a partir da data de nascimento", usuario.CalcularIdade() >= 27, $"{usuario.CalcularIdade()}");

// E-mail duplicado deve ser barrado pelo índice único.
var clone = new Usuario { Nome = "Clone", Email = "ana@bsfm.dev" };
db.Usuarios.Add(clone);
var duplicataBarrada = false;
try { await db.SaveChangesAsync(); }
catch (DbUpdateException) { duplicataBarrada = true; }
// A entidade recusada continua rastreada; sem soltá-la, todo SaveChanges seguinte falharia.
db.Entry(clone).State = EntityState.Detached;
Verificar("e-mail duplicado é recusado pelo banco", duplicataBarrada);

// Sessão + refresh em cascata.
var sessao = new Sessao
{
    UsuarioId = usuario.ID,
    Jti = "jti-teste-123",
    ExpiraEm = DateTime.UtcNow.AddHours(2),
};
db.Sessoes.Add(sessao);
await db.SaveChangesAsync();

db.RefreshTokens.Add(new RefreshToken
{
    SessaoId = sessao.Id,
    TokenHash = new string('a', 64),
    ExpiraEm = DateTime.UtcNow.AddDays(30),
});
await db.SaveChangesAsync();
Verificar("sessão e refresh persistem", await db.RefreshTokens.CountAsync() == 1);

// Apagar o usuário deve derrubar sessão, refresh, análise e consentimento.
db.AnalisesIA.Add(new AnaliseIA { UsuarioID = usuario.ID, Alimento = "Maçã", Confianca = 0.93 });
db.ConsentimentosLGPD.Add(new ConsentimentoLGPD
{
    UsuarioId = usuario.ID, Finalidade = "dados_saude", VersaoTermo = "1.0", Concedido = true,
});
await db.SaveChangesAsync();

db.Usuarios.Remove(usuario);
await db.SaveChangesAsync();

Verificar("apagar usuário remove sessões", await db.Sessoes.CountAsync() == 0);
Verificar("apagar usuário remove refresh tokens", await db.RefreshTokens.CountAsync() == 0);
Verificar("apagar usuário remove análises", await db.AnalisesIA.CountAsync() == 0);
Verificar("apagar usuário remove consentimentos", await db.ConsentimentosLGPD.CountAsync() == 0);

// ---------------------------------------------------------------------------
Console.WriteLine("\n[5] Hospital com dados geográficos (base de SP)");
// ---------------------------------------------------------------------------
db.Hospitais.Add(new Hospital
{
    Cnes = "2078015",
    NomeHospital = "Hospital das Clínicas da FMUSP",
    TipoEstabelecimento = "Hospital Geral",
    Endereco = "Av. Dr. Enéas Carvalho de Aguiar",
    Numero = "255",
    Bairro = "Cerqueira César",
    Cidade = "São Paulo",
    Uf = "SP",
    Cep = "05403-000",
    Telefone = "1132660000",
    Latitude = -23.5574,
    Longitude = -46.6709,
    Especialidades = "Cardiologia;Oncologia",
});
await db.SaveChangesAsync();

var hospital = await db.Hospitais.SingleAsync();
Verificar("hospital com coordenadas é persistido",
    hospital.Latitude is not null && hospital.Longitude is not null);
Verificar("CNES único é gravado", hospital.Cnes == "2078015");

var cnesDuplicado = false;
var cloneHospital = new Hospital { Cnes = "2078015", NomeHospital = "Duplicado" };
db.Hospitais.Add(cloneHospital);
try { await db.SaveChangesAsync(); }
catch (DbUpdateException) { cnesDuplicado = true; }
db.Entry(cloneHospital).State = EntityState.Detached;
Verificar("CNES duplicado é recusado", cnesDuplicado);

await db.Database.CloseConnectionAsync();
try { File.Delete(arquivo); } catch { }

Console.WriteLine($"\n================  {passou} PASS / {falhou} FAIL  ================");
return falhou == 0 ? 0 : 1;
