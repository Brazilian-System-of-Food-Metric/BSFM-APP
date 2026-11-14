namespace BSFM.Nutricao;

/// <summary>
/// Feedback nutricional de uma refeição, por regras.
///
/// A v1 usava Groq (LLM) aqui. Sem chave de LLM disponível, a alternativa honesta
/// é um motor de regras explícito: mesmo resultado previsível, auditável e sem
/// custo por token. O texto é gerado a partir de faixas, não "inventado".
///
/// Limitação declarada: não interpreta ingredientes nem substituições — só os
/// números da refeição contra a meta e o perfil de saúde.
/// </summary>
public static class AnalisadorRefeicao
{
    public sealed record Entrada(
        double Kcal,
        double Proteina,
        double Carboidrato,
        double Gordura,
        double? Fibra,
        double MetaKcal,
        double ConsumidoKcal,
        string? Diabetes,
        string? Intolerancia);

    public sealed record Resultado(
        string Classificacao,
        int Pontuacao,
        string Mensagem,
        IReadOnlyList<string> Dicas);

    public static Resultado Avaliar(Entrada entrada)
    {
        var dicas = new List<string>();
        var pontuacao = 6;

        var kcal = Math.Max(0, entrada.Kcal);
        var meta = entrada.MetaKcal > 0 ? entrada.MetaKcal : 2000;
        var fracaoDoDia = kcal / meta;

        // --- distribuição no dia -------------------------------------------------
        if (fracaoDoDia > 0.45)
        {
            pontuacao -= 2;
            dicas.Add("Esta refeição consome quase metade da meta diária. Considere reduzir a porção.");
        }
        else if (fracaoDoDia < 0.10 && kcal > 0)
        {
            dicas.Add("Refeição leve. Se for substituir uma refeição principal, inclua uma fonte de proteína.");
        }

        // --- composição ----------------------------------------------------------
        var kcalProteina = entrada.Proteina * 4;
        var kcalCarbo = entrada.Carboidrato * 4;
        var kcalGordura = entrada.Gordura * 9;
        var totalMacros = kcalProteina + kcalCarbo + kcalGordura;

        if (totalMacros > 0)
        {
            var fracaoGordura = kcalGordura / totalMacros;
            var fracaoCarbo = kcalCarbo / totalMacros;

            if (fracaoGordura > 0.45)
            {
                pontuacao -= 2;
                dicas.Add("Gordura acima de 45% das calorias. Prefira preparos assados ou grelhados.");
            }

            if (entrada.Proteina >= 20 && fracaoGordura <= 0.45)
            {
                pontuacao += 2;
                dicas.Add("Boa quantidade de proteína — ajuda na saciedade.");
            }
            else if (entrada.Proteina < 8 && kcal > 200)
            {
                dicas.Add("Pouca proteína para o tamanho da refeição. Um ovo, queijo branco ou leguminosas resolvem.");
            }

            if (entrada.Fibra is { } fibra)
            {
                if (fibra >= 5)
                {
                    pontuacao += 1;
                    dicas.Add("Boa fonte de fibra.");
                }
                else if (fibra < 2 && kcal > 250)
                {
                    dicas.Add("Pouca fibra. Fruta, salada ou grão inteiro aumentam a saciedade.");
                }
            }

            // --- perfil de saúde -------------------------------------------------
            var temDiabetes = !string.IsNullOrWhiteSpace(entrada.Diabetes)
                && entrada.Diabetes is not ("Não" or "Não informado" or "Nao");

            if (temDiabetes && fracaoCarbo > 0.6)
            {
                pontuacao -= 2;
                dicas.Add("Com diabetes, carboidrato acima de 60% das calorias pede atenção. Combine com proteína e fibra.");
            }

            if (!string.IsNullOrWhiteSpace(entrada.Intolerancia)
                && entrada.Intolerancia.Contains("lactose", StringComparison.OrdinalIgnoreCase)
                && kcalGordura / Math.Max(1, kcal) > 0.35)
            {
                dicas.Add("Perfil indica intolerância à lactose: confira se a receita leva leite, queijo ou creme.");
            }
        }

        // --- contexto do dia -----------------------------------------------------
        var restante = meta - entrada.ConsumidoKcal;
        if (restante > 0 && kcal > restante)
        {
            dicas.Add($"Este registro passa da meta do dia em {Math.Round(kcal - restante)} kcal.");
        }

        pontuacao = Math.Clamp(pontuacao, 0, 10);

        var classificacao = pontuacao switch
        {
            >= 8 => "adequado",
            >= 5 => "atencao",
            _ => "evitar",
        };

        var mensagem = classificacao switch
        {
            "adequado" => "Refeição equilibrada para a sua meta.",
            "atencao" => "Refeição aceitável, com pontos a ajustar.",
            _ => "Refeição com composição distante do ideal para a sua meta.",
        };

        return new Resultado(classificacao, pontuacao, mensagem, dicas);
    }
}
