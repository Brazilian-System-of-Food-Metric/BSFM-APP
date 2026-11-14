using BSFM.Api.DTOs;
using BSFM.Api.Filtros;
using BSFM.Api.Servicos;
using BSFM.Dados;
using BSFM.Dados.Entidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Controllers;

/// <summary>
/// Histórico de análises de alimento. Substitui `/historico-analises/{usuarioId}`.
///
/// A inferência (YOLO) permanece no app legado até a S4: aqui a análise é
/// **registrada** com os campos de rastreabilidade (confiança, versão do modelo,
/// hash da imagem), que é o que faltava para reproduzir um resultado antigo.
/// </summary>
[ApiController]
[Route("api/eu/analises")]
[SessaoObrigatoria]
public sealed class AnalisesController : ControllerBase
{
    private readonly BsfmDbContext _db;
    private readonly ServicoAuditoria _auditoria;
    private readonly BSFM.Nutricao.ICatalogoNutricional _catalogo;

    public AnalisesController(BsfmDbContext db, ServicoAuditoria auditoria, BSFM.Nutricao.ICatalogoNutricional catalogo)
    {
        _db = db;
        _auditoria = auditoria;
        _catalogo = catalogo;
    }

    private int UsuarioId => HttpContext.UsuarioId();

    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int limite = 30, CancellationToken ct = default)
    {
        limite = Math.Clamp(limite, 1, 200);

        var itens = await _db.AnalisesIA.AsNoTracking()
            .Where(a => a.UsuarioID == UsuarioId)
            .OrderByDescending(a => a.DataAnalise)
            .Take(limite)
            .Select(a => new AnaliseSaida(
                a.ID, a.Alimento, a.Calorias, a.Proteinas, a.Carbos, a.Gorduras,
                a.Porcao, a.DataAnalise, a.PodeConsumir, a.PontuacaoSaude,
                a.AnaliseEmRelacaoAMeta, a.DicaBSFM, a.Confianca, a.ModeloVersao, a.Origem))
            .ToListAsync(ct);

        return Ok(itens);
    }

    // -----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] AnaliseEntrada entrada, CancellationToken ct)
    {
        var nome = (entrada.Alimento ?? string.Empty).Trim();
        if (nome.Length < 2 || nome.Length > 200)
            return BadRequest(new { erro = "alimento_invalido" });

        if (entrada.Calorias is < 0 or > 5000)
            return BadRequest(new { erro = "calorias_invalidas" });

        if (entrada.Proteinas is < 0 or > 500 || entrada.Carbos is < 0 or > 500
            || entrada.Gorduras is < 0 or > 500)
            return BadRequest(new { erro = "macros_invalidos" });

        if (entrada.Confianca is < 0 or > 1)
            return BadRequest(new { erro = "confianca_invalida" });

        if (entrada.PontuacaoSaude is < 0 or > 10)
            return BadRequest(new { erro = "pontuacao_invalida" });

        // Confiança baixa não vira registro silencioso: o cliente precisa saber
        // que o resultado é duvidoso. Abaixo de 0,45 recusamos por padrão.
        const double confiancaMinima = 0.45;
        if (entrada.Confianca is not null && entrada.Confianca < confiancaMinima
            && entrada.Origem is "foto" or null)
        {
            return UnprocessableEntity(new
            {
                erro = "confianca_insuficiente",
                confianca = entrada.Confianca,
                minimo = confiancaMinima,
                mensagem = "Análise descartada por baixa confiança. Registre manualmente ou reenvie a foto.",
            });
        }

        var analise = new AnaliseIA
        {
            UsuarioID = UsuarioId,
            Alimento = nome,
            Calorias = entrada.Calorias ?? 0,
            Proteinas = entrada.Proteinas ?? 0,
            Carbos = entrada.Carbos ?? 0,
            Gorduras = entrada.Gorduras ?? 0,
            Porcao = (entrada.Porcao ?? "100g").Trim(),
            DataAnalise = DateTime.UtcNow,
            PodeConsumir = entrada.PodeConsumir,
            PontuacaoSaude = entrada.PontuacaoSaude ?? 0,
            AnaliseEmRelacaoAMeta = entrada.AnaliseEmRelacaoAMeta ?? string.Empty,
            DicaBSFM = entrada.DicaBSFM ?? string.Empty,
            Confianca = entrada.Confianca,
            ModeloVersao = entrada.ModeloVersao,
            ImagemHash = entrada.ImagemHash,
            Origem = string.IsNullOrWhiteSpace(entrada.Origem) ? "foto" : entrada.Origem!,
        };

        _db.AnalisesIA.Add(analise);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("analise_registrada", UsuarioId, "AnaliseIA",
            analise.ID.ToString(),
            $"{{\"alimento\":\"{analise.Alimento}\",\"origem\":\"{analise.Origem}\"}}",
            ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return CreatedAtAction(nameof(Listar), new { id = analise.ID }, new AnaliseSaida(
            analise.ID, analise.Alimento, analise.Calorias, analise.Proteinas, analise.Carbos,
            analise.Gorduras, analise.Porcao, analise.DataAnalise, analise.PodeConsumir,
            analise.PontuacaoSaude, analise.AnaliseEmRelacaoAMeta, analise.DicaBSFM,
            analise.Confianca, analise.ModeloVersao, analise.Origem));
    }

    // -----------------------------------------------------------------------
    /// <summary>
    /// Registra uma análise resolvendo o alimento pelo catálogo local: o cliente
    /// informa o nome e a porção, o servidor calcula os nutrientes.
    ///
    /// É o caminho preferido. O <c>POST /api/eu/analises</c> continua existindo
    /// para produto embalado (rótulo) e ajuste manual, onde o número vem de fora.
    /// </summary>
    [HttpPost("do-catalogo")]
    public async Task<IActionResult> RegistrarDoCatalogo([FromBody] AnaliseDoCatalogoEntrada entrada, CancellationToken ct)
    {
        var resultado = BSFM.Nutricao.AnalisadorAlimento.PorNome(entrada.Nome, entrada.Gramas ?? 100, _catalogo.Alimentos);

        if (!resultado.Identificado)
            return UnprocessableEntity(new
            {
                erro = resultado.Motivo,
                candidatos = resultado.Candidatos.Select(c => new
                {
                    nome = c.Alimento.Nome,
                    grupo = c.Alimento.Grupo,
                    pontuacao = c.Pontuacao,
                }),
            });

        var analise = new AnaliseIA
        {
            UsuarioID = UsuarioId,
            Alimento = resultado.Alimento!.Nome,
            Calorias = resultado.NaPorcao!.Kcal,
            Proteinas = resultado.NaPorcao.Proteina,
            Carbos = resultado.NaPorcao.Carboidrato,
            Gorduras = resultado.NaPorcao.Gordura,
            Porcao = $"{resultado.Gramas:F0}g",
            DataAnalise = DateTime.UtcNow,
            Confianca = resultado.PontuacaoBusca,
            ModeloVersao = "catalogo-taco",
            Origem = string.IsNullOrWhiteSpace(entrada.Origem) ? "catalogo" : entrada.Origem!,
        };

        _db.AnalisesIA.Add(analise);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("analise_do_catalogo", UsuarioId, "AnaliseIA",
            analise.ID.ToString(),
            $"{{\"alimento\":\"{analise.Alimento}\",\"gramas\":{resultado.Gramas:F0}}}",
            ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return CreatedAtAction(nameof(Listar), new { id = analise.ID }, new AnaliseSaida(
            analise.ID, analise.Alimento, analise.Calorias, analise.Proteinas, analise.Carbos,
            analise.Gorduras, analise.Porcao, analise.DataAnalise, analise.PodeConsumir,
            analise.PontuacaoSaude, analise.AnaliseEmRelacaoAMeta, analise.DicaBSFM,
            analise.Confianca, analise.ModeloVersao, analise.Origem));
    }

    // -----------------------------------------------------------------------
    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] string? data, CancellationToken ct)
    {
        var dia = DateOnly.TryParse(data, out var informado)
            ? informado
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var inicio = dia.ToDateTime(TimeOnly.MinValue).Date;
        var fim = inicio.AddDays(1);

        var doDia = await _db.AnalisesIA.AsNoTracking()
            .Where(a => a.UsuarioID == UsuarioId && a.DataAnalise >= inicio && a.DataAnalise < fim)
            .ToListAsync(ct);

        return Ok(new ResumoDiaSaida(
            dia, doDia.Count,
            Math.Round(doDia.Sum(a => a.Calorias), 1),
            Math.Round(doDia.Sum(a => a.Proteinas), 1),
            Math.Round(doDia.Sum(a => a.Carbos), 1),
            Math.Round(doDia.Sum(a => a.Gorduras), 1)));
    }

    // -----------------------------------------------------------------------
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        var analise = await _db.AnalisesIA
            .SingleOrDefaultAsync(a => a.ID == id && a.UsuarioID == UsuarioId, ct);

        if (analise is null)
            return NotFound(new { erro = "analise_inexistente" });

        _db.AnalisesIA.Remove(analise);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("analise_removida", UsuarioId, "AnaliseIA", id.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return NoContent();
    }
}

