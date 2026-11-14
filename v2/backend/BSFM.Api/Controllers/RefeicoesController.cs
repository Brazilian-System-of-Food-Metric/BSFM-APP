using BSFM.Api.DTOs;
using BSFM.Api.Filtros;
using BSFM.Api.Servicos;
using BSFM.Dados;
using BSFM.Dados.Entidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Controllers;

/// <summary>
/// Refeições planejadas da semana. Substitui `/refeicoes-semana/{usuarioId}`,
/// `/salvar-refeicao-semana` e `DELETE /remover-refeicao-semana/{id}`.
///
/// O delete antigo aceitava qualquer id — aqui a remoção só ocorre se a
/// refeição pertencer ao usuário da sessão.
/// </summary>
[ApiController]
[Route("api/eu/refeicoes")]
[SessaoObrigatoria]
public sealed class RefeicoesController : ControllerBase
{
    private static readonly string[] DiasValidos =
    {
        "Segunda", "Terca", "Quarta", "Quinta", "Sexta", "Sabado", "Domingo",
    };

    private static readonly string[] TiposValidos =
    {
        "Cafe", "Almoco", "Jantar", "Lanche",
    };

    private readonly BsfmDbContext _db;
    private readonly ServicoAuditoria _auditoria;

    public RefeicoesController(BsfmDbContext db, ServicoAuditoria auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private int UsuarioId => HttpContext.UsuarioId();

    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var itens = await _db.RefeicoesAgendadas.AsNoTracking()
            .Where(r => r.UsuarioId == UsuarioId)
            .OrderBy(r => r.DiaSemana).ThenBy(r => r.TipoRefeicao)
            .Select(r => new
            {
                r.Id, r.DiaSemana, r.TipoRefeicao, r.NomePrato, r.Ingredientes, r.ModoPreparo,
                r.Calorias, r.Proteinas, r.Carboidratos, r.Gorduras, r.Concluida, r.ConcluidaEm,
            })
            .ToListAsync(ct);

        return Ok(new
        {
            total = itens.Count,
            caloriasSemana = Math.Round(itens.Sum(i => i.Calorias), 1),
            concluidas = itens.Count(i => i.Concluida),
            itens,
        });
    }

