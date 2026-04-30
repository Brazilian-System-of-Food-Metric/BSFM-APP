using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClassesBSFM;

namespace BSFM.Models
{
    /// <summary>
    /// Representa o cronograma semanal de alimentação de um usuário
    /// </summary>
    [Table("CronogramasSemanais")]
    public class CronogramaSemanal
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public DateTime DataInicio { get; set; }

        [Required]
        public DateTime DataFim { get; set; }

        [Required]
        [StringLength(100)]
        public string NomePlano { get; set; } = string.Empty;

        [StringLength(500)]
        public string Observacoes { get; set; } = string.Empty;

        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime? DataUltimaAtualizacao { get; set; }

        // Relacionamentos
        public virtual Usuario? Usuario { get; set; }
        public virtual ICollection<RefeicaoDiaria> RefeicoesDiarias { get; set; } = new List<RefeicaoDiaria>();
    }

    /// <summary>
    /// Representa uma refeição diária em um cronograma semanal
    /// </summary>
    [Table("RefeicoesDiarias")]
    public class RefeicaoDiaria
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CronogramaSemanalId { get; set; }

        [Required]
        public DiaSemana DiaSemana { get; set; }

        [Required]
        [StringLength(100)]
        public string NomeRefeicao { get; set; } = string.Empty;

        [Required]
        public TimeSpan Horario { get; set; }

        [StringLength(500)]
        public string Descricao { get; set; } = string.Empty;

        public int? Calorias { get; set; }
        public decimal? Proteinas { get; set; } // em gramas
        public decimal? Carboidratos { get; set; } // em gramas
        public decimal? Gorduras { get; set; } // em gramas
        public decimal? Fibra { get; set; } // em gramas

        [StringLength(500)]
        public string Ingredientes { get; set; } = string.Empty;

        [StringLength(1000)]
        public string InstrucoesPreparo { get; set; } = string.Empty;

        public bool EstaConcluida { get; set; } = false;
        public DateTime? DataConclusao { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime? DataUltimaAtualizacao { get; set; }

        // Relacionamentos
        public virtual CronogramaSemanal? CronogramaSemanal { get; set; }
    }

    /// <summary>
    /// Enumeração para os dias da semana
    /// </summary>
    public enum DiaSemana
    {
        Domingo = 0,
        Segunda = 1,
        Terca = 2,
        Quarta = 3,
        Quinta = 4,
        Sexta = 5,
        Sabado = 6
    }

    /// <summary>
    /// Modelo para requisição de importação de prescrição nutricional via OCR
    /// </summary>
    public class ImportarPrescricaoRequest
    {
        [Required]
        public string ImagemBase64 { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string NomePlano { get; set; } = string.Empty;

        [StringLength(500)]
        public string Observacoes { get; set; } = string.Empty;

        public DateTime? DataInicio { get; set; }
    }

    /// <summary>
    /// Modelo para resposta da importação de prescrição
    /// </summary>
    public class ImportarPrescricaoResponse
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public int CronogramaId { get; set; }
        public int TotalRefeicoes { get; set; }
        public List<RefeicaoDiaria> RefeicoesProcessadas { get; set; } = new List<RefeicaoDiaria>();
        public List<string> ErrosProcessamento { get; set; } = new List<string>();
    }

    /// <summary>
    /// Modelo simplificado para visualização de refeições no frontend
    /// </summary>
    public class RefeicaoViewModel
    {
        public int Id { get; set; }
        public string NomeRefeicao { get; set; } = string.Empty;
        public string Horario { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Ingredientes { get; set; } = string.Empty;
        public int? Calorias { get; set; }
        public decimal? Proteinas { get; set; }
        public decimal? Carboidratos { get; set; }
        public decimal? Gorduras { get; set; }
        public bool EstaConcluida { get; set; }
        public string DiaSemana { get; set; } = string.Empty;
    }
}