using BSFM.Nutricao.Visao;

namespace BSFM.Nutricao;

/// <summary>
/// Resultado completo de uma tentativa de análise: o que foi reconhecido, qual
/// alimento do catálogo casou e os nutrientes na porção pedida.
///
/// Quando <c>Identificado</c> é falso, nada de nutriente é inventado — é isso
/// que separa um sistema honesto de um que chuta número.
/// </summary>
public sealed record ResultadoAnalise(
    bool Identificado,
    string? NomeReconhecido,
    string Origem,
    double Confianca,
    AlimentoCatalogo? Alimento,
    double? PontuacaoBusca,
    double Gramas,
    NutrientesPor100g? NaPorcao,
    string? Motivo,
    IReadOnlyList<ResultadoBusca> Candidatos)
{
    public static ResultadoAnalise Falha(string origem, string motivo, IReadOnlyList<ResultadoBusca>? candidatos = null)
        => new(false, null, origem, 0, null, null, 0, null, motivo, candidatos ?? Array.Empty<ResultadoBusca>());
}

/// <summary>
/// Orquestra as duas etapas que o app atual confunde em uma:
///   1. descobrir <b>o nome</b> do alimento (visão ou texto digitado);
///   2. transformar o nome em <b>números</b> (catálogo nutricional).
///
/// Separar as etapas é o que permite trocar o modelo sem tocar na nutrição — e
/// é o que faltava: hoje o YOLO devolve um rótulo em inglês que passa por um
/// dicionário fixo de 452 itens e depois por uma API americana.
/// </summary>
public static class AnalisadorAlimento
{
    public const double GramasPadrao = 100;
    public const double GramasMinimo = 1;
    public const double GramasMaximo = 3000;

    /// <summary>Fluxo por imagem: visão identifica, catálogo converte.</summary>
    public static async Task<ResultadoAnalise> PorImagemAsync(
        ReadOnlyMemory<byte> imagem,
        IVisaoAlimento visao,
        string? dica = null,
        double gramas = GramasPadrao,
        IReadOnlyList<AlimentoCatalogo>? catalogo = null,
        CancellationToken ct = default)
    {
        if (visao is null)
            return ResultadoAnalise.Falha("indisponivel", "provedor_de_visao_ausente");

        if (!visao.Disponivel)
            return ResultadoAnalise.Falha(visao.Identificacao, "provedor_de_visao_indisponivel");

        var reconhecimento = await visao.ReconhecerAsync(imagem, dica, ct);

        if (!reconhecimento.Identificado || string.IsNullOrWhiteSpace(reconhecimento.Nome))
            return ResultadoAnalise.Falha(reconhecimento.Origem,
                reconhecimento.Motivo ?? "nao_identificado");

        return Resolver(reconhecimento.Nome!, reconhecimento.Origem, reconhecimento.Confianca, gramas, catalogo);
    }

    /// <summary>Fluxo por texto: busca no catálogo e converte.</summary>
    public static ResultadoAnalise PorNome(
        string? nome,
        double gramas = GramasPadrao,
        IReadOnlyList<AlimentoCatalogo>? catalogo = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return ResultadoAnalise.Falha("texto", "nome_ausente");

        return Resolver(nome, "texto", 1.0, gramas, catalogo);
    }

    private static ResultadoAnalise Resolver(
        string nome, string origem, double confianca, double gramas,
        IReadOnlyList<AlimentoCatalogo>? catalogo)
    {
        var candidatos = BuscaAlimentos.Buscar(nome, catalogo, 5);
        var melhor = candidatos.FirstOrDefault();

        if (melhor is null)
        {
            // A visão pode acertar o nome e o catálogo não ter o item.
            // Nesse caso respondemos "achei X, mas não tenho os nutrientes".
            return new ResultadoAnalise(false, nome, origem, confianca, null, null, 0, null,
                "alimento_fora_do_catalogo", candidatos);
        }

        var gramasValidas = Math.Clamp(gramas, GramasMinimo, GramasMaximo);

        return new ResultadoAnalise(
            true,
            nome,
            origem,
            confianca,
            melhor.Alimento,
            melhor.Pontuacao,
            gramasValidas,
            melhor.Alimento.Por100g.Escalar(gramasValidas),
            null,
            candidatos);
    }
}
