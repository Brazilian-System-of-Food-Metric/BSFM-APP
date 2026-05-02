using Microsoft.AspNetCore.Mvc;
using PonteBanco;
using ClassesBSFM;
using Microsoft.EntityFrameworkCore;

namespace BSFM.CoreAnalytics.Backend.Controllers
{
    /// <summary>
    /// Controller para gerenciar dados de saúde do usuário.
    /// 
    /// Endpoints:
    /// - GET  /api/Usuario/{id}          → Retorna dados do usuário (para verificar pendências)
    /// - PUT  /api/Usuario/atualizar-saude → Atualiza Intolerancia e Diabetes
    /// 
    /// Fluxo:
    /// 1. Frontend verifica se usuário tem dados pendentes (GET)
    /// 2. Se sim, exibe alerta amarelo com botão "Preencher Agora"
    /// 3. Usuário preenche intolerâncias e diabetes no modal
    /// 4. Frontend salva via PUT
    /// 5. ContextInjector passa esses dados para o Groq nas análises
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UsuarioController : ControllerBase
    {
        private readonly PonteDB _db;
        private readonly ILogger<UsuarioController> _logger;

        public UsuarioController(PonteDB db, ILogger<UsuarioController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Retorna os dados do usuário pelo ID.
        /// Usado pelo frontend para verificar se Intolerancia e Diabetes estão preenchidos.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUsuario(int id)
        {
            var user = await _db.Usuarios.FindAsync(id);
            if (user == null)
                return NotFound(new { mensagem = "Usuário não encontrado." });

            return Ok(new
            {
                user.ID,
                user.Nome,
                user.Email,
                user.Intolerancia,
                user.Diabetes,
                user.IMC,
                user.Peso,
                user.Altura,
                user.PesoMeta
            });
        }

        /// <summary>
        /// Atualiza os dados de saúde do usuário (Intolerancia e Diabetes).
        /// Chamado pelo frontend quando o usuário preenche o modal de saúde.
        /// </summary>
        [HttpPut("atualizar-saude")]
        public async Task<IActionResult> AtualizarSaude(
            [FromBody] AtualizarSaudeRequest request)
        {
            if (request.UsuarioId <= 0)
                return BadRequest(new { mensagem = "ID do usuário inválido." });

            var user = await _db.Usuarios.FindAsync(request.UsuarioId);
            if (user == null)
                return NotFound(new { mensagem = "Usuário não encontrado." });

            // Validações
            if (string.IsNullOrWhiteSpace(request.Intolerancia))
                return BadRequest(new { mensagem = "O campo Intolerancia é obrigatório. Digite 'Nenhuma' se não tiver." });

            if (string.IsNullOrWhiteSpace(request.Diabetes))
                return BadRequest(new { mensagem = "O campo Diabetes é obrigatório." });

            // Atualiza os campos
            user.Intolerancia = request.Intolerancia.Trim();
            user.Diabetes = request.Diabetes.Trim();

            _logger.LogInformation(
                "[SAUDE] Usuário {UsuarioId} atualizou dados de saúde. " +
                "Intolerancia: '{Intolerancia}', Diabetes: '{Diabetes}'",
                request.UsuarioId, user.Intolerancia, user.Diabetes);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                mensagem = "Dados de saúde atualizados com sucesso!",
                intolerancia = user.Intolerancia,
                diabetes = user.Diabetes
            });
        }
    }

    /// <summary>
    /// Modelo de requisição para atualizar dados de saúde
    /// </summary>
    public class AtualizarSaudeRequest
    {
        public int UsuarioId { get; set; }
        public string Intolerancia { get; set; } = string.Empty;
        public string Diabetes { get; set; } = string.Empty;
    }
}
