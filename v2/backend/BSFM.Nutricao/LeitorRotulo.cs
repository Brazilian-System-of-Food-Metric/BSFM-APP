using System.Globalization;
using System.Text.RegularExpressions;

namespace BSFM.Nutricao;

/// <summary>
/// Lê o texto de um rótulo nutricional (já extraído por OCR no cliente) e devolve
/// os valores por 100 g.
///
/// Por que no servidor: o OCR é caro e falha de formas diferentes em cada aparelho.
/// Centralizar a interpretação aqui garante que o mesmo rótulo produza o mesmo
/// número em qualquer dispositivo — e permite corrigir o parser sem republicar o app.
///
/// Não inventa: quando um campo não aparece no texto, ele fica nulo e a confiança cai.
/// </summary>
public static class LeitorRotulo
{
    public sealed record Leitura(
        double? Kcal,
        double? Proteina,
        double? Carboidrato,
        double? Gordura,
        double? Fibra,
        double? Sodio,
        double? GramasDaPorcao,
        double Confianca,
        IReadOnlyList<string> CamposEncontrados,
        string? Aviso);

    private static readonly Regex Porcao = new(
        @"por[çc][ãa]o\s*(?:de\s*)?([\d]+(?:[.,]\d+)?)\s*(g|ml)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CemGramas = new(
        @"(?:por\s*)?100\s*(g|ml)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Leitura Ler(string? textoOcr)
    {
        if (string.IsNullOrWhiteSpace(textoOcr))
            return new Leitura(null, null, null, null, null, null, null, 0, Array.Empty<string>(), "texto_vazio");

        var texto = Normalizar(textoOcr);

        var gramas = 100.0;
        var aviso = (string?)null;

        var mPorcao = Porcao.Match(texto);
        if (mPorcao.Success && double.TryParse(Decimal(mPorcao.Groups[1].Value), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var informado) && informado > 0)
        {
            gramas = informado;
        }
        else if (!CemGramas.IsMatch(texto))
        {
            aviso = "porcao_nao_identificada_assumido_100g";
        }

        var kcal = Capturar(texto, @"energia\s*\(?kcal\)?[^\d]{0,12}([\d.,]+)");
        kcal ??= Capturar(texto, @"([\d.,]+)\s*kcal");

        var proteina = Capturar(texto, @"prote[íi]nas?[^\d]{0,16}([\d.,]+)");
        var carboidrato = Capturar(texto, @"carboidratos?[^\d]{0,16}([\d.,]+)");
        var gordura = Capturar(texto, @"(?:gorduras?\s*(?:totais)?|lip[íi]deos?)[^\d]{0,16}([\d.,]+)");
        var fibra = Capturar(texto, @"fibras?\s*(?:alimentar)?[^\d]{0,16}([\d.,]+)");
        var sodio = Capturar(texto, @"s[óo]dio[^\d]{0,16}([\d.,]+)");

        // A tabela costuma ser por porção; o app trabalha por 100 g.
        double? PorCem(double? valor) => valor is null ? null : Math.Round(valor.Value * 100 / gramas, 2);

        var k = PorCem(kcal);
        var p = PorCem(proteina);
        var c = PorCem(carboidrato);
        var g = PorCem(gordura);
        var f = PorCem(fibra);
        var s = PorCem(sodio);

        var encontrados = new List<string>();
        if (k is not null) encontrados.Add("kcal");
        if (p is not null) encontrados.Add("proteina");
        if (c is not null) encontrados.Add("carboidrato");
        if (g is not null) encontrados.Add("gordura");
        if (f is not null) encontrados.Add("fibra");
        if (s is not null) encontrados.Add("sodio");

        // Confiança = fração dos quatro campos essenciais encontrados.
        var essenciais = new[] { k, p, c, g }.Count(v => v is not null);
        var confianca = Math.Round(essenciais / 4.0, 2);

        return new Leitura(k, p, c, g, f, s, gramas, confianca, encontrados, aviso);
    }

    /// <summary>Normaliza o texto do OCR: acentos preservados, espaços e quebras colapsados.</summary>
    private static string Normalizar(string texto) =>
        Regex.Replace(texto.Replace('\u00A0', ' ').Replace("\r", " ").Replace("\n", " "), @"\s{2,}", " ").Trim();

    private static string Decimal(string valor) => valor.Replace(".", "").Replace(',', '.');

    private static double? Capturar(string texto, string padrao)
    {
        var m = Regex.Match(texto, padrao, RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        if (!double.TryParse(Decimal(m.Groups[1].Value), NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
            return null;

        return valor >= 0 && valor < 100_000 ? valor : null;
    }
}
