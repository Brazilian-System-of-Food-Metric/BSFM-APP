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
    // Contexto do feedback: meta ativa, consumo do dia e perfil de saúde.
    // Vem do banco, nunca do corpo da requisição — é o "injetor de contexto"
    // que a v1 tinha no servidor e que faltava aqui.
    private async Task<(Usuario? Usuario, double MetaKcal, double ConsumidoKcal)> ContextoAsync(CancellationToken ct)
    {
        var usuario = await _db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);

        if (usuario is null)
            return (null, 0, 0);

        var meta = await _db.Metas.AsNoTracking()
            .Where(m => m.UsuarioId == UsuarioId && m.Ativa)
            .OrderByDescending(m => m.CriadoEm)
            .Select(m => (double?)m.CaloriasAlvo)
            .FirstOrDefaultAsync(ct);

        var inicio = DateTime.UtcNow.Date;
        var consumido = await _db.AnalisesIA.AsNoTracking()
            .Where(a => a.UsuarioID == UsuarioId && a.DataAnalise >= inicio)
            .SumAsync(a => (double?)a.Calorias, ct) ?? 0;

        var metaKcal = meta is > 0
            ? meta.Value
            : (usuario.GastoTotal > 0 ? usuario.GastoTotal : 2000);

        return (usuario, metaKcal, consumido);
    }

    private static BSFM.Nutricao.AnalisadorRefeicao.Resultado Avaliar(
        Usuario usuario, double metaKcal, double consumido,
        double kcal, double proteina, double carbo, double gordura, double? fibra)
        => BSFM.Nutricao.AnalisadorRefeicao.Avaliar(
            new BSFM.Nutricao.AnalisadorRefeicao.Entrada(
                kcal, proteina, carbo, gordura, fibra, metaKcal, consumido,
                usuario.Diabetes, usuario.Intolerancia));

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

        var contexto = await ContextoAsync(ct);
        if (contexto.Usuario is null)
            return NotFound(new { erro = "usuario_inexistente" });

        var avaliacao = Avaliar(contexto.Usuario, contexto.MetaKcal, contexto.ConsumidoKcal,
            entrada.Calorias ?? 0, entrada.Proteinas ?? 0, entrada.Carbos ?? 0,
            entrada.Gorduras ?? 0, entrada.Fibra);

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
            // Pontuação é do servidor: o cliente não decide a própria nota.
            PodeConsumir = avaliacao.Classificacao != "evitar",
            PontuacaoSaude = avaliacao.Pontuacao,
            AnaliseEmRelacaoAMeta = avaliacao.Mensagem,
            DicaBSFM = avaliacao.Dicas.FirstOrDefault() ?? string.Empty,
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

        var contexto = await ContextoAsync(ct);
        if (contexto.Usuario is null)
            return NotFound(new { erro = "usuario_inexistente" });

        var avaliacao = Avaliar(contexto.Usuario, contexto.MetaKcal, contexto.ConsumidoKcal,
            resultado.NaPorcao!.Kcal, resultado.NaPorcao.Proteina, resultado.NaPorcao.Carboidrato,
            resultado.NaPorcao.Gordura, resultado.NaPorcao.Fibra);

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
            PodeConsumir = avaliacao.Classificacao != "evitar",
            PontuacaoSaude = avaliacao.Pontuacao,
            AnaliseEmRelacaoAMeta = avaliacao.Mensagem,
            DicaBSFM = avaliacao.Dicas.FirstOrDefault() ?? string.Empty,
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
    /// <summary>
    /// Avalia uma refeição com o contexto de quem está logado — meta calórica,
    /// consumo do dia, diabetes e intolerância — sem gravar nada.
    ///
    /// É o mesmo motor usado ao registrar, exposto para o preview da análise e
    /// para reavaliar itens antigos do diário. O cliente manda só os macros;
    /// o contexto é lido do banco.
    /// </summary>
    [HttpPost("avaliar")]
    public async Task<IActionResult> AvaliarRefeicao([FromBody] AvaliacaoEntrada entrada, CancellationToken ct)
    {
        if (entrada.Kcal is < 0 or > 5000
            || entrada.Proteinas is < 0 or > 500
            || entrada.Carbos is < 0 or > 500
            || entrada.Gorduras is < 0 or > 500
            || entrada.Fibra is < 0 or > 500)
            return BadRequest(new { erro = "valores_invalidos" });

        var contexto = await ContextoAsync(ct);
        if (contexto.Usuario is null)
            return NotFound(new { erro = "usuario_inexistente" });

        var avaliacao = Avaliar(contexto.Usuario, contexto.MetaKcal, contexto.ConsumidoKcal,
            entrada.Kcal ?? 0, entrada.Proteinas ?? 0, entrada.Carbos ?? 0,
            entrada.Gorduras ?? 0, entrada.Fibra);

        return Ok(new
        {
            classificacao = avaliacao.Classificacao,
            pontuacao = avaliacao.Pontuacao,
            mensagem = avaliacao.Mensagem,
            dicas = avaliacao.Dicas,
            podeConsumir = avaliacao.Classificacao != "evitar",
            metaKcal = Math.Round(contexto.MetaKcal),
            consumidoKcal = Math.Round(contexto.ConsumidoKcal),
            perfil = new
            {
                diabetes = contexto.Usuario.Diabetes,
                intolerancia = contexto.Usuario.Intolerancia,
            },
        });
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
