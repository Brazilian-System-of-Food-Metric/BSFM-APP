using System.Net.Http.Json;
using System.Text.Json;

namespace BSFM.Api.Servicos;

/// <summary>
/// Envio do código de verificação pelo Brevo (API transacional).
///
/// Sem <c>BREVO_API_KEY</c> o serviço usa o enviador de console — o app continua
/// funcionando em desenvolvimento. Em produção, a ausência da chave é registrada
/// de forma explícita no log, porque o sintoma para o usuário é "o código não chegou".
/// </summary>
public sealed class EnviadorCodigoBrevo : IEnviadorCodigo
{
    private const string Url = "https://api.brevo.com/v3/smtp/email";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _remetenteEmail;
    private readonly string _remetenteNome;
    private readonly ILogger<EnviadorCodigoBrevo> _log;

    public EnviadorCodigoBrevo(
        HttpClient http,
        string apiKey,
        string remetenteEmail,
        string remetenteNome,
        ILogger<EnviadorCodigoBrevo> log)
    {
        _http = http;
        _apiKey = apiKey;
        _remetenteEmail = remetenteEmail;
        _remetenteNome = remetenteNome;
        _log = log;
    }

    public async Task EnviarAsync(string email, string codigo, CancellationToken ct = default)
    {
        var payload = new
        {
            sender = new { name = _remetenteNome, email = _remetenteEmail },
            to = new[] { new { email, name = "Usuário BSFM" } },
            subject = $"Código de verificação: {codigo}",
            htmlContent = MontarHtml(codigo),
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = JsonContent.Create(payload),
        };
        requisicao.Headers.Add("api-key", _apiKey);
        requisicao.Headers.Add("accept", "application/json");

        try
        {
            using var resposta = await _http.SendAsync(requisicao, ct);

            if (resposta.IsSuccessStatusCode)
            {
                _log.LogInformation("Código enviado para {Email} via Brevo.", Mascarar(email));
                return;
            }

            var erro = await resposta.Content.ReadAsStringAsync(ct);
            var dica = (int)resposta.StatusCode switch
            {
                401 => "chave BREVO_API_KEY inválida ou revogada",
                400 when erro.Contains("sender", StringComparison.OrdinalIgnoreCase) =>
                    $"remetente '{_remetenteEmail}' não verificado no Brevo (valide o e-mail/domínio em Senders)",
                400 => "payload recusado pelo Brevo",
                402 => "créditos de envio esgotados na conta Brevo",
                429 => "limite de envios por segundo atingido",
                _ => "falha no provedor de e-mail",
            };

            _log.LogError("Brevo recusou o envio para {Email}: {Status} ({Dica}) {Erro}",
                Mascarar(email), (int)resposta.StatusCode, dica, erro);

            // Diagnóstico opcional para demonstração: sem isso, um Brevo quebrado
            // deixa o usuário sem acesso e sem pista nenhuma.
            if (Environment.GetEnvironmentVariable("BSFM_EMAIL_LOG_CODIGO") == "1")
            {
                _log.LogWarning("CÓDIGO DE VERIFICAÇÃO para {Email}: {Codigo} (BSFM_EMAIL_LOG_CODIGO=1)",
                    Mascarar(email), codigo);
            }
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(excecao, "Falha de rede ao enviar código para {Email}.", Mascarar(email));
        }
    }

    /// <summary>Log nunca recebe o e-mail completo.</summary>
    private static string Mascarar(string email)
    {
        var partes = email.Split('@');
        if (partes.Length != 2 || partes[0].Length == 0) return "***";

        var usuario = partes[0];
        var visivel = usuario.Length <= 2 ? usuario[..1] : usuario[..2];
        return $"{visivel}***@{partes[1]}";
    }

    private static string MontarHtml(string codigo) => $"""
        <div style="font-family:Inter,system-ui,sans-serif;background:#f4f9f6;padding:32px">
          <div style="max-width:480px;margin:0 auto;background:#ffffff;border:1px solid #d3e2db;border-radius:16px;padding:32px">
            <div style="font-size:20px;font-weight:700;color:#0f211a;margin-bottom:8px">BSFM</div>
            <div style="color:#466054;font-size:14px;margin-bottom:24px">Seu código de verificação</div>
            <div style="background:#dcfce7;border:2px dashed #16a34a;border-radius:12px;padding:20px;text-align:center">
              <span style="font-size:36px;font-weight:800;letter-spacing:10px;color:#15803d">{codigo}</span>
            </div>
            <p style="color:#6b8478;font-size:12px;margin-top:24px">
              Vale por 30 minutos. Se você não solicitou, ignore este e-mail.
            </p>
          </div>
        </div>
        """;
}
