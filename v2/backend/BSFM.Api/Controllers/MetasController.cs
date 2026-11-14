using BSFM.Api.DTOs;
using BSFM.Api.Filtros;
using BSFM.Api.Servicos;
using BSFM.Dados;
using BSFM.Dados.Entidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Controllers;

/// <summary>
/// Metas nutricionais. Substitui `POST /definir-meta`, que gravava direto em
/// `Usuarios.PesoMeta` sem histórico e sem validação.
///
/// Aqui cada definição cria uma linha em `Metas` e desativa a anterior: dá para
/// reconstruir a evolução das metas, não só o valor atual.
/// </summary>
[ApiController]
[Route("api/eu/metas")]
[SessaoObrigatoria]
public sealed class MetasController : ControllerBase
{
    private readonly BsfmDbContext _db;
    private readonly ServicoAuditoria _auditoria;

    public MetasController(BsfmDbContext db, ServicoAuditoria auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private int UsuarioId => HttpContext.UsuarioId();

    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken ct)
    {
        var meta = await _db.Metas.AsNoTracking()
            .Where(m => m.UsuarioId == UsuarioId && m.Ativa)
            .OrderByDescending(m => m.CriadoEm)
            .FirstOrDefaultAsync(ct);

        var usuario = await _db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);

        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        // Sem meta definida, deriva uma a partir do gasto energético atual.
        var calorias = meta?.CaloriasAlvo > 0 ? meta.CaloriasAlvo : Math.Round(usuario.GastoTotal, 0);
        var proteinas = meta?.ProteinasAlvo > 0 ? meta.ProteinasAlvo : Math.Round(usuario.Peso * 1.6, 0);
        var agua = meta?.AguaAlvoMl > 0 ? meta.AguaAlvoMl : CalculadoraNutricional.CalcularAguaAlvoMl(usuario.Peso);
        var pesoAlvo = meta?.PesoAlvo > 0 ? meta.PesoAlvo : usuario.PesoMeta;

        var inicioDoDia = DateTime.UtcNow.Date;
        var consumidas = await _db.AnalisesIA.AsNoTracking()
            .Where(a => a.UsuarioID == UsuarioId && a.DataAnalise >= inicioDoDia)
            .SumAsync(a => (double?)a.Calorias, ct) ?? 0;

        return Ok(new MetaSaida(
            calorias, proteinas, agua, pesoAlvo,
            Math.Round(consumidas, 1),
            calorias > 0 ? Math.Round(consumidas / calorias * 100, 1) : 0,
            meta?.CriadoEm ?? DateTime.UtcNow));
    }

    // -----------------------------------------------------------------------
    [HttpPut]
    public async Task<IActionResult> Definir([FromBody] MetaEntrada entrada, CancellationToken ct)
    {
        if (entrada.CaloriasAlvo is { } calorias && (!double.IsFinite(calorias) || calorias <= 0 || calorias > 10000))
            return BadRequest(new { erro = "calorias_alvo_invalidas" });

        if (entrada.ProteinasAlvo is { } proteinas && (!double.IsFinite(proteinas) || proteinas < 0 || proteinas > 1000))
            return BadRequest(new { erro = "proteinas_alvo_invalidas" });

        if (entrada.AguaAlvoMl is { } agua && (!double.IsFinite(agua) || agua < 0 || agua > 10000))
            return BadRequest(new { erro = "agua_alvo_invalida" });

        if (entrada.PesoAlvo is { } pesoAlvo && (!double.IsFinite(pesoAlvo) || pesoAlvo <= 0 || pesoAlvo > 500))
            return BadRequest(new { erro = "peso_alvo_invalido" });

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        var anteriores = await _db.Metas
            .Where(m => m.UsuarioId == UsuarioId && m.Ativa)
            .ToListAsync(ct);

        foreach (var m in anteriores) m.Ativa = false;

        var nova = new Meta
        {
            UsuarioId = UsuarioId,
            CaloriasAlvo = entrada.CaloriasAlvo ?? 0,
            ProteinasAlvo = entrada.ProteinasAlvo ?? 0,
            AguaAlvoMl = entrada.AguaAlvoMl ?? CalculadoraNutricional.CalcularAguaAlvoMl(usuario.Peso),
            PesoAlvo = entrada.PesoAlvo ?? usuario.PesoMeta,
            CriadoEm = DateTime.UtcNow,
            Ativa = true,
        };

        _db.Metas.Add(nova);

        // Espelha o peso-alvo em Usuarios por compatibilidade com o app atual.
        usuario.PesoMeta = nova.PesoAlvo;
        usuario.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("meta_definida", UsuarioId, "Meta", nova.Id.ToString(),
            $"{{\"calorias\":{nova.CaloriasAlvo},\"agua\":{nova.AguaAlvoMl}}}",
            ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new { mensagem = "Meta definida.", id = nova.Id, metasAnterioresDesativadas = anteriores.Count });
    }

    // -----------------------------------------------------------------------
    [HttpGet("historico")]
    public async Task<IActionResult> Historico(CancellationToken ct)
    {
        var lista = await _db.Metas.AsNoTracking()
            .Where(m => m.UsuarioId == UsuarioId)
            .OrderByDescending(m => m.CriadoEm)
            .Select(m => new
            {
                id = m.Id,
                caloriasAlvo = m.CaloriasAlvo,
                proteinasAlvo = m.ProteinasAlvo,
                aguaAlvoMl = m.AguaAlvoMl,
                pesoAlvo = m.PesoAlvo,
                criadoEm = m.CriadoEm,
                ativa = m.Ativa,
            })
            .ToListAsync(ct);

        return Ok(lista);
    }
}
