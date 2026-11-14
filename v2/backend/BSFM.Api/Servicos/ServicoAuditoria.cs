using BSFM.Dados;
using BSFM.Dados.Entidades;

namespace BSFM.Api.Servicos;

/// <summary>
/// Trilha de auditoria. Registra o evento, nunca o segredo: senha, token e
/// dado clínico não podem aparecer em texto claro aqui.
/// </summary>
public sealed class ServicoAuditoria
{
    private readonly BsfmDbContext _db;
    private readonly ILogger<ServicoAuditoria> _log;

    public ServicoAuditoria(BsfmDbContext db, ILogger<ServicoAuditoria> log)
    {
        _db = db;
        _log = log;
    }

    public async Task RegistrarAsync(
        string evento,
        int? usuarioId = null,
        string? entidade = null,
        string? entidadeId = null,
        string? dadosJson = null,
        string? ipHash = null,
        CancellationToken ct = default)
    {
        _db.Auditoria.Add(new Auditoria
        {
            Evento = evento,
            UsuarioId = usuarioId,
            Entidade = entidade,
            EntidadeId = entidadeId,
            DadosJson = dadosJson,
            IpHash = ipHash,
            CriadoEm = DateTime.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Auditoria nunca pode derrubar a operação principal.
            _log.LogWarning(ex, "Falha ao gravar auditoria do evento {Evento}", evento);
        }
    }
}

/// <summary>
/// Envio do código de verificação. A implementação de console existe para
/// desenvolvimento; em produção entra o Brevo, sem mudar os chamadores.
/// </summary>
public interface IEnviadorCodigo
{
    Task EnviarAsync(string email, string codigo, CancellationToken ct = default);
}

public sealed class EnviadorCodigoConsole : IEnviadorCodigo
{
    private readonly ILogger<EnviadorCodigoConsole> _log;

    public EnviadorCodigoConsole(ILogger<EnviadorCodigoConsole> log) => _log = log;

    public Task EnviarAsync(string email, string codigo, CancellationToken ct = default)
    {
        // Nunca vai para a resposta HTTP — esse era exatamente o furo original.
        _log.LogInformation("[EMAIL] codigo para {Email}: {Codigo}", email, codigo);
        Console.WriteLine($"[EMAIL] codigo para {email}: {codigo}");
        return Task.CompletedTask;
    }
}
