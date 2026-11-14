namespace BSFM.Api.Servicos;

/// <summary>
/// Cálculos nutricionais. Extraído de <c>ClassesBSFM.CalcularNutricional</c>
/// sem alterar as fórmulas — Harris-Benedict revisada para TMB e fatores de
/// atividade fixos — para que o resultado não mude de valor após a refatoração.
/// </summary>
public static class CalculadoraNutricional
{
    public static double CalcularImc(double pesoKg, double alturaMetros)
    {
        if (alturaMetros <= 0 || pesoKg <= 0) return 0;
        return Math.Round(pesoKg / (alturaMetros * alturaMetros), 2);
    }

    public static double CalcularTmb(string sexo, double pesoKg, double alturaMetros, int idade)
    {
        if (pesoKg <= 0 || alturaMetros <= 0 || idade <= 0) return 0;

        var alturaCm = alturaMetros * 100;

        var tmb = string.Equals(sexo?.Trim(), "Masculino", StringComparison.OrdinalIgnoreCase)
            ? 88.362 + (13.397 * pesoKg) + (4.799 * alturaCm) - (5.677 * idade)
            : 447.593 + (9.247 * pesoKg) + (3.098 * alturaCm) - (4.330 * idade);

        return Math.Round(tmb, 2);
    }

    public static double CalcularGastoTotal(string nivelAtividade, double tmb)
    {
        var nivel = (nivelAtividade ?? string.Empty).Trim().ToLowerInvariant();

        var fator = nivel switch
        {
            var n when n.Contains("sedent") => 1.2,
            var n when n.Contains("ativo") && n.Contains("muito") => 1.725,
            var n when n.Contains("ativo") => 1.55,
            _ => 1.2,
        };

        return Math.Round(tmb * fator, 2);
    }

    /// <summary>Classificação de IMC usada pela OMS.</summary>
    public static string ClassificarImc(double imc) => imc switch
    {
        <= 0 => "Não calculado",
        < 18.5 => "Abaixo do peso",
        < 25 => "Peso adequado",
        < 30 => "Sobrepeso",
        < 35 => "Obesidade grau I",
        < 40 => "Obesidade grau II",
        _ => "Obesidade grau III",
    };

    /// <summary>Meta de água: 35 ml por kg, mínimo de 1,5 L.</summary>
    public static double CalcularAguaAlvoMl(double pesoKg)
        => Math.Max(1500, Math.Round(pesoKg * 35, 0));
}
