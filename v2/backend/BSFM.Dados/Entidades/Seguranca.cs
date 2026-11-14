using System.ComponentModel.DataAnnotations;

namespace BSFM.Dados.Entidades;

/// <summary>
/// Sessão autenticada. Uma linha por login bem-sucedido.
///
/// O token JWT carrega o <see cref="Jti"/>; esta tabela é a fonte da verdade
/// sobre validade e revogação. É o que permite "sair de todos os dispositivos"
/// e invalidar sessão antes do token expirar.
/// </summary>
public class Sessao
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Identificador único do token (<c>jti</c>), presente nas claims.</summary>
    public string Jti { get; set; } = string.Empty;

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadaEm { get; set; }
    public DateTime? UltimoUsoEm { get; set; }

    /// <summary>SHA-256 do IP — guarda o suficiente para investigar abuso sem armazenar o IP.</summary>
    public string? IpHash { get; set; }

    /// <summary>SHA-256 do User-Agent, pelo mesmo motivo.</summary>
    public string? UserAgentHash { get; set; }

    public string? RevogadaMotivo { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public bool EstaAtiva(DateTime agora) => RevogadaEm is null && ExpiraEm > agora;
}

/// <summary>
/// Refresh token com rotação e detecção de reuso.
///
/// O valor em claro nunca é gravado: guarda-se apenas o SHA-256. Se um token
/// já rotacionado reaparecer, a cadeia inteira é revogada — é assim que se
/// detecta roubo de token.
/// </summary>
public class RefreshToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessaoId { get; set; }
    public Sessao? Sessao { get; set; }

    /// <summary>SHA-256 do token em claro (hex).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }

    /// <summary>Token que substituiu este na rotação.</summary>
    public Guid? SubstituidoPorId { get; set; }

    public string? IpHash { get; set; }

    public bool EstaAtivo(DateTime agora) => RevogadoEm is null && ExpiraEm > agora;
}

/// <summary>
/// Trilha de auditoria. Registra o evento, não o conteúdo sensível: dados
/// pessoais aparecem apenas como hash ou referência, nunca em texto claro.
/// </summary>
public class Auditoria
{
    [Key]
    public long Id { get; set; }

    public int? UsuarioId { get; set; }

    /// <summary>Ex.: login_sucesso, login_falha, senha_alterada, sessao_revogada.</summary>
    public string Evento { get; set; } = string.Empty;

    public string? Entidade { get; set; }
    public string? EntidadeId { get; set; }

    /// <summary>Detalhes em JSON, já sanitizados (sem senha, sem token, sem dado clínico).</summary>
    public string? DadosJson { get; set; }

    public string? IpHash { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Consentimento LGPD por finalidade. Dado de saúde é categoria especial:
/// exige registro de quando foi concedido, para qual finalidade e sob qual versão do termo.
/// </summary>
public class ConsentimentoLGPD
{
    [Key]
    public long Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Ex.: dados_saude, analise_imagem, comunicacoes.</summary>
    public string Finalidade { get; set; } = string.Empty;

    public string VersaoTermo { get; set; } = string.Empty;
    public bool Concedido { get; set; }
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
    public string? IpHash { get; set; }

    /// <summary>Como o consentimento foi obtido: cadastro, tela_metas, reconsentimento.</summary>
    public string? Origem { get; set; }
}
