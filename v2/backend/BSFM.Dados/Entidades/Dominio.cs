using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BSFM.Dados.Entidades;

/// <summary>
/// Base para toda entidade persistida. Centraliza identidade e carimbo de tempo
/// para que auditoria e ordenação não dependam de cada tabela lembrar disso.
/// </summary>
public abstract class EntidadeBase
{
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
}

/// <summary>
/// Usuário do BSFM. Mantém os nomes de coluna originais do banco atual
/// (PascalCase, tabela "Usuarios") para que a migração não exija renomear nada,
/// e acrescenta os campos que a camada de segurança precisa.
/// </summary>
public class Usuario : EntidadeBase
{
    [Key]
    public int ID { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Idade declarada — mantida por compatibilidade; o valor autoritativo vem de DataNascimento.</summary>
    public int Idade { get; set; }

    public string Email { get; set; } = string.Empty;
    public bool EmailVerificado { get; set; }

    /// <summary>Hash PBKDF2 (formato `pbkdf2-sha256$...`) ou BCrypt legado.</summary>
    public string SenhaHash { get; set; } = string.Empty;
    public DateTime? SenhaAtualizadaEm { get; set; }

    // --- Termos e LGPD -----------------------------------------------------
    public bool AceitouTermos { get; set; }
    public DateTime DataAceite { get; set; }
    public string VersaoTermos { get; set; } = string.Empty;

    // --- Antropometria -----------------------------------------------------
    public string Sexo { get; set; } = "Não Informado";
    public double Peso { get; set; }
    public double Altura { get; set; }
    public double PesoMeta { get; set; }
    public DateTime? DataNascimento { get; set; }
    public string TipoPessoa { get; set; } = "Sedentário";
    public string Intolerancia { get; set; } = string.Empty;
    public string Diabetes { get; set; } = "Não informado";

    // --- Métricas calculadas ----------------------------------------------
    public double IMC { get; set; }
    public double TMB { get; set; }
    public double GastoTotal { get; set; }

    // --- Segundo fator -----------------------------------------------------
    public string? TotpSegredo { get; set; }
    public bool TotpHabilitado { get; set; }

    // --- Autorização e bloqueio progressivo -------------------------------
    public string Papel { get; set; } = "usuario";
    public int TentativasFalhas { get; set; }
    public DateTime? BloqueadoAte { get; set; }

    /// <summary>Token de verificação por e-mail (uso único, expira).</summary>
    public string? TokenVerificacao { get; set; }
    public DateTime? TokenVerificacaoExpiraEm { get; set; }

    public int CalcularIdade()
    {
        if (DataNascimento is null) return Idade;
        var hoje = DateTime.Today;
        var idade = hoje.Year - DataNascimento.Value.Year;
        if (DataNascimento.Value.Date > hoje.AddYears(-idade)) idade--;
        return idade;
    }
}

/// <summary>Resultado de uma análise de alimento (foto ou rótulo).</summary>
public class AnaliseIA
{
    [Key]
    public int ID { get; set; }

    public int UsuarioID { get; set; }
    public Usuario? Usuario { get; set; }

    public string Alimento { get; set; } = string.Empty;
    public double Calorias { get; set; }
    public double Proteinas { get; set; }
    public double Carbos { get; set; }
    public double Gorduras { get; set; }
    public string Porcao { get; set; } = string.Empty;
    public DateTime DataAnalise { get; set; } = DateTime.UtcNow;

    // --- Feedback nutricional ---------------------------------------------
    public bool? PodeConsumir { get; set; }
    public int PontuacaoSaude { get; set; }
    public string AnaliseEmRelacaoAMeta { get; set; } = string.Empty;
    public string DicaBSFM { get; set; } = string.Empty;

    // --- Rastreabilidade do modelo (novo) ---------------------------------
    /// <summary>Confiança da detecção, 0..1. Substitui o threshold fixo de 0,16.</summary>
    public double? Confianca { get; set; }