    // -----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] RefeicaoEntrada entrada, CancellationToken ct)
    {
        var dia = Normalizar(entrada.DiaSemana, DiasValidos);
        var tipo = Normalizar(entrada.TipoRefeicao, TiposValidos);

        if (dia is null) return BadRequest(new { erro = "dia_invalido", aceitos = DiasValidos });
        if (tipo is null) return BadRequest(new { erro = "tipo_invalido", aceitos = TiposValidos });

        var nome = (entrada.NomePrato ?? string.Empty).Trim();
        if (nome.Length < 2 || nome.Length > 200)
            return BadRequest(new { erro = "nome_prato_invalido" });

        if (entrada.Calorias is < 0 or > 5000)
            return BadRequest(new { erro = "calorias_invalidas" });

        if (entrada.Proteinas is < 0 or > 500 || entrada.Carboidratos is < 0 or > 500
            || entrada.Gorduras is < 0 or > 500)
            return BadRequest(new { erro = "macros_invalidos" });

        var refeicao = new RefeicaoAgendada
        {
            UsuarioId = UsuarioId,
            DiaSemana = dia,
            TipoRefeicao = tipo,
            NomePrato = nome,
            Ingredientes = (entrada.Ingredientes ?? string.Empty).Trim(),
            ModoPreparo = (entrada.ModoPreparo ?? string.Empty).Trim(),
            Calorias = entrada.Calorias ?? 0,
            Proteinas = entrada.Proteinas ?? 0,
            Carboidratos = entrada.Carboidratos ?? 0,
            Gorduras = entrada.Gorduras ?? 0,
            DataCriacao = DateTime.UtcNow,
        };

        _db.RefeicoesAgendadas.Add(refeicao);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("refeicao_criada", UsuarioId, "RefeicaoAgendada",
            refeicao.Id.ToString(), $"{{\"dia\":\"{dia}\",\"tipo\":\"{tipo}\"}}",
            ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return CreatedAtAction(nameof(Listar), new { id = refeicao.Id }, new RefeicaoSaida(
            refeicao.Id, refeicao.DiaSemana, refeicao.TipoRefeicao, refeicao.NomePrato,
            refeicao.Ingredientes, refeicao.ModoPreparo, refeicao.Calorias,
            refeicao.Proteinas, refeicao.Carboidratos, refeicao.Gorduras));
    }

    // -----------------------------------------------------------------------
    /// <summary>Edita uma refeição planejada. Mantém o CRUD que a v1 tinha.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] RefeicaoEntrada entrada, CancellationToken ct)
    {
        var dia = Normalizar(entrada.DiaSemana, DiasValidos);
        var tipo = Normalizar(entrada.TipoRefeicao, TiposValidos);

        if (dia is null) return BadRequest(new { erro = "dia_invalido", aceitos = DiasValidos });
        if (tipo is null) return BadRequest(new { erro = "tipo_invalido", aceitos = TiposValidos });

        var nome = (entrada.NomePrato ?? string.Empty).Trim();
        if (nome.Length < 2 || nome.Length > 200)
            return BadRequest(new { erro = "nome_prato_invalido" });

        if (entrada.Calorias is < 0 or > 5000)
            return BadRequest(new { erro = "calorias_invalidas" });

        if (entrada.Proteinas is < 0 or > 500 || entrada.Carboidratos is < 0 or > 500
            || entrada.Gorduras is < 0 or > 500)
            return BadRequest(new { erro = "macros_invalidos" });

        var refeicao = await _db.RefeicoesAgendadas
            .SingleOrDefaultAsync(r => r.Id == id && r.UsuarioId == UsuarioId, ct);

        if (refeicao is null)
            return NotFound(new { erro = "refeicao_inexistente" });

        refeicao.DiaSemana = dia;
        refeicao.TipoRefeicao = tipo;
        refeicao.NomePrato = nome;
        refeicao.Ingredientes = (entrada.Ingredientes ?? string.Empty).Trim();
        refeicao.ModoPreparo = (entrada.ModoPreparo ?? string.Empty).Trim();
        refeicao.Calorias = entrada.Calorias ?? 0;
        refeicao.Proteinas = entrada.Proteinas ?? 0;
        refeicao.Carboidratos = entrada.Carboidratos ?? 0;
        refeicao.Gorduras = entrada.Gorduras ?? 0;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("refeicao_atualizada", UsuarioId, "RefeicaoAgendada",
            refeicao.Id.ToString(), $"{{\"dia\":\"{dia}\",\"tipo\":\"{tipo}\"}}",
            ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new RefeicaoSaida(
            refeicao.Id, refeicao.DiaSemana, refeicao.TipoRefeicao, refeicao.NomePrato,
            refeicao.Ingredientes, refeicao.ModoPreparo, refeicao.Calorias,
            refeicao.Proteinas, refeicao.Carboidratos, refeicao.Gorduras));
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    /// <summary>Marca ou desmarca uma refeicao planejada como cumprida.</summary>
    [HttpPut("{id:int}/concluir")]
    public async Task<IActionResult> Concluir(int id, [FromBody] ConcluirEntrada entrada, CancellationToken ct)
    {
        var refeicao = await _db.RefeicoesAgendadas
            .SingleOrDefaultAsync(r => r.Id == id && r.UsuarioId == UsuarioId, ct);

        if (refeicao is null)
            return NotFound(new { erro = "refeicao_inexistente" });

        refeicao.Concluida = entrada.Concluida;
        refeicao.ConcluidaEm = entrada.Concluida ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(ct);

        return Ok(new { id, concluida = refeicao.Concluida, concluidaEm = refeicao.ConcluidaEm });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        // Filtro por UsuarioId junto com o Id: um id de outra pessoa vira 404.
        var refeicao = await _db.RefeicoesAgendadas
            .SingleOrDefaultAsync(r => r.Id == id && r.UsuarioId == UsuarioId, ct);

        if (refeicao is null)
            return NotFound(new { erro = "refeicao_inexistente" });

        _db.RefeicoesAgendadas.Remove(refeicao);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("refeicao_removida", UsuarioId, "RefeicaoAgendada",
            id.ToString(), null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return NoContent();
    }

    // -----------------------------------------------------------------------
    private static string? Normalizar(string? valor, string[] aceitos)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        // Remove acentos para comparar ("Terc,a" -> "Terca").
        var semAcento = new string(valor.Trim().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();

        return aceitos.FirstOrDefault(a => a.ToLowerInvariant() == semAcento);
    }
}

public sealed record ConcluirEntrada(bool Concluida);
