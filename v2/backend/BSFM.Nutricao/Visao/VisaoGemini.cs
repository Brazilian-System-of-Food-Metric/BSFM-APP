using System.Net.Http.Json;
using System.Text.Json;

namespace BSFM.Nutricao.Visao;

/// <summary>
/// Reconhecimento por visão multimodal (Google Gemini).
///
/// Por que trocar o classificador caseiro: reconhecer comida exige separar
/// "arroz com feijão e bife" em três itens e nomear cada um em português. Um YOLO
/// treinado com poucas centenas de imagens não chega lá — e o limiar de 0,16
/// usado no app atual só disfarça isso com falsos positivos. Um modelo multimodal
/// pré-treinado resolve sem treino e sem GPU, dentro do nível gratuito.
///
/// O contrato com o resto do sistema é o mesmo: nome + confiança, ou "não sei".
/// </summary>
public sealed class VisaoGemini : IVisaoAlimento
{
    private const string Prompt = """
        Você é um nutricionista identificando alimentos em uma foto.
        Responda APENAS com um objeto JSON, sem markdown, no formato:
        {"alimento":"nome em portugues do Brasil","confianca":0.0,"porcao_estimada_g":0}
        Regras:
        - Se houver vários alimentos, escolha o principal (maior área).
        - Use nomes simples e comuns no Brasil ("arroz branco", "feijão carioca", "frango grelhado").
        - Se não for possível identificar comida, responda {"alimento":null,"confianca":0}.
        - confianca entre 0 e 1; seja honesto, não infle o valor.
        """;

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _modelo;

    public VisaoGemini(HttpClient http, string? apiKey, string? modelo = null)
    {
        _http = http;
        _apiKey = apiKey ?? string.Empty;
        _modelo = string.IsNullOrWhiteSpace(modelo) ? "gemini-2.5-flash" : modelo;
    }

    public string Identificacao => "gemini";

    public bool Disponivel => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<Reconhecimento> ReconhecerAsync(
        ReadOnlyMemory<byte> imagem, string? dica, CancellationToken ct = default)
    {
        if (!Disponivel)
            return Reconhecimento.NaoIdentificado(Identificacao, "chave_api_ausente");

        if (imagem.Length == 0)
            return Reconhecimento.NaoIdentificado(Identificacao, "imagem_vazia");

        if (imagem.Length > 6 * 1024 * 1024)
            return Reconhecimento.NaoIdentificado(Identificacao, "imagem_acima_do_limite");

        var instrucao = string.IsNullOrWhiteSpace(dica) ? Prompt : $"{Prompt}\nDica do usuário: {dica}";

        var corpo = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = instrucao },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = DetectarMime(imagem.Span),
                                data = Convert.ToBase64String(imagem.Span),
                            },
                        },
                    },
                },
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 200,
                responseMimeType = "application/json",
            },
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelo}:generateContent?key={_apiKey}";

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.PostAsJsonAsync(url, corpo, ct);
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException)
        {
            return Reconhecimento.NaoIdentificado(Identificacao, $"falha_de_rede:{excecao.GetType().Name}");
        }

        using (resposta)
        {
            if (!resposta.IsSuccessStatusCode)
                return Reconhecimento.NaoIdentificado(Identificacao, $"http_{(int)resposta.StatusCode}");

            var texto = await ExtrairTextoAsync(resposta, ct);
            if (texto is null)
                return Reconhecimento.NaoIdentificado(Identificacao, "resposta_sem_texto");

            return Interpretar(texto);
        }
    }

    /// <summary>Navega o envelope do Gemini e devolve o texto do primeiro candidato.</summary>
    private static async Task<string?> ExtrairTextoAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
        using var documento = await JsonDocument.ParseAsync(fluxo, default, ct);

        if (!documento.RootElement.TryGetProperty("candidates", out var candidatos) ||
            candidatos.ValueKind != JsonValueKind.Array || candidatos.GetArrayLength() == 0)
            return null;

        var primeiro = candidatos[0];
        if (!primeiro.TryGetProperty("content", out var conteudo) ||
            !conteudo.TryGetProperty("parts", out var partes) ||
            partes.ValueKind != JsonValueKind.Array || partes.GetArrayLength() == 0)
            return null;

        return partes[0].TryGetProperty("text", out var texto) ? texto.GetString() : null;
    }

    /// <summary>
    /// Interpreta o JSON do modelo com tolerância: remove cerca de markdown,
    /// valida faixa de confiança e trata nome nulo como "não identificado".
    /// </summary>
    public static Reconhecimento Interpretar(string texto)
    {
        var limpo = texto.Trim();

        if (limpo.StartsWith("```", StringComparison.Ordinal))
        {
            var inicio = limpo.IndexOf('\n');
            if (inicio >= 0) limpo = limpo[(inicio + 1)..];
            var fim = limpo.LastIndexOf("```", StringComparison.Ordinal);
            if (fim >= 0) limpo = limpo[..fim];
        }

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(limpo.Trim());
        }
        catch (JsonException)
        {
            return Reconhecimento.NaoIdentificado("gemini", "json_invalido");
        }

        using (documento)
        {
            var raiz = documento.RootElement;

            if (!raiz.TryGetProperty("alimento", out var nome) || nome.ValueKind != JsonValueKind.String)
                return Reconhecimento.NaoIdentificado("gemini", "sem_alimento");

            var valor = nome.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(valor))
                return Reconhecimento.NaoIdentificado("gemini", "alimento_vazio");

            var confianca = raiz.TryGetProperty("confianca", out var c) && c.TryGetDouble(out var d) ? d : 0;
            confianca = Math.Clamp(confianca, 0, 1);

            if (confianca <= 0)
                return Reconhecimento.NaoIdentificado("gemini", "confianca_zero");

            return new Reconhecimento(true, valor, confianca, "gemini");
        }
    }

    /// <summary>Assinatura de arquivo — o CDP e o upload nem sempre informam o mime.</summary>
    private static string DetectarMime(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";

        return "image/jpeg";
    }
}
