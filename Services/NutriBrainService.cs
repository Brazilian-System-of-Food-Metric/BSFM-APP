using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BSFM.CoreAnalytics.Backend.Services
{
    /// <summary>
    /// NutriBrainService
    /// 
    /// Motor de inferência que integra com a API do Groq (Llama 3).
    /// 
    /// Por que Groq?
    /// - Velocidade: ~200-800ms por inferência (vs 3-8s de APIs multimodais)
    /// - Custo: Modelo Llama 3 8B é extremamente barato por token
    /// - Rate Limits: ~30 req/s (vs 3-10 req/min de APIs de visão)
    /// - Precisão: Llama 3 70B tem performance comparável ao GPT-3.5 em análise textual
    /// 
    /// Endpoint: https://api.groq.com/openai/v1/chat/completions
    /// Modelo: llama3-70b-8192 (ou llama3-8b-8192 para economia)
    /// </summary>
    public class NutriBrainService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NutriBrainService> _logger;
        private readonly string _apiKey;
        
        // Groq API Configuration
        private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
        private const string ModeloPadrao = "llama3-70b-8192"; // 70B para maior precisão
        // Alternativa: "llama3-8b-8192" (8B, mais rápido e barato)
        
        // Timeout generoso para o Groq processar
        private static readonly TimeSpan GroqTimeout = TimeSpan.FromSeconds(15);

        public NutriBrainService(HttpClient httpClient, ILogger<NutriBrainService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") 
                      ?? throw new InvalidOperationException(
                          "GROQ_API_KEY não configurada. Defina a variável de ambiente no Render.");
        }

        /// <summary>
        /// Analisa o texto OCR de um rótulo usando o Groq Llama 3
        /// </summary>
        public async Task<RotuloResponse> AnalisarRotuloAsync(
            string textoOcr, 
            string systemPrompt,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("[NUTRIBRAIN] Iniciando análise de rótulo...");
                
                // 1. Monta o payload para a API do Groq
                var payload = new GroqChatRequest
                {
                    Model = ModeloPadrao,
                    Messages = new List<GroqMessage>
                    {
                        new GroqMessage 
                        { 
                            Role = "system", 
                            Content = systemPrompt 
                        },
                        new GroqMessage
                        {
                            Role = "user",
                            Content = $"## TEXTO OCR DO RÓTULO:\n\n{textoOcr}\n\nAnalise este rótulo e retorne APENAS o JSON no formato especificado."
                        }
                    },
                    Temperature = 0.1,  // Baixa temperatura = respostas mais determinísticas
                    MaxTokens = 1024,
                    ResponseFormat = new GroqResponseFormat { Type = "json_object" }
                };

                // 2. Serializa e envia a requisição
                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                _logger.LogDebug("[NUTRIBRAIN] Payload: {Payload}", jsonPayload);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(GroqTimeout);

                var response = await _httpClient.SendAsync(requestMessage, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[NUTRIBRAIN] Groq API error: {StatusCode} - {Body}", 
                        response.StatusCode, responseBody);
                    throw new HttpRequestException(
                        $"Groq API retornou {response.StatusCode}: {responseBody}");
                }

                // 3. Desserializa a resposta do Groq
                var groqResponse = JsonSerializer.Deserialize<GroqChatResponse>(responseBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var content = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrEmpty(content))
                {
                    throw new InvalidOperationException("Groq retornou resposta vazia.");
                }

                _logger.LogInformation("[NUTRIBRAIN] Resposta recebida em {Tempo}ms", 
                    groqResponse?.Usage?.TotalTime ?? 0);

                // 4. Extrai o JSON da resposta (segurança contra texto extra)
                var jsonLimpo = ExtrairJsonDaResposta(content);

                // 5. Desserializa para o modelo de resposta
                var resultado = JsonSerializer.Deserialize<RotuloResponse>(jsonLimpo, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (resultado == null)
                {
                    throw new InvalidOperationException("Falha ao desserializar resposta do Groq.");
                }

                // 6. Validação dos campos obrigatórios
                resultado.ProdutoDetectado ??= "Produto não identificado";
                resultado.AnaliseEmRelacaoAMeta ??= "Análise não disponível.";
                resultado.DicaBSFM ??= "Consulte um nutricionista para orientação personalizada.";

                _logger.LogInformation("[NUTRIBRAIN] Análise concluída: {Produto}", 
                    resultado.ProdutoDetectado);

                return resultado;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[NUTRIBRAIN] Timeout na requisição ao Groq.");
                return new RotuloResponse
                {
                    ProdutoDetectado = "Tempo limite excedido",
                    PodeConsumir = null,
                    PontuacaoSaude = 0,
                    AnaliseEmRelacaoAMeta = "A análise excedeu o tempo limite. Tente novamente.",
                    DicaBSFM = "Verifique sua conexão com a internet e tente novamente."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NUTRIBRAIN] Erro ao analisar rótulo.");
                throw;
            }
        }

        /// <summary>
        /// Extrai um JSON válido da resposta do LLM, mesmo que venha com texto extra
        /// </summary>
        private string ExtrairJsonDaResposta(string rawContent)
        {
            // Remove blocos de código markdown ```json ... ```
            var cleaned = rawContent.Trim();
            if (cleaned.StartsWith("```"))
            {
                var start = cleaned.IndexOf('\n') + 1;
                var end = cleaned.LastIndexOf("```");
                if (end > start)
                    cleaned = cleaned[start..end].Trim();
            }

            // Encontra o primeiro { e o último }
            var firstBrace = cleaned.IndexOf('{');
            var lastBrace = cleaned.LastIndexOf('}');
            
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleaned = cleaned[firstBrace..(lastBrace + 1)];
            }

            return cleaned;
        }
    }

    // ====== MODELOS DE RESPOSTA ======

    /// <summary>
    /// Modelo de resposta da análise de rótulo.
    /// O Groq é FORÇADO a retornar APENAS este JSON, sem texto adicional.
    /// </summary>
    public class RotuloResponse
    {
        [JsonPropertyName("ProdutoDetectado")]
        public string? ProdutoDetectado { get; set; }

        [JsonPropertyName("PodeConsumir")]
        public bool? PodeConsumir { get; set; }

        [JsonPropertyName("PontuacaoSaude")]
        public int PontuacaoSaude { get; set; }

        [JsonPropertyName("AnaliseEmRelacaoAMeta")]
        public string? AnaliseEmRelacaoAMeta { get; set; }

        [JsonPropertyName("DicaBSFM")]
        public string? DicaBSFM { get; set; }
    }

    // ====== REQUEST MODELS ======

    public class GroqChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "llama3-70b-8192";

        [JsonPropertyName("messages")]
        public List<GroqMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.1;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 1024;

        [JsonPropertyName("response_format")]
        public GroqResponseFormat? ResponseFormat { get; set; }
    }

    public class GroqMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class GroqResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";
    }

    // ====== RESPONSE MODELS ======

    public class GroqChatResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<GroqChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public GroqUsage? Usage { get; set; }
    }

    public class GroqChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public GroqResponseMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    public class GroqResponseMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    public class GroqUsage
    {
        [JsonPropertyName("queue_time")]
        public long QueueTime { get; set; }

        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("total_time")]
        public long TotalTime { get; set; }
    }
}
