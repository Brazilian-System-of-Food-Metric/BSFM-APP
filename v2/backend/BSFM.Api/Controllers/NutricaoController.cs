using BSFM.Nutricao;
using BSFM.Nutricao.Visao;
using Microsoft.AspNetCore.Mvc;

namespace BSFM.Api.Controllers;

/// <summary>
/// Consulta nutricional e reconhecimento por imagem.
///
/// Rota pública de propósito: não expõe dado pessoal — é catálogo. O que é
/// pessoal (registrar a análise no histórico) continua em /api/eu/analises,
/// com sessão e assinatura.
///
/// Fluxo esperado no app:
///   1. usuário fotografa ou digita o alimento;
///   2. /api/nutricao/por-imagem ou /por-nome devolve nutrientes + candidatos;
///   3. usuário confirma;
///   4. app grava em POST /api/eu/analises.
///
/// Separar "descobrir" de "gravar" evita que um erro do modelo vire histórico
/// do usuário sem confirmação.
/// </summary>
[ApiController]
[Route("api/nutricao")]
public sealed class NutricaoController : ControllerBase
{
    private const int LimiteImagemBytes = 6 * 1024 * 1024;

    private readonly IVisaoAlimento _visao;
    private readonly ICatalogoNutricional _catalogo;
    private readonly ILogger<NutricaoController> _log;

    public NutricaoController(IVisaoAlimento visao, ICatalogoNutricional catalogo, ILogger<NutricaoController> log)
    {
        _visao = visao;
        _catalogo = catalogo;
        _log = log;
    }

    // -----------------------------------------------------------------------
    [HttpGet("alimentos")]
    public IActionResult Buscar([FromQuery] string? q, [FromQuery] int limite = 8)
    {
        var resultados = BuscaAlimentos.Buscar(q, _catalogo.Alimentos, limite);

        return Ok(new
        {
            consulta = q,
            total = resultados.Count,
            itens = resultados.Select(r => new
            {
                nome = r.Alimento.Nome,
                grupo = r.Alimento.Grupo,
                pontuacao = r.Pontuacao,
                por100g = new
                {
                    kcal = r.Alimento.Por100g.Kcal,
                    proteina = r.Alimento.Por100g.Proteina,
                    carboidrato = r.Alimento.Por100g.Carboidrato,
                    gordura = r.Alimento.Por100g.Gordura,
                    fibra = r.Alimento.Por100g.Fibra,
                },
            }),
        });
    }

    // -----------------------------------------------------------------------
    [HttpPost("por-nome")]
    public IActionResult PorNome([FromBody] PorNomeEntrada entrada)
    {
        var resultado = AnalisadorAlimento.PorNome(entrada.Nome, entrada.Gramas ?? AnalisadorAlimento.GramasPadrao, _catalogo.Alimentos);
        return Responder(resultado);
    }

    // -----------------------------------------------------------------------
    [HttpPost("por-imagem")]
    public async Task<IActionResult> PorImagem([FromBody] PorImagemEntrada entrada, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entrada.ImagemBase64))
            return BadRequest(new { erro = "imagem_ausente" });

        byte[] imagem;
        try
        {
            imagem = Convert.FromBase64String(entrada.ImagemBase64);
        }
        catch (FormatException)
        {
            return BadRequest(new { erro = "imagem_invalida", mensagem = "esperado base64" });
        }

        if (imagem.Length == 0)
            return BadRequest(new { erro = "imagem_vazia" });

        if (imagem.Length > LimiteImagemBytes)
            return StatusCode(413, new { erro = "imagem_acima_do_limite", bytes = imagem.Length });

        var resultado = await AnalisadorAlimento.PorImagemAsync(
            imagem, _visao, entrada.Dica,
            entrada.Gramas ?? AnalisadorAlimento.GramasPadrao, _catalogo.Alimentos, ct);

        if (!resultado.Identificado)
        {
            // Provedor não configurado não é erro do cliente: 503 com motivo.
            var status = resultado.Motivo == "provedor_de_visao_indisponivel"
                || resultado.Motivo == "provedor_de_visao_ausente"
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status422UnprocessableEntity;

            return StatusCode(status, new
            {
                identificado = false,
                motivo = resultado.Motivo,
                origem = resultado.Origem,
                candidatos = resultado.Candidatos.Select(MapearCandidato),
            });
        }

        return Ok(Mapear(resultado));
    }

    // -----------------------------------------------------------------------
    [HttpGet("provedor")]
    public IActionResult Provedor()
        => Ok(new
        {
            id = _visao.Identificacao,
            disponivel = _visao.Disponivel,
            catalogo = new { origem = _catalogo.Origem, alimentos = _catalogo.Alimentos.Count },
            mensagem = _visao.Disponivel
                ? "Reconhecimento por imagem ativo."
                : "Reconhecimento por imagem desligado: configure BSFM_GEMINI_API_KEY. A busca por nome continua funcionando.",
        });

    // -----------------------------------------------------------------------
    private IActionResult Responder(ResultadoAnalise resultado)
    {
        if (!resultado.Identificado)
            return UnprocessableEntity(new
            {
                identificado = false,
                motivo = resultado.Motivo,
                candidatos = resultado.Candidatos.Select(MapearCandidato),
            });

        return Ok(Mapear(resultado));
    }

    private static object Mapear(ResultadoAnalise r) => new
    {
        identificado = r.Identificado,
        nomeReconhecido = r.NomeReconhecido,
        origem = r.Origem,
        confianca = r.Confianca,
        alimento = r.Alimento is null ? null : new
        {
            nome = r.Alimento.Nome,
            grupo = r.Alimento.Grupo,
            pontosBusca = r.PontuacaoBusca,
        },
        gramas = r.Gramas,
        porcao = r.NaPorcao is null ? null : new
        {
            kcal = r.NaPorcao.Kcal,
            proteina = r.NaPorcao.Proteina,
            carboidrato = r.NaPorcao.Carboidrato,
            gordura = r.NaPorcao.Gordura,
            fibra = r.NaPorcao.Fibra,
        },
        candidatos = r.Candidatos.Select(MapearCandidato),
    };

    private static object MapearCandidato(ResultadoBusca c) => new
    {
        nome = c.Alimento.Nome,
        grupo = c.Alimento.Grupo,
        pontuacao = c.Pontuacao,
        kcalPor100g = c.Alimento.Por100g.Kcal,
    };
}

public sealed record PorNomeEntrada(string? Nome, double? Gramas);

public sealed record PorImagemEntrada(string? ImagemBase64, string? Dica, double? Gramas);
