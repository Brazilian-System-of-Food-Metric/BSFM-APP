using System.Net.Mime;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace BSFM.Api.Middleware;

/// <summary>
/// Tratamento de erro padronizado (RFC 9457 / ProblemDetails).
///
/// A API antiga devolvia <c>{ mensagem: "Erro ao conectar com o banco de dados." }</c>
/// com 500 para tudo — inclusive para erro de validação. Aqui cada tipo de falha
/// tem status e código próprios, e o detalhe técnico não vaza para o cliente.
/// </summary>
public sealed class TratamentoErrosMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<TratamentoErrosMiddleware> _log;

    public TratamentoErrosMiddleware(RequestDelegate proximo, ILogger<TratamentoErrosMiddleware> log)
    {
        _proximo = proximo;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _proximo(contexto);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            // Cliente desistiu: não é erro do servidor.
        }
        catch (Exception excecao)
        {
            if (contexto.Response.HasStarted)
            {
                _log.LogError(excecao, "Falha após o início da resposta em {Rota}", contexto.Request.Path);
                return;
            }

            _log.LogError(excecao, "Falha não tratada em {Metodo} {Rota}",
                contexto.Request.Method, contexto.Request.Path);

            var (status, codigo, titulo) = Classificar(excecao);

            contexto.Response.Clear();
            contexto.Response.StatusCode = status;

            if (status == 503)
                contexto.Response.Headers.RetryAfter = "5";
            contexto.Response.ContentType = MediaTypeNames.Application.ProblemJson;

            var problema = new
            {
                type = "https://bsfm.dev/erros/" + codigo,
                title = titulo,
                status,
                erro = codigo,
                traceId = contexto.TraceIdentifier,
            };

            await contexto.Response.WriteAsync(JsonSerializer.Serialize(problema));
        }
    }

    private static (int Status, string Codigo, string Titulo) Classificar(Exception excecao)
    {
        // Qualquer falha de banco na cadeia vira 503: o EF pode embrulhar a
        // NpgsqlException em RetryLimitExceededException/InvalidOperationException.
        if (FalhaDeBanco(excecao))
            return (503, "banco_indisponivel", "Serviço temporariamente indisponível");

        return excecao switch
        {
            ArgumentException => (400, "requisicao_invalida", "Requisição inválida"),
            UnauthorizedAccessException => (403, "acesso_negado", "Acesso negado"),
            KeyNotFoundException => (404, "nao_encontrado", "Recurso não encontrado"),
            // Detalhe de banco nunca vai para o cliente (pode conter SQL e nomes de coluna).
            _ => (500, "erro_interno", "Erro interno"),
        };
    }

    private static bool FalhaDeBanco(Exception? excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is NpgsqlException or DbUpdateException or TimeoutException) return true;
            if (atual.GetType().FullName?.StartsWith("Npgsql.", StringComparison.Ordinal) == true) return true;
            if (atual.GetType().Name is "RetryLimitExceededException") return true;
        }

        return false;
    }
}

public static class TratamentoErrosExtensao
{
    public static IApplicationBuilder UseTratamentoErros(this IApplicationBuilder app)
        => app.UseMiddleware<TratamentoErrosMiddleware>();
}
