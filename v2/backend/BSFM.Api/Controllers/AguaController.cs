using BSFM.Api.DTOs;
using BSFM.Api.Filtros;
using BSFM.Api.Servicos;
using BSFM.Dados;
using BSFM.Dados.Entidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Controllers;

/// <summary>
/// Consumo de água do próprio usuário. Substitui `/registrar-agua`,
/// `/agua-diario/{usuarioId}`, `/agua-semanal/{usuarioId}` e
/// `DELETE /remover-ultima-agua/{usuarioId}` — todos anônimos e por id na URL.
/// </summary>
[ApiController]
[Route("api/eu/agua")]
[SessaoObrigatoria]
public sealed class AguaController : ControllerBase
{
    private const double MaximoPorRegistro = 3000;

    private readonly BsfmDbContext _db;
    private readonly ServicoAuditoria _auditoria;

    public AguaController(BsfmDbContext db, ServicoAuditoria auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private int UsuarioId => HttpContext.UsuarioId();

    // -----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] AguaEntrada entrada, CancellationToken ct)
    {
        if (entrada.Ml is null or <= 0 or > MaximoPorRegistro)
            return BadRequest(new { erro = "quantidade_invalida" });

        var agora = DateTime.UtcNow;

        var registro = new ConsumoAgua
        {
            UsuarioId = UsuarioId,
            Ml = entrada.Ml.Value,
            DataRegistro = agora,
        };

        _db.ConsumoAgua.Add(registro);
        await _db.SaveChangesAsync(ct);

        var (total, meta) = await TotaisDoDiaAsync(DateOnly.FromDateTime(agora), ct);

        return Ok(new AguaDiaSaida(DateOnly.FromDateTime(agora), total, meta,
            meta > 0 ? Math.Round(total / meta * 100, 1) : 0,
            await _db.ConsumoAgua.CountAsync(a => a.UsuarioId == UsuarioId
                && a.DataRegistro.Date == agora.Date, ct)));
    }

    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> DoDia([FromQuery] string? data, CancellationToken ct)
    {
        var dia = DateOnly.TryParse(data, out var informado) ? informado : DateOnly.FromDateTime(DateTime.UtcNow);

        var (total, meta) = await TotaisDoDiaAsync(dia, ct);

        var registros = await _db.ConsumoAgua.AsNoTracking()
            .Where(a => a.UsuarioId == UsuarioId && a.DataRegistro.Date == dia.ToDateTime(TimeOnly.MinValue).Date)
            .OrderBy(a => a.DataRegistro)
            .Select(a => new { id = a.Id, ml = a.Ml, hora = a.DataRegistro })
            .ToListAsync(ct);

        return Ok(new
        {
            data = dia,
            totalMl = total,
            metaMl = meta,
            percentual = meta > 0 ? Math.Round(total / meta * 100, 1) : 0,
            registros,
        });
    }

    // -----------------------------------------------------------------------
    [HttpGet("semanal")]
    public async Task<IActionResult> Semanal(CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = hoje.AddDays(-6);

        var dados = await _db.ConsumoAgua.AsNoTracking()
            .Where(a => a.UsuarioId == UsuarioId
                && a.DataRegistro.Date >= inicio.ToDateTime(TimeOnly.MinValue).Date)
            .GroupBy(a => a.DataRegistro.Date)
            .Select(g => new { data = g.Key, total = g.Sum(x => x.Ml) })
            .ToListAsync(ct);

        var meta = await MetaDeAguaAsync(ct);

        var serie = Enumerable.Range(0, 7)
            .Select(deslocamento => inicio.AddDays(deslocamento))
            .Select(dia => new
            {
                data = dia,
                totalMl = dados.Where(d => DateOnly.FromDateTime(d.data) == dia).Sum(d => d.total),
                metaMl = meta,
            })
            .ToList();

        return Ok(new
        {
            inicio,
            fim = hoje,
            mediaMl = Math.Round(serie.Average(d => d.totalMl), 1),
            diasComMeta = serie.Count(d => d.totalMl >= d.metaMl),
            serie,
        });
    }

    // -----------------------------------------------------------------------
    [HttpDelete("ultimo")]
    public async Task<IActionResult> RemoverUltimo(CancellationToken ct)
    {
        var ultimo = await _db.ConsumoAgua
            .Where(a => a.UsuarioId == UsuarioId)
            .OrderByDescending(a => a.DataRegistro)
            .FirstOrDefaultAsync(ct);

        if (ultimo is null)
            return NotFound(new { erro = "nenhum_registro" });

        _db.ConsumoAgua.Remove(ultimo);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("agua_removida", UsuarioId, "ConsumoAgua", ultimo.Id.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return NoContent();
    }

    // -----------------------------------------------------------------------
    private async Task<(double Total, double Meta)> TotaisDoDiaAsync(DateOnly dia, CancellationToken ct)
    {
        var alvo = dia.ToDateTime(TimeOnly.MinValue).Date;
        var total = await _db.ConsumoAgua.AsNoTracking()
            .Where(a => a.UsuarioId == UsuarioId && a.DataRegistro.Date == alvo)
            .SumAsync(a => (double?)a.Ml, ct) ?? 0;

        return (total, await MetaDeAguaAsync(ct));
    }

    /// <summary>Meta de água da meta ativa; se não houver, deriva do peso.</summary>
    private async Task<double> MetaDeAguaAsync(CancellationToken ct)
    {
        var meta = await _db.Metas.AsNoTracking()
            .Where(m => m.UsuarioId == UsuarioId && m.Ativa)
            .Select(m => (double?)m.AguaAlvoMl)
            .FirstOrDefaultAsync(ct);

        if (meta is > 0)
            return meta.Value;

        var peso = await _db.Usuarios.AsNoTracking()
            .Where(u => u.ID == UsuarioId)
            .Select(u => u.Peso)
            .FirstOrDefaultAsync(ct);

        return CalculadoraNutricional.CalcularAguaAlvoMl(peso);
    }
}