    /// <summary>Versão do modelo/dataset que produziu o resultado, para reprodutibilidade.</summary>
    public string? ModeloVersao { get; set; }

    /// <summary>SHA-256 da imagem analisada — permite deduplicar sem guardar a foto.</summary>
    public string? ImagemHash { get; set; }

    /// <summary>Origem da análise: foto, rotulo, manual.</summary>
    public string Origem { get; set; } = "foto";
}

/// <summary>Série histórica de medidas corporais.</summary>
public class HistoricoProgresso
{
    [Key]
    public int ID { get; set; }
    public int UsuarioID { get; set; }
    public Usuario? Usuario { get; set; }
    public double Peso { get; set; }
    public double Altura { get; set; }
    public double IMC { get; set; }
    public DateTime DataRegistro { get; set; } = DateTime.UtcNow;
}

/// <summary>Registro de consumo de água.</summary>
public class ConsumoAgua
{
    [Key]
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public double Ml { get; set; }
    public DateTime DataRegistro { get; set; } = DateTime.UtcNow;
}

/// <summary>Prato agendado na semana.</summary>
public class RefeicaoAgendada
{
    [Key]
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string DiaSemana { get; set; } = string.Empty;
    public string TipoRefeicao { get; set; } = string.Empty;
    public string NomePrato { get; set; } = string.Empty;
    public string Ingredientes { get; set; } = string.Empty;
    public string ModoPreparo { get; set; } = string.Empty;
    public double Calorias { get; set; }
    public double Proteinas { get; set; }
    public double Carboidratos { get; set; }
    public double Gorduras { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}

/// <summary>Plano alimentar semanal (cabeçalho).</summary>
public class CronogramaSemanal
{
    [Key]
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataInicio { get; set; } = DateTime.UtcNow;

    public ICollection<RefeicaoDiaria> RefeicoesDiarias { get; set; } = new List<RefeicaoDiaria>();
}

/// <summary>Refeição de um dia dentro de um cronograma semanal.</summary>
public class RefeicaoDiaria
{
    [Key]
    public int Id { get; set; }
    public int CronogramaSemanalId { get; set; }
    public CronogramaSemanal? CronogramaSemanal { get; set; }
    public string DiaSemana { get; set; } = string.Empty;
    public string TipoRefeicao { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public double Calorias { get; set; }
}

/// <summary>
/// Estabelecimento de saúde. Ampliado para receber a base de São Paulo:
/// identificador CNES, coordenadas e classificação.
/// </summary>
public class Hospital
{
    [Key]
    public int ID { get; set; }

    /// <summary>Código CNES do DATASUS — chave natural do estabelecimento.</summary>
    public string? Cnes { get; set; }

    public string NomeHospital { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }
    public string? TipoEstabelecimento { get; set; }

    public string? Endereco { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string Cidade { get; set; } = "São Paulo";
    public string Uf { get; set; } = "SP";
    public string? Cep { get; set; }

    public string? Telefone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Especialidades separadas por ponto e vírgula (formato do CNES).</summary>
    public string? Especialidades { get; set; }

    public bool Ativo { get; set; } = true;
}

/// <summary>Meta nutricional do usuário. Antes vivia como campo solto em Usuarios.</summary>
public class Meta
{
    [Key]
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public double CaloriasAlvo { get; set; }
    public double ProteinasAlvo { get; set; }
    public double AguaAlvoMl { get; set; }
    public double PesoAlvo { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public bool Ativa { get; set; } = true;
}

/// <summary>Alimento da tabela TACO — base nutricional local, sem depender de API externa.</summary>
public class Alimento
{
    [Key]
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Grupo { get; set; } = string.Empty;
    public double Calorias100g { get; set; }
    public double Proteina100g { get; set; }
    public double Carboidrato100g { get; set; }
    public double Gordura100g { get; set; }
    public double Fibra100g { get; set; }
    public string Fonte { get; set; } = "TACO";
}
