using System.Globalization;
using System.Text;

namespace BSFM.Nutricao;

/// <summary>
/// Busca tolerante no catálogo: acento, caixa, plural e erro de digitação.
///
/// Por que isto importa mais que o modelo: o app vai receber "Arroz", "arroz branco",
/// "ARROZ COZIDO" e "aroz". Todos precisam cair no mesmo alimento. Um classificador
/// de imagem nunca resolve isso sozinho — ele devolve um rótulo, que ainda precisa
/// ser resolvido contra uma base.
/// </summary>
public static class BuscaAlimentos
{
    public const double PontuacaoMinimaPadrao = 0.55;

    /// <summary>Normaliza para comparação: sem acento, minúsculo, espaço único.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var construtor = new StringBuilder(decomposto.Length);

        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark)
                continue;

            construtor.Append(char.IsLetterOrDigit(caractere) || char.IsWhiteSpace(caractere) ? caractere : ' ');
        }

        return string.Join(' ', construtor.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string[] Tokens(string normalizado) =>
        normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Redução ingênua de plural — o suficiente para "macas" e "ovos".</summary>
    private static string SemPlural(string token) =>
        token.Length > 3 && token.EndsWith('s') ? token[..^1] : token;

    /// <summary>
    /// Retorna os melhores candidatos acima do mínimo, em ordem decrescente.
    /// Lista vazia significa "não identificado" — e isso é uma resposta válida.
    /// </summary>
    public static IReadOnlyList<ResultadoBusca> Buscar(
        string? consulta,
        IReadOnlyList<AlimentoCatalogo>? catalogo = null,
        int limite = 5,
        double minimo = PontuacaoMinimaPadrao)
    {
        var alvo = Normalizar(consulta);
        if (alvo.Length < 2)
            return Array.Empty<ResultadoBusca>();

        catalogo ??= CatalogoAlimentos.Padrao;
        limite = Math.Clamp(limite, 1, 50);

        var achados = new List<ResultadoBusca>();

        foreach (var alimento in catalogo)
        {
            var melhor = 0.0;
            string? termo = null;

            foreach (var candidato in alimento.Termos())
            {
                var nota = Pontuar(alvo, Normalizar(candidato));
                if (nota > melhor)
                {
                    melhor = nota;
                    termo = candidato;
                }

                if (melhor >= 1.0) break;
            }

            if (melhor >= minimo)
                achados.Add(new ResultadoBusca(alimento, Math.Round(melhor, 3), termo ?? alimento.Nome));
        }

        return achados
            .OrderByDescending(a => a.Pontuacao)
            .ThenByDescending(a => PrioridadeDePreparo(a.Alimento.Nome, a.Alimento.Grupo)
                                   + AjusteDePreferencia(alvo, a.Alimento.Nome))
            .ThenBy(a => a.Alimento.Nome, StringComparer.Ordinal)
            .Take(limite)
            .ToList();
    }

    /// <summary>
    /// Qual variante a pessoa quer dizer quando digita só o nome base.
    ///
    /// A TACO lista "Leite, de cabra" e "Leite, condensado" ao lado de "Leite, de vaca,
    /// integral". Sem esta tabela, a ordenação alfabética entrega o exótico primeiro.
    /// A lista é explícita de propósito: é preferência de produto brasileiro, não
    /// uma heurística escondida — e pode ser revista sem tocar no código de busca.
    /// </summary>
    private static readonly Dictionary<string, string> VariantePreferida = new(StringComparer.Ordinal)
    {
        ["arroz"] = "tipo 1",
        ["feijao"] = "carioca",
        ["leite"] = "vaca",
        ["frango"] = "peito",
        ["ovo"] = "inteiro",
        ["banana"] = "prata",
        ["pao"] = "frances",
        ["carne"] = "patinho",
        ["queijo"] = "minas",
        ["peixe"] = "tilapia",
        ["batata"] = "inglesa",
    };

    /// <summary>Partes isoladas do alimento raramente são o que se quer registrar.</summary>
    private static readonly string[] PartesMenosUteis =
    {
        "clara", "gema", "coracao", "figado", "moela", "bico", "pe ", "ossos",
    };

    private static int AjusteDePreferencia(string consultaNormalizada, string nomeDoAlimento)
    {
        var nome = Normalizar(nomeDoAlimento);
        var ajuste = 0;

        if (VariantePreferida.TryGetValue(consultaNormalizada, out var preferida)
            && nome.Contains(preferida, StringComparison.Ordinal))
            ajuste += 20;

        foreach (var parte in PartesMenosUteis)
            if (nome.Contains(parte, StringComparison.Ordinal)) { ajuste -= 15; break; }

        return ajuste;
    }

    /// <summary>
    /// Desempate quando vários itens casam igual — e na TACO sempre casam vários
    /// (são 20 variedades de arroz, 8 de leite).
    ///
    /// Regras, em ordem de peso:
    ///   1. comida pronta (cozida/grelhada/assada) vence o ingrediente cru;
    ///   2. produto processado (doce, condensado, em barra) perde para o alimento;
    ///   3. descrição mais genérica vence a muito qualificada;
    ///   4. "cru" só penaliza fora de frutas e hortaliças, onde é o estado normal.
    /// </summary>
    internal static int PrioridadeDePreparo(string nome, string? grupo = null)
    {
        var n = Normalizar(nome);
        var g = Normalizar(grupo);
        var prioridade = 0;

        if (n.Contains("cozid")) prioridade += 30;
        if (n.Contains("grelhad") || n.Contains("assad") || n.Contains("refogad")) prioridade += 25;
        if (n.Contains("frit")) prioridade += 10;

        // Produto industrializado/doce raramente é o que a pessoa quer dizer.
        foreach (var termo in new[] { "doce", "condensad", "em barra", "geleia", "compota", "cristalizad", "nectar" })
            if (n.Contains(termo)) { prioridade -= 25; break; }

        if (n.Contains("broto")) prioridade -= 40;
        if (n.Contains("seco") || n.Contains("desidratad")) prioridade -= 20;

        var natural = g.Contains("fruta") || g.Contains("hortalica") || g.Contains("legume") || g.Contains("ovo");
        if (!natural && n.Contains("cru")) prioridade -= 30;

        // Menos vírgulas = descrição mais genérica = mais provável de ser o prato.
        prioridade -= 6 * nome.Count(c => c == ',');

        return prioridade;
    }

    /// <summary>Melhor candidato único, ou nulo quando nada passa do mínimo.</summary>
    public static ResultadoBusca? Melhor(string? consulta,
        IReadOnlyList<AlimentoCatalogo>? catalogo = null,
        double minimo = PontuacaoMinimaPadrao)
        => Buscar(consulta, catalogo, 1, minimo).FirstOrDefault();

    private static double Pontuar(string consulta, string candidato)
    {
        if (consulta.Length == 0 || candidato.Length == 0) return 0;

        if (consulta == candidato) return 1.00;

        // Plural: "macas" casa com "maca".
        if (SemPlural(consulta) == SemPlural(candidato)) return 0.97;

        if (candidato.StartsWith(consulta, StringComparison.Ordinal) ||
            consulta.StartsWith(candidato, StringComparison.Ordinal)) return 0.90;

        if (consulta.Length >= 4 && candidato.Contains(consulta, StringComparison.Ordinal)) return 0.80;

        var tokensConsulta = Tokens(consulta).Select(SemPlural).ToArray();
        var tokensCandidato = Tokens(candidato).Select(SemPlural).ToArray();

        // Todos os termos da consulta aparecem no candidato.
        if (tokensConsulta.All(t => tokensCandidato.Contains(t))) return 0.75;

        var intersecao = tokensConsulta.Intersect(tokensCandidato).Count();
        var uniao = tokensConsulta.Union(tokensCandidato).Count();
        var jaccard = uniao == 0 ? 0 : (double)intersecao / uniao;

        // Similaridade de edição cobre erro de digitação ("aroz" x "arroz").
        var similaridade = Similaridade(consulta, candidato);

        var notaTokens = jaccard > 0 ? 0.45 + (0.30 * jaccard) : 0;
        var notaEdicao = similaridade >= 0.75 ? 0.70 * similaridade : 0;

        return Math.Max(notaTokens, notaEdicao);
    }

    /// <summary>Similaridade 0..1 baseada em distância de Levenshtein.</summary>
    public static double Similaridade(string a, string b)
    {
        if (a == b) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;

        var distancia = Levenshtein(a, b);
        var maior = Math.Max(a.Length, b.Length);
        return 1.0 - ((double)distancia / maior);
    }

    private static int Levenshtein(string a, string b)
    {
        var anterior = new int[b.Length + 1];
        var atual = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) anterior[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            atual[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var custo = a[i - 1] == b[j - 1] ? 0 : 1;
                atual[j] = Math.Min(Math.Min(atual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + custo);
            }

            (anterior, atual) = (atual, anterior);
        }

        return anterior[b.Length];
    }
}
