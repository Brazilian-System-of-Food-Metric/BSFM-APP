using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BSFM.Dados;

/// <summary>
/// Fábrica de contexto em tempo de design (usada por `dotnet ef`).
///
/// A escolha do provedor vem de <c>BSFM_BD_PROVIDER</c> (npgsql | sqlite).
/// O padrão é Postgres porque é o banco de produção — migrations geradas aqui
/// são a autoridade do schema.
/// </summary>
public sealed class FabricaContextoDesignTime : IDesignTimeDbContextFactory<BsfmDbContext>
{
    public BsfmDbContext CreateDbContext(string[] args)
    {
        var provedor = Environment.GetEnvironmentVariable("BSFM_BD_PROVIDER") ?? "npgsql";

        var opcoes = new DbContextOptionsBuilder<BsfmDbContext>();

        if (string.Equals(provedor, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            opcoes.UseSqlite("Data Source=bsfm-design.db");
        }
        else
        {
            // String de conexão fictícia: geração de migration não abre conexão.
            opcoes.UseNpgsql("Host=localhost;Database=bsfm;Username=design;Password=design");
        }

        return new BsfmDbContext(opcoes.Options);
    }
}

/// <summary>
/// Montagem do contexto em tempo de execução, a partir da URL do banco.
/// Aceita o formato `postgres://usuario:senha@host:porta/banco` usado por
/// Neon/Railway e converte para a string de conexão do Npgsql.
/// </summary>
public static class FabricaContexto
{
    public const string VariavelUrlBanco = "DATABASE_URL";
    public const string VariavelProvedor = "BSFM_BD_PROVIDER";

    public static BsfmDbContext Criar(string? urlBanco = null, string? provedor = null)
    {
        urlBanco ??= Environment.GetEnvironmentVariable(VariavelUrlBanco);
        provedor ??= Environment.GetEnvironmentVariable(VariavelProvedor);

        var opcoes = new DbContextOptionsBuilder<BsfmDbContext>();

        if (string.IsNullOrWhiteSpace(urlBanco))
        {
            // Desenvolvimento local sem banco: SQLite em arquivo.
            opcoes.UseSqlite("Data Source=UsuariosBSFM.db");
        }
        else if (string.Equals(provedor, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            opcoes.UseSqlite(urlBanco);
        }
        else
        {
            opcoes.UseNpgsql(ConverterUrlPostgres(urlBanco), npgsql =>
            {
                // Neon suspende a instância e derruba conexões ociosas. Sem retry, a
                // primeira consulta depois da pausa falha com EndOfStream.
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                npgsql.CommandTimeout(30);
            });
        }

        return new BsfmDbContext(opcoes.Options);
    }

    /// <summary>Converte `postgres://...` em string de conexão Npgsql.</summary>
    public static string ConverterUrlPostgres(string url)
    {
        if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            // Já é uma string de conexão no formato chave=valor.
            return url;
        }

        var uri = new Uri(url.Replace("postgres://", "postgresql://", StringComparison.OrdinalIgnoreCase));
        var credenciais = uri.UserInfo.Split(':', 2);

        var porta = uri.Port > 0 ? uri.Port : 5432;
        var ssl = url.Contains("sslmode=disable", StringComparison.OrdinalIgnoreCase) ? "Disable" : "Require";

        return $"Host={uri.Host};"
             + $"Port={porta};"
             + $"Username={Uri.UnescapeDataString(credenciais[0])};"
             + $"Password={Uri.UnescapeDataString(credenciais.Length > 1 ? credenciais[1] : string.Empty)};"
             + $"Database={uri.AbsolutePath.TrimStart('/')};"
             + $"SSL Mode={ssl};"
             + "Trust Server Certificate=true;"
             + "Pooling=true;Maximum Pool Size=20;"
             + "Connection Idle Lifetime=30;Keepalive=30;"
             + "Timeout=15;Command Timeout=30;";
    }
}
