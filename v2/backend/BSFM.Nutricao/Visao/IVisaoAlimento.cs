namespace BSFM.Nutricao.Visao;

/// <summary>
/// Contrato de reconhecimento de alimento por imagem.
///
/// A abstração existe porque o modelo muda, mas o produto não: o app precisa de
/// "nome do alimento + confiança", não de um framework específico. YOLO é uma
/// implementação possível; visão multimodal é outra.
/// </summary>
public interface IVisaoAlimento
{
    /// <summary>Identificador estável do provedor ("gemini", "yolo", "indisponivel").</summary>
    string Identificacao { get; }

    /// <summary>Indica se o provedor está configurado para atender.</summary>
    bool Disponivel { get; }

    /// <summary>
    /// Tenta identificar um alimento. Nunca inventa: quando não sabe, devolve
    /// <c>Identificado = false</c> com o motivo.
    /// </summary>
    Task<Reconhecimento> ReconhecerAsync(ReadOnlyMemory<byte> imagem, string? dica, CancellationToken ct = default);
}

/// <summary>
/// Provedor que sempre responde "não identificado". Existe para que a ausência
/// de configuração produza uma resposta honesta em vez de erro 500 ou chute.
/// </summary>
public sealed class VisaoIndisponivel : IVisaoAlimento
{
    private readonly string _motivo;

    public VisaoIndisponivel(string motivo = "provedor_de_visao_nao_configurado") => _motivo = motivo;

    public string Identificacao => "indisponivel";

    public bool Disponivel => false;

    public Task<Reconhecimento> ReconhecerAsync(ReadOnlyMemory<byte> imagem, string? dica, CancellationToken ct = default)
        => Task.FromResult(Reconhecimento.NaoIdentificado(Identificacao, _motivo));
}
