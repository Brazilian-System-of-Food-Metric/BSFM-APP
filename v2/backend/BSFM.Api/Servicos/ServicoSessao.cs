using System.Security.Cryptography;
using System.Text;
using BSFM.Dados;
using BSFM.Dados.Entidades;
using BSFM.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Servicos;

/// <summary>
/// Regras de sessão. Concentra tudo que precisa ser persistido para que um
/// token possa ser revogado antes de expirar.
/// </summary>
public sealed class ServicoSessao
{
    private readonly BsfmDbContext _db;
    private readonly EmissorSessao _emissor;
    private readonly SegredoMestre _segredo;
    private readonly TimeSpan _validadeSessao;
    private readonly TimeSpan _validadeRefresh;

    public ServicoSessao(BsfmDbContext db, EmissorSessao emissor, SegredoMestre segredo)
    {
        _db = db;
        _emissor = emissor;
        _segredo = segredo;
        _validadeSessao = TimeSpan.FromHours(2);
        _validadeRefresh = TimeSpan.FromDays(30);
    }

    public sealed record SessaoEmitida(string Token, string RefreshToken, string ChaveAssinatura, DateTime ExpiraEm, Guid SessaoId);

    /// <summary>Cria a sessão, emite o token e devolve o refresh em claro (única vez).</summary>
    public async Task<SessaoEmitida> CriarAsync(Usuario usuario, string? ipHash, string? userAgentHash, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var jti = Base64Url.Codificar(RandomNumberGenerator.GetBytes(16));

        var sessao = new Sessao
        {
            UsuarioId = usuario.ID,
            Jti = jti,
            CriadaEm = agora,
            ExpiraEm = agora.Add(_validadeSessao),
            IpHash = ipHash,
            UserAgentHash = userAgentHash,
        };

        _db.Sessoes.Add(sessao);

        var refreshClaro = GerarRefreshClaro();
        _db.RefreshTokens.Add(new RefreshToken
        {
            SessaoId = sessao.Id,
            TokenHash = HashRefresh(refreshClaro),
            CriadoEm = agora,
            ExpiraEm = agora.Add(_validadeRefresh),
            IpHash = ipHash,
        });

        await _db.SaveChangesAsync(ct);

        var token = _emissor.Emitir(usuario.ID, usuario.Papel, jti);
        return new SessaoEmitida(token, refreshClaro, ChaveSessao.Derivar(_segredo.Valor, jti), sessao.ExpiraEm, sessao.Id);
    }

    /// <summary>
    /// Rotaciona o refresh. Se o token apresentado já foi rotacionado antes,
    /// a cadeia inteira é revogada — sinal de token roubado.
    /// </summary>
    public async Task<SessaoEmitida?> RotacionarAsync(string refreshClaro, string? ipHash, CancellationToken ct = default)
    {
        var hash = HashRefresh(refreshClaro);
        var agora = DateTime.UtcNow;

        var atual = await _db.RefreshTokens
            .Include(r => r.Sessao)
            .SingleOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (atual is null)
            return null;

        // Reuso de token já rotacionado: derruba tudo daquela sessão.
        if (atual.RevogadoEm is not null)
        {
            await RevogarSessaoAsync(atual.SessaoId, "refresh_reutilizado", ct);
            return null;
        }

        if (!atual.EstaAtivo(agora) || atual.Sessao is null || !atual.Sessao.EstaAtiva(agora))
            return null;

        var sessao = atual.Sessao;

        var novoClaro = GerarRefreshClaro();
        var novo = new RefreshToken
        {
            SessaoId = sessao.Id,
            TokenHash = HashRefresh(novoClaro),
            CriadoEm = agora,
            ExpiraEm = agora.Add(_validadeRefresh),
            IpHash = ipHash,
        };

        atual.RevogadoEm = agora;
        atual.SubstituidoPorId = novo.Id;
        _db.RefreshTokens.Add(novo);

        sessao.UltimoUsoEm = agora;
        await _db.SaveChangesAsync(ct);

        var token = _emissor.Emitir(sessao.UsuarioId, "usuario", sessao.Jti);
        return new SessaoEmitida(token, novoClaro, ChaveSessao.Derivar(_segredo.Valor, sessao.Jti), sessao.ExpiraEm, sessao.Id);
    }

    public async Task RevogarSessaoAsync(Guid sessaoId, string motivo, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;

        var sessao = await _db.Sessoes.SingleOrDefaultAsync(s => s.Id == sessaoId, ct);
        if (sessao is not null && sessao.RevogadaEm is null)
        {
            sessao.RevogadaEm = agora;
            sessao.RevogadaMotivo = motivo;
        }

        var tokens = await _db.RefreshTokens.Where(r => r.SessaoId == sessaoId && r.RevogadoEm == null).ToListAsync(ct);
        foreach (var t in tokens) t.RevogadoEm = agora;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RevogarTodasDoUsuarioAsync(int usuarioId, string motivo, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var sessoes = await _db.Sessoes.Where(s => s.UsuarioId == usuarioId && s.RevogadaEm == null).ToListAsync(ct);

        foreach (var s in sessoes)
        {
            s.RevogadaEm = agora;
            s.RevogadaMotivo = motivo;
        }

        var sessoesIds = sessoes.Select(s => s.Id).ToList();
        var tokens = await _db.RefreshTokens
            .Where(r => sessoesIds.Contains(r.SessaoId) && r.RevogadoEm == null)
            .ToListAsync(ct);

        foreach (var t in tokens) t.RevogadoEm = agora;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Localiza o refresh pelo valor em claro sem rotacionar (usado no logout).</summary>
    public async Task<RefreshToken?> LocalizarRefreshAsync(string refreshClaro, CancellationToken ct = default)
    {
        var hash = HashRefresh(refreshClaro);
        return await _db.RefreshTokens.SingleOrDefaultAsync(r => r.TokenHash == hash, ct);
    }

    public static string HashRefresh(string claro) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(claro))).ToLowerInvariant();

    private static string GerarRefreshClaro() => Base64Url.Codificar(RandomNumberGenerator.GetBytes(32));

    /// <summary>Hash de IP para auditoria: guarda o suficiente para correlacionar, não o IP.</summary>
    public static string? HashIp(string? ip) =>
        string.IsNullOrWhiteSpace(ip) ? null
        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ip)))[..16].ToLowerInvariant();
}
