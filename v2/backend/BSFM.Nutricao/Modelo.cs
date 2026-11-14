namespace BSFM.Nutricao;

/// <summary>
/// Valores nutricionais por 100 g. A unidade é sempre 100 g para que o cálculo
/// por porção seja uma multiplicação simples e conferível.
/// </summary>
public sealed record NutrientesPor100g(
    double Kcal,
    double Proteina,
    double Carboidrato,
    double Gordura,
    double Fibra)
{
    public static readonly NutrientesPor100g Zero = new(0, 0, 0, 0, 0);

    public NutrientesPor100g Escalar(double gramas)
    {
        if (gramas <= 0) return Zero;
        var fator = gramas / 100.0;
        return new NutrientesPor100g(
            Arredondar(Kcal * fator),
            Arredondar(Proteina * fator),
            Arredondar(Carboidrato * fator),
            Arredondar(Gordura * fator),
            Arredondar(Fibra * fator));
    }

    private static double Arredondar(double valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public override string ToString() => $"{Kcal:F0} kcal | P {Proteina:F1} C {Carboidrato:F1} G {Gordura:F1}";
}

/// <summary>Alimento do catálogo, com sinônimos para casar com o que o usuário digita.</summary>
public sealed record AlimentoCatalogo(
    string Nome,
    string Grupo,
    NutrientesPor100g Por100g,
    IReadOnlyList<string> Sinonimos)
{
    /// <summary>Todos os textos pelos quais este alimento pode ser encontrado.</summary>
    public IEnumerable<string> Termos()
    {
        yield return Nome;
        foreach (var sinonimo in Sinonimos) yield return sinonimo;
    }
}

/// <summary>Resultado de uma busca no catálogo, com a pontuação que justificou o match.</summary>
public sealed record ResultadoBusca(AlimentoCatalogo Alimento, double Pontuacao, string TermoQueCasou);

/// <summary>O que o sistema responde quando precisa mostrar "não sei" em vez de chutar.</summary>
public sealed record Reconhecimento(
    bool Identificado,
    string? Nome,
    double Confianca,
    string Origem,
    string? Motivo = null)
{
    public static Reconhecimento NaoIdentificado(string origem, string motivo) =>
        new(false, null, 0, origem, motivo);
}
