using System.Text.RegularExpressions;

namespace BSFM.Nutricao;

/// <summary>
/// Extrai refeições de um texto de prescrição nutricional (já passado por OCR).
///
/// A v1 fazia isso com `OcrNutricionalService` + LLM para "entender" o plano.
/// Aqui o recorte é determinístico: procura cabeçalhos de refeição conhecidos e
/// agrupa as linhas seguintes como itens.
///
/// Limitação declarada: não estima calorias por conta própria. Se o texto trouxer
/// "kcal", o valor é aproveitado; caso contrário fica nulo e o app pede o número.
/// </summary>
public static class LeitorPrescricao
{
    public sealed record Item(string Descricao, double? Kcal);

    public sealed record Refeicao(string Nome, IReadOnlyList<Item> Itens);

    public sealed record Resultado(IReadOnlyList<Refeicao> Refeicoes, int TotalItens, string? Aviso);

    private static readonly (string Chave, string Nome)[] Cabecalhos =
    {
        ("cafe da manha", "Cafe"),
        ("café da manhã", "Cafe"),
        ("desjejum", "Cafe"),
        ("colacao", "Lanche"),
        ("colação", "Lanche"),
        ("almoco", "Almoco"),
        ("almoço", "Almoco"),
        ("jantar", "Jantar"),
        ("ceia", "Lanche"),
        ("lanche", "Lanche"),
    };

    private static readonly Regex Marcador = new(@"^\s*(?:[-•*·]|\d+[.)])\s*", RegexOptions.Compiled);
    private static readonly Regex KcalNoItem = new(@"([\d.,]+)\s*kcal", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Resultado Ler(string? textoOcr)
    {
        if (string.IsNullOrWhiteSpace(textoOcr))
            return new Resultado(Array.Empty<Refeicao>(), 0, "texto_vazio");

        var linhas = textoOcr
            .Replace("\r", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var refeicoes = new List<Refeicao>();
        string? atual = null;
        var itens = new List<Item>();

        void Fechar()
        {
            if (atual is null) return;
            refeicoes.Add(new Refeicao(atual, itens.ToList()));
            itens.Clear();
        }

        foreach (var linha in linhas)
        {
            var minuscula = linha.ToLowerInvariant();
            var cabecalho = Cabecalhos.FirstOrDefault(c => minuscula.Contains(c.Chave));

            // Cabeçalho só quando a linha é curta: evita casar "almoço" no meio de uma frase.
            var ehCabecalho = cabecalho.Nome is not null
                && linha.Length <= 40
                && minuscula.IndexOf(cabecalho.Chave, StringComparison.Ordinal) <= 12;

            if (ehCabecalho)
            {
                Fechar();
                atual = cabecalho.Nome;
                continue;
            }

            if (atual is null) continue;

            var descricao = Marcador.Replace(linha, string.Empty).Trim();
            if (descricao.Length < 2) continue;

            var m = KcalNoItem.Match(descricao);
            double? kcal = null;
            if (m.Success && double.TryParse(m.Groups[1].Value.Replace(",", "."),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var valor))
            {
                kcal = valor;
                descricao = KcalNoItem.Replace(descricao, string.Empty).Trim(' ', '-', '–', ':');
            }

            itens.Add(new Item(descricao, kcal));
        }

        Fechar();

        var total = refeicoes.Sum(r => r.Itens.Count);
        var aviso = refeicoes.Count == 0 ? "nenhuma_refeicao_identificada" : null;

        return new Resultado(refeicoes, total, aviso);
    }
}
