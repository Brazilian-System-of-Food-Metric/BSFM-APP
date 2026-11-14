using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BSFM.Seguranca;

/// <summary>Resultado da validação da assinatura de uma requisição.</summary>
public readonly record struct ResultadoAssinatura(bool Ok, string? Erro)
{
    public static ResultadoAssinatura Sucesso() => new(true, null);
    public static ResultadoAssinatura Falha(string erro) => new(false, erro);
}

/// <summary>
/// Assinatura por requisição (HMAC-SHA256) — a "verificação em hash a cada
/// sessão de usuário" exigida no PIM.
///
/// Duas formas de uso:
///   • <see cref="Calcular"/> / <see cref="Validar"/> — chave fixa do construtor;
///   • <see cref="CalcularCom"/> / <see cref="ValidarCom"/> — chave por sessão,
///     derivada do <c>jti</c> (é o modo usado pela API, para o navegador nunca
///     precisar de um segredo global).
///
/// String canônica assinada (com \n literais):
///   METODO
///   CAMINHO
///   TIMESTAMP_UNIX
///   NONCE
///   SHA256_HEX(CORPO)
///
/// O cliente envia: X-BSFM-Timestamp, X-BSFM-Nonce, X-BSFM-Signature.
/// </summary>
public sealed class VerificadorAssinatura
{
    private readonly byte[] _chavePadrao;
    private readonly TimeSpan _janela;
    private readonly TimeProvider _relogio;
    private readonly ConcurrentDictionary<string, long> _noncesUsados = new(StringComparer.Ordinal);

    /// <param name="segredo">Segredo dedicado à assinatura. Não reutilize o do token.</param>
    /// <param name="janela">Tolerância de relógio. Padrão: 5 minutos.</param>
    public VerificadorAssinatura(string segredo, TimeSpan? janela = null, TimeProvider? relogio = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segredo);

        var bytes = Encoding.UTF8.GetBytes(segredo);
        if (bytes.Length < 32)
            throw new ArgumentException("O segredo de assinatura precisa ter ao menos 32 bytes.", nameof(segredo));

        _chavePadrao = bytes;
        _janela = janela ?? TimeSpan.FromMinutes(5);
        _relogio = relogio ?? TimeProvider.System;
    }

    /// <summary>Assinatura com a chave padrão do construtor.</summary>
    public string Calcular(string metodo, string caminho, long timestampUnix, string nonce, ReadOnlySpan<byte> corpo)
        => CalcularCom(_chavePadrao, metodo, caminho, timestampUnix, nonce, corpo);

    /// <summary>Assinatura com uma chave explícita (por sessão).</summary>
    public string CalcularCom(byte[] chave, string metodo, string caminho, long timestampUnix, string nonce, ReadOnlySpan<byte> corpo)
    {
        ArgumentNullException.ThrowIfNull(chave);

        var corpoHash = Convert.ToHexString(SHA256.HashData(corpo)).ToLowerInvariant();
        var canonica = $"{metodo.ToUpperInvariant()}\n{caminho}\n{timestampUnix}\n{nonce}\n{corpoHash}";

        return Convert.ToHexString(
            HMACSHA256.HashData(chave, Encoding.UTF8.GetBytes(canonica))).ToLowerInvariant();
    }

    /// <summary>Validação com a chave padrão do construtor.</summary>
    public ResultadoAssinatura Validar(
        string? metodo,
        string? caminho,
        string? timestampTexto,
        string? nonce,
        ReadOnlySpan<byte> corpo,
        string? assinatura)
        => ValidarCom(_chavePadrao, metodo, caminho, timestampTexto, nonce, corpo, assinatura);

    /// <summary>
    /// Valida janela de tempo, formato, assinatura e reuso de nonce, nessa ordem,
    /// usando a chave informada.
    /// </summary>
    public ResultadoAssinatura ValidarCom(
        byte[] chave,
        string? metodo,
        string? caminho,
        string? timestampTexto,
        string? nonce,
        ReadOnlySpan<byte> corpo,
        string? assinatura)
    {
        ArgumentNullException.ThrowIfNull(chave);

        if (string.IsNullOrWhiteSpace(metodo) || string.IsNullOrWhiteSpace(caminho))
            return ResultadoAssinatura.Falha("requisicao_incompleta");

        // A ausência da assinatura é diagnosticada antes do nonce: um cliente que
        // nem envia os cabeçalhos de segurança não deve parecer "nonce inválido".
        if (string.IsNullOrWhiteSpace(assinatura))
            return ResultadoAssinatura.Falha("assinatura_ausente");

        if (string.IsNullOrWhiteSpace(nonce) || nonce.Length < 8 || nonce.Length > 128)
            return ResultadoAssinatura.Falha("nonce_invalido");

        if (!long.TryParse(timestampTexto, out var timestamp))
            return ResultadoAssinatura.Falha("timestamp_invalido");

        var agora = _relogio.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(agora - timestamp) > (long)_janela.TotalSeconds)
            return ResultadoAssinatura.Falha("timestamp_fora_da_janela");

        var esperada = CalcularCom(chave, metodo, caminho, timestamp, nonce, corpo);

        var a = Encoding.ASCII.GetBytes(esperada);
        var b = Encoding.ASCII.GetBytes(assinatura.Trim().ToLowerInvariant());
        if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b))
            return ResultadoAssinatura.Falha("assinatura_invalida");

        // Só marca o nonce depois de a assinatura ser válida: um atacante não
        // consegue "queimar" nonces legítimos com requisições forjadas.
        var chaveNonce = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)))[..32];
        var expiraEm = agora + (long)_janela.TotalSeconds;
        if (!_noncesUsados.TryAdd(chaveNonce, expiraEm))
            return ResultadoAssinatura.Falha("nonce_reutilizado");

        LimparNoncesExpirados(agora);
        return ResultadoAssinatura.Sucesso();
    }

    /// <summary>Quantidade de nonces retidos — usado em teste e em métrica.</summary>
    public int NoncesRetidos => _noncesUsados.Count;

    private void LimparNoncesExpirados(long agora)
    {
        foreach (var par in _noncesUsados)
            if (par.Value < agora)
                _noncesUsados.TryRemove(par.Key, out _);
    }
}
