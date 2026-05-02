using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BSFM.Models;
using PonteBanco;
using Microsoft.Extensions.Logging;

namespace BSFM.Services
{
    /// <summary>
    /// Serviço para processamento OCR de prescrições nutricionais
    /// Simula a extração de informações de imagens de prescrições médicas
    /// </summary>
    public class OcrNutricionalService
    {
        private readonly ILogger<OcrNutricionalService> _logger;
        private readonly PonteDB _db;

        public OcrNutricionalService(ILogger<OcrNutricionalService> logger, PonteDB db)
        {
            _logger = logger;
            _db = db;
        }

        /// <summary>
        /// Processa uma imagem de prescrição nutricional e extrai as informações de refeições
        /// </summary>
        /// <param name="imagemBase64">Imagem em formato base64</param>
        /// <param name="nomePlano">Nome do plano alimentar</param>
        /// <param name="observacoes">Observações adicionais</param>
        /// <param name="dataInicio">Data de início do plano</param>
        /// <param name="usuarioId">ID do usuário</param>
        /// <returns>Resultado do processamento OCR</returns>
        public async Task<ImportarPrescricaoResponse> ProcessarPrescricaoNutricionalAsync(
            string imagemBase64, 
            string nomePlano, 
            string observacoes, 
            DateTime? dataInicio, 
            int usuarioId)
        {
            try
            {
                _logger.LogInformation("Iniciando processamento OCR para prescrição nutricional do usuário {UsuarioId}", usuarioId);

                // Validação básica da imagem
                if (string.IsNullOrEmpty(imagemBase64))
                {
                    return new ImportarPrescricaoResponse
                    {
                        Sucesso = false,
                        Mensagem = "Imagem inválida ou vazia"
                    };
                }

                // Decodifica a imagem base64 (simulação)
                var imagemBytes = Convert.FromBase64String(imagemBase64.Split(',').Last());
                
                // Simula o processamento OCR real
                var textoExtraido = await SimularProcessamentoOCRAsync(imagemBytes);
                
                if (string.IsNullOrEmpty(textoExtraido))
                {
                    return new ImportarPrescricaoResponse
                    {
                        Sucesso = false,
                        Mensagem = "Não foi possível extrair texto da imagem"
                    };
                }

                // Extrai informações do texto OCR
                var informacoesPlano = ExtrairInformacoesPlano(textoExtraido);
                
                // Cria o cronograma semanal
                var cronograma = await CriarCronogramaSemanalAsync(
                    usuarioId, 
                    nomePlano, 
                    observacoes, 
                    dataInicio, 
                    informacoesPlano);

                // Extrai e cria as refeições diárias
                var refeicoes = ExtrairRefeicoesDoTexto(textoExtraido, cronograma.Id);
                
                // Salva as refeições no banco de dados
                await SalvarRefeicoesAsync(refeicoes);

                _logger.LogInformation("Processamento OCR concluído com sucesso. Plano ID: {CronogramaId}, {TotalRefeicoes} refeições criadas", 
                    cronograma.Id, refeicoes.Count);

                return new ImportarPrescricaoResponse
                {
                    Sucesso = true,
                    Mensagem = $"Prescrição processada com sucesso! {refeicoes.Count} refeições foram importadas para o plano '{nomePlano}'.",
                    CronogramaId = cronograma.Id,
                    TotalRefeicoes = refeicoes.Count,
                    RefeicoesProcessadas = refeicoes,
                    ErrosProcessamento = new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar prescrição nutricional para o usuário {UsuarioId}", usuarioId);
                return new ImportarPrescricaoResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro ao processar prescrição: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Simula o processamento OCR real (em produção, usaria Azure Computer Vision, Google Vision, etc.)
        /// </summary>
        private async Task<string> SimularProcessamentoOCRAsync(byte[] imagemBytes)
        {
            // Simula um tempo de processamento
            await Task.Delay(1000);

            // Simula texto extraído de uma prescrição nutricional real
            return @"
PREScrição NUTRICIONAL - PLANO SEMANAL
Nutricionista: Dra. Ana Silva CRN-3 12345
Paciente: João Silva
Data: 02/04/2024

PLANO ALIMENTAR SEMANAL - OBJETIVO: HIPERTROFIA

SEGUNDA-FEIRA:
08:00 - Café da Manhã: Ovos mexidos com espinafre, pão integral e abacate
12:30 - Almoço: Filé de frango grelhado, arroz integral, feijão preto, salada de folhas
15:30 - Lanche: Iogurte natural com granola e frutas vermelhas
18:30 - Jantar: Salmão ao forno, quinoa, legumes refogados
21:00 - Ceia: Whey protein com banana

TERÇA-FEIRA:
08:00 - Café da Manhã: Aveia com leite, mel e castanhas
12:30 - Almoço: Bife acebolado, batata doce, salada de rúcula
15:30 - Lanche: Sanduíche de peito de peru com queijo branco
18:30 - Jantar: Tilápia grelhada, purê de inhame, brócolis
21:00 - Ceia: Iogurte grego com mel

QUARTA-FEIRA:
08:00 - Café da Manhã: Panquecas de aveia com frutas
12:30 - Almoço: Frango ao molho de mostarda, arroz integral, lentilha
15:30 - Lanche: Smoothie de proteína com frutas
18:30 - Jantar: Omelete de claras, pão integral, salada
21:00 - Ceia: Castanhas e frutas secas

QUINTA-FEIRA:
08:00 - Café da Manhã: Tapioca com ricota e mel
12:30 - Almoço: Filé mignon, batata assada, salada verde
15:30 - Lanche: Barra de cereal proteica
18:30 - Jantar: Peixe ao vapor, arroz negro, legumes
21:00 - Ceia: Leite com cacau em pó

SEXTA-FEIRA:
08:00 - Café da Manhã: Smoothie bowl com frutas e granola
12:30 - Almoço: Frango desfiado, quinoa, abobrinha refogada
15:30 - Lanche: Frutas frescas
18:30 - Jantar: Carne moída ao molho de tomate, arroz integral
21:00 - Ceia: Iogurte com frutas

SÁBADO:
09:00 - Café da Manhã: Ovos poché, pão integral, abacate
13:00 - Almoço: Lasanha de berinjela, salada de grão de bico
16:00 - Lanche: Suco verde com whey
19:00 - Jantar: Frango ao curry, arroz integral
22:00 - Ceia: Frutas e castanhas

DOMINGO:
09:00 - Café da Manhã: Panquecas com frutas vermelhas
13:00 - Almoço: Peixe assado, purê de batata doce, salada
16:00 - Lanche: Iogurte com granola
19:00 - Jantar: Sopa de legumes com frango desfiado
22:00 - Ceia: Chá e frutas

OBSERVAÇÕES:
- Ingerir no mínimo 3 litros de água por dia
- Suplementação: Whey Protein 30g ao acordar e pós-treino
- Multivitamínico diário
- Ômega 3: 2g ao dia
- Evitar alimentos ultraprocessados
- Manter horários regulares das refeições

CALORIAS DIÁRIAS ESTIMADAS: 2800 kcal
PROTEÍNAS: 200g (30%)
CARBOIDRATOS: 350g (50%)
GORDURAS: 70g (20%)
";
        }

        /// <summary>
        /// Extrai informações gerais do plano da prescrição
        /// </summary>
        private InformacoesPlano ExtrairInformacoesPlano(string texto)
        {
            var informacoes = new InformacoesPlano();

            // Extrai objetivo
            var objetivoMatch = Regex.Match(texto, @"OBJETIVO:\s*([^\n]+)", RegexOptions.IgnoreCase);
            if (objetivoMatch.Success)
            {
                informacoes.Objetivo = objetivoMatch.Groups[1].Value.Trim();
            }

            // Extrai calorias diárias
            var caloriasMatch = Regex.Match(texto, @"CALORIAS DIÁRIAS ESTIMADAS:\s*(\d+)\s*kcal", RegexOptions.IgnoreCase);
            if (caloriasMatch.Success)
            {
                informacoes.CaloriasDiarias = int.Parse(caloriasMatch.Groups[1].Value);
            }

            // Extrai macros
            var proteinasMatch = Regex.Match(texto, @"PROTEÍNAS:\s*(\d+)g", RegexOptions.IgnoreCase);
            if (proteinasMatch.Success)
            {
                informacoes.ProteinasDiarias = int.Parse(proteinasMatch.Groups[1].Value);
            }

            var carboidratosMatch = Regex.Match(texto, @"CARBOIDRATOS:\s*(\d+)g", RegexOptions.IgnoreCase);
            if (carboidratosMatch.Success)
            {
                informacoes.CarboidratosDiarios = int.Parse(carboidratosMatch.Groups[1].Value);
            }

            var gordurasMatch = Regex.Match(texto, @"GORDURAS:\s*(\d+)g", RegexOptions.IgnoreCase);
            if (gordurasMatch.Success)
            {
                informacoes.GordurasDiarias = int.Parse(gordurasMatch.Groups[1].Value);
            }

            return informacoes;
        }

        /// <summary>
        /// Extrai as refeições do texto OCR
        /// </summary>
        private List<RefeicaoDiaria> ExtrairRefeicoesDoTexto(string texto, int cronogramaId)
        {
            var refeicoes = new List<RefeicaoDiaria>();
            var diasSemana = new[] { "SEGUNDA-FEIRA", "TERÇA-FEIRA", "QUARTA-FEIRA", "QUINTA-FEIRA", "SEXTA-FEIRA", "SÁBADO", "DOMINGO" };

            foreach (var dia in diasSemana)
            {
                var diaEnum = ObterDiaSemanaEnum(dia);
                var refeicoesDoDia = ExtrairRefeicoesDoDia(texto, dia, diaEnum, cronogramaId);
                refeicoes.AddRange(refeicoesDoDia);
            }

            return refeicoes;
        }

        /// <summary>
        /// Extrai refeições de um dia específico
        /// </summary>
        private List<RefeicaoDiaria> ExtrairRefeicoesDoDia(string texto, string nomeDia, DiaSemana diaEnum, int cronogramaId)
        {
            var refeicoes = new List<RefeicaoDiaria>();
            
            // Encontra o bloco de texto do dia
            var inicioDia = texto.IndexOf(nomeDia);
            if (inicioDia == -1) return refeicoes;

            var fimDia = texto.IndexOf("\n\n", inicioDia);
            if (fimDia == -1) fimDia = texto.Length;

            var blocoDia = texto.Substring(inicioDia, fimDia - inicioDia);

            // Expressão regular para encontrar refeições no formato "HH:MM - Nome: Descrição"
            var regex = new Regex(@"(\d{2}:\d{2})\s*-\s*([^:]+):\s*(.+?)(?=\n\d{2}:\d{2}|\n\n|\n[A-Z]|$)", RegexOptions.Singleline);

            var matches = regex.Matches(blocoDia);
            
            foreach (Match match in matches)
            {
                var horarioStr = match.Groups[1].Value;
                var nomeRefeicao = match.Groups[2].Value.Trim();
                var descricao = match.Groups[3].Value.Trim();

                // Converte horário para TimeSpan
                if (TimeSpan.TryParse(horarioStr, out var horario))
                {
                    var refeicao = new RefeicaoDiaria
                    {
                        CronogramaSemanalId = cronogramaId,
                        DiaSemana = diaEnum,
                        NomeRefeicao = nomeRefeicao,
                        Horario = horario,
                        Descricao = descricao,
                        Ingredientes = ExtrairIngredientes(descricao),
                        Calorias = CalcularCaloriasAproximadas(descricao),
                        Proteinas = CalcularProteinasAproximadas(descricao),
                        Carboidratos = CalcularCarboidratosAproximados(descricao),
                        Gorduras = CalcularGordurasAproximadas(descricao),
                        DataCriacao = DateTime.Now
                    };

                    refeicoes.Add(refeicao);
                }
            }

            return refeicoes;
        }

        /// <summary>
        /// Extrai ingredientes da descrição da refeição
        /// </summary>
        private string ExtrairIngredientes(string descricao)
        {
            // Simples extração de ingredientes comuns
            var ingredientes = new List<string>();
            
            if (descricao.Contains("ovo", StringComparison.OrdinalIgnoreCase))
                ingredientes.Add("Ovos");
            if (descricao.Contains("frango", StringComparison.OrdinalIgnoreCase))
                ingredientes.Add("Frango");
            if (descricao.Contains("peixe", StringComparison.OrdinalIgnoreCase))
                ingredientes.Add("Peixe");
            if (descricao.Contains("arroz", StringComparison.OrdinalIgnoreCase))
                ingredientes.Add("Arroz");
            if (descricao.Contains("batata", StringComparison.OrdinalIgnoreCase))
                ingredientes.Add("Batata");
            if (descricao.Contains("salada", StringComparison.OrdinalIgnoreCase))
                ingredientes.Add("Salada verde");
            if (descricao.Contains("fruta", StringComparison.OrdinalIgnoreCase))
                ingredientes.Add("Frutas");
            if (descricao.Contains("iogurte", StringComparison.OrdinalIgnoreCase))
                ingredientes.Add("Iogurte");

            return ingredientes.Any() ? string.Join(", ", ingredientes) : "Ingredientes variados";
        }

        /// <summary>
        /// Calcula calorias aproximadas baseado na descrição
        /// </summary>
        private int? CalcularCaloriasAproximadas(string descricao)
        {
            var caloriasBase = 300; // Base para qualquer refeição
            
            if (descricao.Contains("frango", StringComparison.OrdinalIgnoreCase) || 
                descricao.Contains("peixe", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("carne", StringComparison.OrdinalIgnoreCase))
                caloriasBase += 200;

            if (descricao.Contains("arroz", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("batata", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("quinoa", StringComparison.OrdinalIgnoreCase))
                caloriasBase += 150;

            if (descricao.Contains("fruta", StringComparison.OrdinalIgnoreCase))
                caloriasBase += 50;

            if (descricao.Contains("aveia", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("granola", StringComparison.OrdinalIgnoreCase))
                caloriasBase += 100;

            return caloriasBase;
        }

        /// <summary>
        /// Calcula proteínas aproximadas
        /// </summary>
        private decimal? CalcularProteinasAproximadas(string descricao)
        {
            decimal proteinas = 10; // Base

            if (descricao.Contains("frango", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("peixe", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("carne", StringComparison.OrdinalIgnoreCase))
                proteinas += 25;

            if (descricao.Contains("ovo", StringComparison.OrdinalIgnoreCase))
                proteinas += 10;

            if (descricao.Contains("iogurte", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("queijo", StringComparison.OrdinalIgnoreCase))
                proteinas += 8;

            return proteinas;
        }

        /// <summary>
        /// Calcula carboidratos aproximados
        /// </summary>
        private decimal? CalcularCarboidratosAproximados(string descricao)
        {
            decimal carboidratos = 20; // Base

            if (descricao.Contains("arroz", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("batata", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("quinoa", StringComparison.OrdinalIgnoreCase))
                carboidratos += 40;

            if (descricao.Contains("aveia", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("granola", StringComparison.OrdinalIgnoreCase))
                carboidratos += 30;

            if (descricao.Contains("fruta", StringComparison.OrdinalIgnoreCase))
                carboidratos += 15;

            return carboidratos;
        }

        /// <summary>
        /// Calcula gorduras aproximadas
        /// </summary>
        private decimal? CalcularGordurasAproximadas(string descricao)
        {
            decimal gorduras = 5; // Base

            if (descricao.Contains("abacate", StringComparison.OrdinalIgnoreCase))
                gorduras += 15;

            if (descricao.Contains("castanha", StringComparison.OrdinalIgnoreCase) ||
                descricao.Contains("nozes", StringComparison.OrdinalIgnoreCase))
                gorduras += 10;

            if (descricao.Contains("queijo", StringComparison.OrdinalIgnoreCase))
                gorduras += 8;

            return gorduras;
        }

        /// <summary>
        /// Converte nome do dia para enum DiaSemana
        /// </summary>
        private DiaSemana ObterDiaSemanaEnum(string nomeDia)
        {
            return nomeDia switch
            {
                "SEGUNDA-FEIRA" => DiaSemana.Segunda,
                "TERÇA-FEIRA" => DiaSemana.Terca,
                "QUARTA-FEIRA" => DiaSemana.Quarta,
                "QUINTA-FEIRA" => DiaSemana.Quinta,
                "SEXTA-FEIRA" => DiaSemana.Sexta,
                "SÁBADO" => DiaSemana.Sabado,
                "DOMINGO" => DiaSemana.Domingo,
                _ => DiaSemana.Segunda
            };
        }

        /// <summary>
        /// Cria o cronograma semanal no banco de dados
        /// </summary>
        private async Task<CronogramaSemanal> CriarCronogramaSemanalAsync(int usuarioId, string nomePlano, string observacoes, DateTime? dataInicio, InformacoesPlano informacoes)
        {
            var dataInicial = dataInicio ?? DateTime.Now.Date;
            var dataFinal = dataInicial.AddDays(6);

            var cronograma = new CronogramaSemanal
            {
                UsuarioId = usuarioId,
                DataInicio = dataInicial,
                DataFim = dataFinal,
                NomePlano = nomePlano,
                Observacoes = observacoes,
                DataCriacao = DateTime.Now
            };

            _db.CronogramasSemanais.Add(cronograma);
            await _db.SaveChangesAsync();

            return cronograma;
        }

        /// <summary>
        /// Salva as refeições no banco de dados
        /// </summary>
        private async Task SalvarRefeicoesAsync(List<RefeicaoDiaria> refeicoes)
        {
            _db.RefeicoesDiarias.AddRange(refeicoes);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Informações extraídas da prescrição nutricional
    /// </summary>
    public class InformacoesPlano
    {
        public string Objetivo { get; set; } = string.Empty;
        public int? CaloriasDiarias { get; set; }
        public int? ProteinasDiarias { get; set; }
        public int? CarboidratosDiarios { get; set; }
        public int? GordurasDiarias { get; set; }
    }
}