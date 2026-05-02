using Microsoft.AspNetCore.Mvc;
using BSFM.CoreAnalytics.Backend.Services;
using PonteBanco;
using ClassesBSFM;

namespace BSFM.CoreAnalytics.Backend.Controllers
{
    /// <summary>
    /// RotuloController
    /// 
    /// Endpoint para análise de rótulos nutricionais.
    /// Recebe o texto OCR do frontend (processado pelo Tesseract.js no navegador)
    /// e envia para o Groq Llama 3 com o contexto personalizado do usuário.
    /// 
    /// POST /api/rotulo/analisar
    /// Body: { usuarioId: int, textoOcr: string }
    /// Response: { ProdutoDetectado, PodeConsumir, PontuacaoSaude, AnaliseEmRelacaoAMeta, DicaBSFM }
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RotuloController : ControllerBase
    {
        private readonly ContextInjectorService _contextInjector;
        private readonly NutriBrainService _nutriBrain;
        private readonly PonteDB _db;
        private readonly ILogger<RotuloController> _logger;

        public RotuloController(
            ContextInjectorService contextInjector,
            NutriBrainService nutriBrain,
            PonteDB db,
            ILogger<RotuloController> logger)
        {
            _contextInjector = contextInjector;
            _nutriBrain = nutriBrain;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/rotulo/analisar
        /// 
        /// Fluxo:
        /// 1. Recebe texto OCR do frontend (processado pelo Tesseract.js no navegador)
        /// 2. Busca contexto do usuário no PostgreSQL (IMC, TMB, metas, histórico, intolerâncias, diabetes)
        /// 3. Monta SystemPrompt personalizado
        /// 4. Envia para o Groq Llama 3
        /// 5. Retorna JSON estruturado com análise
        /// 6. Salva análise no histórico do usuário
        /// </summary>
        [HttpPost("analisar")]
        public async Task<IActionResult> AnalisarRotulo([FromBody] AnalisarRotuloRequest request)
        {
            // Validações
            if (request.UsuarioId <= 0)
                return BadRequest(new { mensagem = "ID do usuário inválido." });

            if (string.IsNullOrWhiteSpace(request.TextoOcr))
                return BadRequest(new { mensagem = "Texto OCR não pode estar vazio. Capture a foto do rótulo primeiro." });

            if (request.TextoOcr.Length < 10)
                return BadRequest(new { mensagem = "Texto OCR muito curto. Tente uma foto mais nítida da tabela nutricional." });

            try
            {
                _logger.LogInformation("[ROTULO] Iniciando análise para usuário {UsuarioId}", request.UsuarioId);

                // 1. Busca contexto do usuário
                var userContext = await _contextInjector.BuildContextAsync(request.UsuarioId);

                // 2. Monta SystemPrompt personalizado
                var systemPrompt = _contextInjector.BuildSystemPrompt(userContext);

                // 3. Envia para o Groq
                var resultado = await _nutriBrain.AnalisarRotuloAsync(request.TextoOcr, systemPrompt);

                // 4. Salva no histórico com os macros extraídos pelo Groq
                try
                {
                    var analise = new AnaliseIA
                    {
                        UsuarioID = request.UsuarioId,
                        Alimento = resultado.ProdutoDetectado ?? "Rótulo escaneado",
                        Porcao = "N/A",
                        Calorias = Math.Round(resultado.Calorias, 2),
                        Proteinas = Math.Round(resultado.Proteinas, 2),
                        Carbos = Math.Round(resultado.Carboidratos, 2),
                        Gorduras = Math.Round(resultado.Gorduras, 2),
                        DataAnalise = DateTime.Now,
                        PodeConsumir = resultado.PodeConsumir,
                        PontuacaoSaude = resultado.PontuacaoSaude,
                        AnaliseEmRelacaoAMeta = resultado.AnaliseEmRelacaoAMeta ?? "",
                        DicaBSFM = resultado.DicaBSFM ?? ""
                    };

                    _db.AnalisesIA.Add(analise);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("[ROTULO] Análise salva no histórico com ID {AnaliseId} - Calorias={Calorias}, Carbos={Carbos}, Proteinas={Proteinas}, Gorduras={Gorduras}", 
                        analise.ID, analise.Calorias, analise.Carbos, analise.Proteinas, analise.Gorduras);
                }
                catch (Exception ex)
                {
                    // Não falha a requisição se o salvamento falhar
                    _logger.LogWarning(ex, "[ROTULO] Não foi possível salvar análise no histórico");
                }

                _logger.LogInformation("[ROTULO] Análise concluída: {Produto} - Score: {Score} - Calorias: {Calorias}kcal", 
                    resultado.ProdutoDetectado, resultado.PontuacaoSaude, resultado.Calorias);

                return Ok(new
                {
                    produtoDetectado = resultado.ProdutoDetectado,
                    podeConsumir = resultado.PodeConsumir,
                    pontuacaoSaude = resultado.PontuacaoSaude,
                    analiseEmRelacaoAMeta = resultado.AnaliseEmRelacaoAMeta,
                    dicaBSFM = resultado.DicaBSFM,
                    calorias = resultado.Calorias,
                    carboidratos = resultado.Carboidratos,
                    proteinas = resultado.Proteinas,
                    gorduras = resultado.Gorduras,
                    sodio = resultado.Sodio,
                    acucar = resultado.Acucar
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "[ROTULO] Usuário não encontrado: {UsuarioId}", request.UsuarioId);
                return NotFound(new { mensagem = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[ROTULO] Erro de comunicação com Groq");
                return StatusCode(502, new { mensagem = "Serviço de IA temporariamente indisponível. Tente novamente em alguns segundos." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ROTULO] Erro interno ao analisar rótulo");
                return StatusCode(500, new { mensagem = "Erro interno ao analisar o rótulo. Tente novamente." });
            }
        }
    }

    /// <summary>
    /// Modelo de requisição para análise de rótulo
    /// </summary>
    public class AnalisarRotuloRequest
    {
        public int UsuarioId { get; set; }
        public string TextoOcr { get; set; } = string.Empty;
    }
}