/// <summary>
/// Evolução corporal ao longo do tempo. Substitui `GET /evolucao/{usuarioId}`.
/// </summary>
[ApiController]
[Route("api/eu/evolucao")]
[SessaoObrigatoria]
public sealed class EvolucaoController : ControllerBase
{
    private readonly BsfmDbContext _db;

    public EvolucaoController(BsfmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Obter([FromQuery] int limite = 90, CancellationToken ct = default)
    {
        limite = Math.Clamp(limite, 2, 730);

        var usuario = await _db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.ID == HttpContext.UsuarioId(), ct);

        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        var pontos = await _db.Historicos.AsNoTracking()
            .Where(h => h.UsuarioID == usuario.ID)
            .OrderBy(h => h.DataRegistro)
            .Take(limite)
            .Select(h => new { h.DataRegistro, h.Peso, h.IMC })
            .ToListAsync(ct);

        var serie = pontos.Select(p => new PontoEvolucao(p.DataRegistro, p.Peso, p.IMC)).ToList();

        // Sem histórico, a série começa no estado atual — nunca inventamos pontos.
        if (serie.Count == 0)
            serie.Add(new PontoEvolucao(DateTime.UtcNow, usuario.Peso, usuario.IMC));

        var primeiro = serie[0];
        var ultimo = serie[^1];

        return Ok(new EvolucaoSaida(
            primeiro.Peso,
            ultimo.Peso,
            Math.Round(ultimo.Peso - primeiro.Peso, 2),
            primeiro.Imc,
            ultimo.Imc,
            Math.Round(ultimo.Imc - primeiro.Imc, 2),
            usuario.PesoMeta,
            Math.Round(ultimo.Peso - usuario.PesoMeta, 2),
            serie));
    }
}
