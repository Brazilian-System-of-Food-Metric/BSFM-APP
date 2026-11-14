namespace BSFM.Nutricao;

/// <summary>
/// Catálogo nutricional local.
///
/// Base inicial: alimentos brasileiros comuns, valores por 100 g de referência
/// (TACO — Tabela Brasileira de Composição de Alimentos), **arredondados**.
/// Servem para o app funcionar offline e para dar ordem de grandeza correta.
///
/// A fonte autoritativa continua sendo a planilha oficial da TACO: use
/// <see cref="CarregarCsv"/> para substituir ou completar esta base com os
/// valores exatos, sem recompilar.
///
/// Motivo de existir: o modelo de visão identifica um rótulo ("arroz branco");
/// quem transforma rótulo em número é o catálogo. Sem isto, melhorar o modelo
/// não melhora a nutrição mostrada.
/// </summary>
public static class CatalogoAlimentos
{
    private static readonly Lazy<IReadOnlyList<AlimentoCatalogo>> Semente = new(MontarSemente);

    /// <summary>Catálogo em memória (semente + eventuais cargas via CSV).</summary>
    public static IReadOnlyList<AlimentoCatalogo> Padrao => Semente.Value;

    /// <summary>
    /// Lê um CSV no formato <c>nome;grupo;kcal;proteina;carboidrato;gordura;fibra;sinonimos</c>
    /// (separador <c>;</c>, sinônimos separados por <c>|</c>).
    /// Linhas inválidas são ignoradas — importar 600 linhas não deve falhar por uma.
    /// </summary>
    public static IReadOnlyList<AlimentoCatalogo> CarregarCsv(IEnumerable<string> linhas)
    {
        var lista = new List<AlimentoCatalogo>();

        foreach (var linha in linhas)
        {
            if (string.IsNullOrWhiteSpace(linha) || linha.TrimStart().StartsWith('#'))
                continue;

            var campos = linha.Split(';');
            if (campos.Length < 7) continue;

            if (!double.TryParse(campos[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var kcal)) continue;
            if (!double.TryParse(campos[3], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var proteina)) continue;
            if (!double.TryParse(campos[4], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var carbo)) continue;
            if (!double.TryParse(campos[5], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var gordura)) continue;
            if (!double.TryParse(campos[6], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var fibra)) continue;

            var nome = campos[0].Trim();
            if (nome.Length < 2) continue;

            var sinonimos = campos.Length > 7
                ? campos[7].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            lista.Add(new AlimentoCatalogo(nome, campos[1].Trim(),
                new NutrientesPor100g(kcal, proteina, carbo, gordura, fibra), sinonimos));
        }

        return lista;
    }

    private static IReadOnlyList<AlimentoCatalogo> MontarSemente()
    {
        AlimentoCatalogo A(string nome, string grupo, double k, double p, double c, double g, double f, params string[] s) =>
            new(nome, grupo, new NutrientesPor100g(k, p, c, g, f), s);

        return new List<AlimentoCatalogo>
        {
            // Cereais e massas
            A("Arroz branco cozido", "Cereais", 128, 2.5, 28.1, 0.2, 1.6, "arroz", "arroz branco", "arroz cozido"),
            A("Arroz integral cozido", "Cereais", 124, 2.6, 25.8, 1.0, 2.7, "arroz integral"),
            A("Macarrao cozido", "Cereais", 158, 5.8, 30.7, 0.9, 1.8, "macarrao", "massa", "espaguete"),
            A("Pao frances", "Cereais", 300, 8.0, 58.6, 3.1, 2.3, "pao", "pao de sal", "paozinho"),
            A("Pao de forma integral", "Cereais", 253, 9.4, 49.9, 3.7, 6.9, "pao integral", "pao de forma"),
            A("Aveia em flocos", "Cereais", 389, 16.9, 66.2, 6.9, 10.6, "aveia", "flocos de aveia"),
            A("Farinha de mandioca", "Cereais", 365, 1.2, 87.9, 0.3, 6.4, "farinha", "farofa"),

            // Leguminosas
            A("Feijao carioca cozido", "Leguminosas", 76, 4.8, 13.6, 0.5, 8.5, "feijao", "feijao carioca"),
            A("Feijao preto cozido", "Leguminosas", 77, 4.5, 14.0, 0.5, 8.4, "feijao preto"),
            A("Lentilha cozida", "Leguminosas", 93, 6.3, 16.3, 0.5, 7.9, "lentilha"),
            A("Grao de bico cozido", "Leguminosas", 130, 7.0, 21.2, 2.1, 6.0, "grao de bico"),

            // Carnes e ovos
            A("Frango peito sem pele grelhado", "Carnes", 159, 32.0, 0, 2.5, 0, "frango", "peito de frango", "frango grelhado"),
            A("Frango coxa sem pele cozida", "Carnes", 167, 26.9, 0, 6.5, 0, "coxa de frango", "sobrecoxa"),
            A("Carne bovina patinho grelhado", "Carnes", 219, 32.0, 0, 9.0, 0, "carne", "bife", "patinho", "carne bovina"),
            A("Carne bovina moida cozida", "Carnes", 212, 26.0, 0, 11.0, 0, "carne moida", "moida"),
            A("Ovo de galinha cozido", "Ovos", 146, 13.3, 0.6, 9.5, 0, "ovo", "ovos cozidos"),
            A("Ovo frito", "Ovos", 240, 15.6, 0.7, 18.6, 0, "ovo frito"),
            A("Tilapia grelhada", "Peixes", 128, 26.0, 0, 2.7, 0, "tilapia", "peixe", "file de peixe"),
            A("Salmao grelhado", "Peixes", 229, 23.0, 0, 14.0, 0, "salmao"),
            A("Atum em lata", "Peixes", 116, 26.0, 0, 1.0, 0, "atum", "atum enlatado"),

            // Laticinios
            A("Leite integral", "Laticinios", 61, 3.2, 4.7, 3.3, 0, "leite", "leite de vaca"),
            A("Iogurte natural", "Laticinios", 51, 4.1, 3.6, 1.9, 0, "iogurte"),
            A("Queijo minas frescal", "Laticinios", 264, 17.4, 3.2, 20.2, 0, "queijo", "queijo minas", "queijo branco"),

            // Fruits
            A("Banana prata", "Frutas", 98, 1.3, 26.0, 0.1, 2.0, "banana"),
            A("Maca com casca", "Frutas", 52, 0.2, 13.9, 0.2, 2.4, "maca", "maca fuji"),
            A("Laranja pera", "Frutas", 47, 0.9, 11.8, 0.1, 2.4, "laranja"),
            A("Mamao papaia", "Frutas", 43, 0.5, 10.8, 0.1, 1.8, "mamao"),
            A("Manga palmer", "Frutas", 60, 0.8, 15.2, 0.4, 1.6, "manga"),
            A("Morango", "Frutas", 32, 0.7, 7.7, 0.3, 2.0, "morangos"),
            A("Abacaxi", "Frutas", 50, 0.5, 13.1, 0.1, 1.4, "abacaxi"),
            A("Abacate", "Frutas", 160, 2.0, 8.5, 14.7, 6.7, "avocado"),
            A("Uva italia", "Frutas", 69, 0.7, 18.1, 0.2, 0.9, "uva"),

            // Raizes e tuberculos
            A("Batata doce cozida", "Raizes", 86, 1.6, 20.1, 0.1, 3.0, "batata doce"),
            A("Batata inglesa cozida", "Raizes", 86, 2.0, 19.9, 0.1, 2.2, "batata", "batata cozida", "batata inglesa"),
            A("Mandioca cozida", "Raizes", 125, 0.6, 30.1, 0.3, 1.6, "mandioca", "aipim", "macaxeira"),
            A("Pure de batata", "Raizes", 88, 1.9, 17.0, 1.2, 1.5, "pure"),

            // Verduras e legumes
            A("Alface crespa", "Verduras", 15, 1.3, 2.0, 0.2, 1.8, "alface", "salada"),
            A("Tomate", "Legumes", 18, 0.9, 3.9, 0.2, 1.2, "tomates"),
            A("Cenoura crua", "Legumes", 41, 0.9, 9.6, 0.2, 3.2, "cenoura"),
            A("Brocolis cozido", "Verduras", 35, 2.4, 7.2, 0.4, 3.3, "brocolis"),
            A("Couve refogada", "Verduras", 90, 2.9, 8.7, 5.1, 5.0, "couve", "couve manteiga"),
            A("Beterraba cozida", "Legumes", 44, 1.6, 9.9, 0.1, 3.4, "beterraba"),
            A("Abobrinha cozida", "Legumes", 19, 1.1, 4.3, 0.2, 1.6, "abobrinha"),

            // Gorduras e oleaginosas
            A("Azeite de oliva", "Oleos", 884, 0, 0, 100, 0, "azeite", "oleo de oliva"),
            A("Oleo de soja", "Oleos", 884, 0, 0, 100, 0, "oleo"),
            A("Manteiga com sal", "Oleos", 726, 0.4, 0.1, 82.4, 0, "manteiga"),
            A("Castanha de caju torrada", "Oleaginosas", 570, 18.5, 29.1, 46.3, 3.7, "castanha", "castanha de caju"),
            A("Pasta de amendoim", "Oleaginosas", 588, 25.0, 20.0, 50.0, 6.0, "amendoim", "pasta de amendoim"),

            // Bebidas e doces
            A("Suco de laranja natural", "Bebidas", 45, 0.7, 11.2, 0.1, 0.2, "suco", "suco de laranja"),
            A("Cafe infusao 10%", "Bebidas", 9, 0.9, 1.5, 0, 0, "cafe", "cafe preto", "cafezinho"),
            A("Refrigerante cola", "Bebidas", 42, 0, 10.6, 0, 0, "refrigerante", "coca", "coca-cola", "refri"),
            A("Acucar cristal", "Doces", 387, 0, 99.9, 0, 0, "acucar", "acucar refinado"),
            A("Chocolate ao leite", "Doces", 540, 7.2, 59.6, 30.3, 1.7, "chocolate", "barra de chocolate"),
            A("Bolo de chocolate", "Doces", 410, 6.0, 55.0, 18.0, 2.0, "bolo", "bolo de chocolate"),
        };
    }
}
