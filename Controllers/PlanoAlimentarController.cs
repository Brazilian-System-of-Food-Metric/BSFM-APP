using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BSFM.Models;
using BSFM.Services;
using PonteBanco;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace BSFM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PlanoAlimentarController : ControllerBase
    {
        private readonly PonteDB _db;
        private readonly OcrNutricionalService _ocrService;
        private readonly ILogger<PlanoAlimentarController> _logger;

        public PlanoAlimentarController(PonteDB db, OcrNutricionalService ocrService, ILogger<PlanoAlimentarController> logger)
        {
            _db = db;
            _ocrService = ocrService;
            _logger = logger;
        }

        /// <summary>
        /// Importa uma prescrição nutricional via OCR e cria o cronograma semanal
        /// </summary>
        [HttpPost("importar-prescricao")]
        public async Task<IActionResult> ImportarPrescricao([FromBody] ImportarPrescricaoRequest request)
        {
            try
            {
                // Obtém o ID do usuário autenticado (simulação - em produção usaria Claims)
                var usuarioId = ObterUsuarioId();

                if (usuarioId <= 0)
                {
                    return Unauthorized(new { mensagem = "Usuário não autenticado" });
                }

                // Validação dos dados
                if (string.IsNullOrEmpty(request.ImagemBase64))
                {
                    return BadRequest(new { mensagem = "Imagem é obrigatória" });
                }

                if (string.IsNullOrEmpty(request.NomePlano))
                {
                    return BadRequest(new { mensagem = "Nome do plano é obrigatório" });
                }

                // Processa a prescrição via OCR
                var resultado = await _ocrService.ProcessarPrescricaoNutricionalAsync(
                    request.ImagemBase64,
                    request.NomePlano,
                    request.Observacoes,
                    request.DataInicio,
                    usuarioId
                );

                if (resultado.Sucesso)
                {
                    return Ok(new
                    {
                        sucesso = true,
                        mensagem = resultado.Mensagem,
                        cronogramaId = resultado.CronogramaId,
                        totalRefeicoes = resultado.TotalRefeicoes
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = resultado.Mensagem
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao importar prescrição nutricional");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro interno do servidor ao processar a prescrição"
                });
            }
        }

        /// <summary>
        /// Obtém o cronograma semanal ativo de um usuário
        /// </summary>
        [HttpGet("cronograma-ativo")]
        public async Task<IActionResult> ObterCronogramaAtivo()
        {
            try
            {
                var usuarioId = ObterUsuarioId();

                if (usuarioId <= 0)
                {
                    return Unauthorized(new { mensagem = "Usuário não autenticado" });
                }

                var hoje = DateTime.Now.Date;
                var cronograma = await _db.CronogramasSemanais
                    .Include(c => c.RefeicoesDiarias)
                    .Where(c => c.UsuarioId == usuarioId &&
                               c.DataInicio <= hoje &&
                               c.DataFim >= hoje)
                    .OrderByDescending(c => c.DataCriacao)
                    .FirstOrDefaultAsync();

                if (cronograma == null)
                {
                    return NotFound(new
                    {
                        sucesso = false,
                        mensagem = "Nenhum cronograma semanal ativo encontrado"
                    });
                }

                return Ok(new
                {
                    sucesso = true,
                    cronograma = new
                    {
                        id = cronograma.Id,
                        nomePlano = cronograma.NomePlano,
                        dataInicio = cronograma.DataInicio,
                        dataFim = cronograma.DataFim,
                        observacoes = cronograma.Observacoes,
                        refeicoesDiarias = cronograma.RefeicoesDiarias.Select(r => new
                        {
                            id = r.Id,
                            diaSemana = r.DiaSemana.ToString(),
                            nomeRefeicao = r.NomeRefeicao,
                            horario = r.Horario.ToString(@"hh\:mm"),
                            descricao = r.Descricao,
                            ingredientes = r.Ingredientes,
                            calorias = r.Calorias,
                            proteinas = r.Proteinas,
                            carboidratos = r.Carboidratos,
                            gorduras = r.Gorduras,
                            estaConcluida = r.EstaConcluida
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter cronograma ativo");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro interno do servidor ao obter o cronograma"
                });
            }
        }

        /// <summary>
        /// Obtém as refeições de um dia específico
        /// </summary>
        [HttpGet("refeicoes-dia/{diaSemana}")]
        public async Task<IActionResult> ObterRefeicoesDoDia(string diaSemana)
        {
            try
            {
                var usuarioId = ObterUsuarioId();

                if (usuarioId <= 0)
                {
                    return Unauthorized(new { mensagem = "Usuário não autenticado" });
                }

                // Converte string para enum
                if (!Enum.TryParse<DiaSemana>(diaSemana, true, out var diaEnum))
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dia da semana inválido"
                    });
                }

                var hoje = DateTime.Now.Date;
                var cronograma = await _db.CronogramasSemanais
                    .Include(c => c.RefeicoesDiarias)
                    .Where(c => c.UsuarioId == usuarioId &&
                               c.DataInicio <= hoje &&
                               c.DataFim >= hoje)
                    .OrderByDescending(c => c.DataCriacao)
                    .FirstOrDefaultAsync();

                if (cronograma == null)
                {
                    return NotFound(new
                    {
                        sucesso = false,
                        mensagem = "Nenhum cronograma semanal ativo encontrado"
                    });
                }

                var refeicoesDoDia = cronograma.RefeicoesDiarias
                    .Where(r => r.DiaSemana == diaEnum)
                    .OrderBy(r => r.Horario)
                    .Select(r => new RefeicaoViewModel
                    {
                        Id = r.Id,
                        NomeRefeicao = r.NomeRefeicao,
                        Horario = r.Horario.ToString(@"hh\:mm"),
                        Descricao = r.Descricao,
                        Ingredientes = r.Ingredientes,
                        Calorias = r.Calorias,
                        Proteinas = r.Proteinas,
                        Carboidratos = r.Carboidratos,
                        Gorduras = r.Gorduras,
                        EstaConcluida = r.EstaConcluida,
                        DiaSemana = r.DiaSemana.ToString()
                    })
                    .ToList();

                return Ok(new
                {
                    sucesso = true,
                    diaSemana = diaSemana,
                    refeicoes = refeicoesDoDia
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter refeições do dia {DiaSemana}", diaSemana);
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro interno do servidor ao obter as refeições"
                });
            }
        }

        /// <summary>
        /// Marca uma refeição como concluída
        /// </summary>
        [HttpPut("refeicao/{refeicaoId}/concluir")]
        public async Task<IActionResult> ConcluirRefeicao(int refeicaoId)
        {
            try
            {
                var usuarioId = ObterUsuarioId();

                if (usuarioId <= 0)
                {
                    return Unauthorized(new { mensagem = "Usuário não autenticado" });
                }

                var refeicao = await _db.RefeicoesDiarias
                    .Include(r => r.CronogramaSemanal)
                    .Where(r => r.Id == refeicaoId && r.CronogramaSemanal != null && r.CronogramaSemanal.UsuarioId == usuarioId)
                    .FirstOrDefaultAsync();

                if (refeicao == null)
                {
                    return NotFound(new
                    {
                        sucesso = false,
                        mensagem = "Refeição não encontrada"
                    });
                }

                refeicao.EstaConcluida = true;
                refeicao.DataConclusao = DateTime.Now;
                refeicao.DataUltimaAtualizacao = DateTime.Now;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Refeição marcada como concluída",
                    refeicaoId = refeicao.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao concluir refeição {RefeicaoId}", refeicaoId);
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro interno do servidor ao concluir a refeição"
                });
            }
        }

        /// <summary>
        /// Adiciona uma nova refeição ao cronograma
        /// </summary>
        [HttpPost("refeicao")]
        public async Task<IActionResult> AdicionarRefeicao([FromBody] AdicionarRefeicaoRequest request)
        {
            try
            {
                var usuarioId = ObterUsuarioId();

                if (usuarioId <= 0)
                {
                    return Unauthorized(new { mensagem = "Usuário não autenticado" });
                }

                // Validação
                if (string.IsNullOrEmpty(request.NomeRefeicao) || string.IsNullOrEmpty(request.Descricao))
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Nome e descrição da refeição são obrigatórios"
                    });
                }

                if (!Enum.TryParse<DiaSemana>(request.DiaSemana, true, out var diaEnum))
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dia da semana inválido"
                    });
                }

                if (!TimeSpan.TryParse(request.Horario, out var horario))
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Horário inválido"
                    });
                }

                var hoje = DateTime.Now.Date;
                var cronograma = await _db.CronogramasSemanais
                    .Where(c => c.UsuarioId == usuarioId &&
                               c.DataInicio <= hoje &&
                               c.DataFim >= hoje)
                    .OrderByDescending(c => c.DataCriacao)
                    .FirstOrDefaultAsync();

                if (cronograma == null)
                {
                    return NotFound(new
                    {
                        sucesso = false,
                        mensagem = "Nenhum cronograma semanal ativo encontrado"
                    });
                }

                var novaRefeicao = new RefeicaoDiaria
                {
                    CronogramaSemanalId = cronograma.Id,
                    DiaSemana = diaEnum,
                    NomeRefeicao = request.NomeRefeicao,
                    Horario = horario,
                    Descricao = request.Descricao,
                    Ingredientes = request.Ingredientes,
                    Calorias = request.Calorias,
                    Proteinas = request.Proteinas,
                    Carboidratos = request.Carboidratos,
                    Gorduras = request.Gorduras,
                    DataCriacao = DateTime.Now
                };

                _db.RefeicoesDiarias.Add(novaRefeicao);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Refeição adicionada com sucesso",
                    refeicaoId = novaRefeicao.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar refeição");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro interno do servidor ao adicionar a refeição"
                });
            }
        }

        /// <summary>
        /// Obtém o resumo nutricional do dia
        /// </summary>
        [HttpGet("resumo-dia/{diaSemana}")]
        public async Task<IActionResult> ObterResumoNutricional(string diaSemana)
        {
            try
            {
                var usuarioId = ObterUsuarioId();

                if (usuarioId <= 0)
                {
                    return Unauthorized(new { mensagem = "Usuário não autenticado" });
                }

                if (!Enum.TryParse<DiaSemana>(diaSemana, true, out var diaEnum))
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dia da semana inválido"
                    });
                }

                var hoje = DateTime.Now.Date;
                var cronograma = await _db.CronogramasSemanais
                    .Include(c => c.RefeicoesDiarias)
                    .Where(c => c.UsuarioId == usuarioId &&
                               c.DataInicio <= hoje &&
                               c.DataFim >= hoje)
                    .OrderByDescending(c => c.DataCriacao)
                    .FirstOrDefaultAsync();

                if (cronograma == null)
                {
                    return NotFound(new
                    {
                        sucesso = false,
                        mensagem = "Nenhum cronograma semanal ativo encontrado"
                    });
                }

                var refeicoesDoDia = cronograma.RefeicoesDiarias
                    .Where(r => r.DiaSemana == diaEnum)
                    .ToList();

                var totalCalorias = refeicoesDoDia.Sum(r => r.Calorias ?? 0);
                var totalProteinas = refeicoesDoDia.Sum(r => r.Proteinas ?? 0);
                var totalCarboidratos = refeicoesDoDia.Sum(r => r.Carboidratos ?? 0);
                var totalGorduras = refeicoesDoDia.Sum(r => r.Gorduras ?? 0);
                var totalConcluidas = refeicoesDoDia.Count(r => r.EstaConcluida);

                return Ok(new
                {
                    sucesso = true,
                    diaSemana = diaSemana,
                    totalRefeicoes = refeicoesDoDia.Count,
                    refeicoesConcluidas = totalConcluidas,
                    metaCalorias = 2200, // Meta padrão (poderia vir do perfil do usuário)
                    metaProteinas = 180, // Meta padrão
                    metaCarboidratos = 250, // Meta padrão
                    metaGorduras = 70, // Meta padrão
                    consumo = new
                    {
                        calorias = totalCalorias,
                        proteinas = totalProteinas,
                        carboidratos = totalCarboidratos,
                        gorduras = totalGorduras
                    },
                    progresso = new
                    {
                        calorias = Math.Min(100, (totalCalorias * 100) / 2200),
                        proteinas = Math.Min(100, (totalProteinas * 100) / 180),
                        carboidratos = Math.Min(100, (totalCarboidratos * 100) / 250),
                        gorduras = Math.Min(100, (totalGorduras * 100) / 70)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter resumo nutricional do dia {DiaSemana}", diaSemana);
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro interno do servidor ao obter o resumo nutricional"
                });
            }
        }

        /// <summary>
        /// Simula a obtenção do ID do usuário autenticado
        /// Em produção, usaria Claims do JWT
        /// </summary>
        private int ObterUsuarioId()
        {
            // Simulação - em produção usaria:
            // var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            // return int.Parse(userId);
            
            return 1; // ID do usuário de teste
        }
    }

    /// <summary>
    /// Modelo para requisição de adição de refeição
    /// </summary>
    public class AdicionarRefeicaoRequest
    {
        [Required]
        public string NomeRefeicao { get; set; } = string.Empty;

        [Required]
        public string Descricao { get; set; } = string.Empty;

        [Required]
        public string DiaSemana { get; set; } = string.Empty;

        [Required]
        public string Horario { get; set; } = string.Empty;

        public string Ingredientes { get; set; } = string.Empty;
        public int? Calorias { get; set; }
        public decimal? Proteinas { get; set; }
        public decimal? Carboidratos { get; set; }
        public decimal? Gorduras { get; set; }
    }
}