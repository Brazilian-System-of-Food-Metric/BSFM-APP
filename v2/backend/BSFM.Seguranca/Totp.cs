using System.Security.Cryptography;
using System.Text;

namespace BSFM.Seguranca;

/// <summary>
/// TOTP (RFC 6238) — segundo fator de autenticação, o "código de 6 dígitos"
/// que o app já envia por e-mail, mas agora verificável de forma
/// criptográfica em vez de comparado no JavaScript.
///
/// Parâmetros: SHA-1, 6 dígitos, passo de 30 s, janela de ±1 passo
/// (tolera 30 s de desvio de relógio para cada lado).
/// </summary>
public static class Totp
{
    public const int Digitos = 6;
    public const int PassoSegundos = 30;
    private const string AlfabetoBase32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Gera um segredo aleatório de 160 bits, codificado em Base32 (o que se mostra ao usuário).</summary>
    public static string GerarSegredo()
        => CodificarBase32(RandomNumberGenerator.GetBytes(20));

    /// <summary>Código válido no instante informado.</summary>
    public static string GerarCodigo(string segredoBase32, DateTimeOffset? instante = null)
    {
        var chave = DecodificarBase32(segredoBase32);
        var contador = (instante ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / PassoSegundos;
        return Calcular(chave, contador);
    }

    /// <summary>
    /// Confere o código aceitando um passo para trás e um para frente.
    /// Usa comparação em tempo constante.
    /// </summary>
    public static bool Verificar(string segredoBase32, string? codigo, DateTimeOffset? instante = null, int passosTolerados = 1)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return false;

        codigo = codigo.Trim().Replace(" ", string.Empty);
        if (codigo.Length != Digitos || !codigo.All(char.IsAsciiDigit))
            return false;

        byte[] chave;
        try
        {
            chave = DecodificarBase32(segredoBase32);
        }
        catch (FormatException)
        {
            return false;
        }

        var agora = (instante ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / PassoSegundos;
        var recebido = Encoding.ASCII.GetBytes(codigo);

        for (var deslocamento = -passosTolerados; deslocamento <= passosTolerados; deslocamento++)
        {
            var contador = agora + deslocamento;
            if (contador < 0)
                continue;

            var esperado = Encoding.ASCII.GetBytes(Calcular(chave, contador));
            if (CryptographicOperations.FixedTimeEquals(esperado, recebido))
                return true;
        }

        return false;
    }

    private static string Calcular(byte[] chave, long contador)
    {
        Span<byte> buffer = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            buffer[i] = (byte)(contador & 0xFF);
            contador >>= 8;
        }

        var hmac = HMACSHA1.HashData(chave, buffer);

        // Truncamento dinâmico (RFC 4226 §5.3).
        var deslocamento = hmac[^1] & 0x0F;
        var binario = ((hmac[deslocamento] & 0x7F) << 24)
                    | ((hmac[deslocamento + 1] & 0xFF) << 16)
                    | ((hmac[deslocamento + 2] & 0xFF) << 8)
                    | (hmac[deslocamento + 3] & 0xFF);

        var modulo = (int)Math.Pow(10, Digitos);
        return (binario % modulo).ToString().PadLeft(Digitos, '0');
    }

    private static string CodificarBase32(ReadOnlySpan<byte> dados)
    {
        var sb = new StringBuilder((dados.Length * 8 + 4) / 5);
        int bits = 0, valor = 0;

        foreach (var b in dados)
        {
            valor = (valor << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(AlfabetoBase32[(valor >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }

        if (bits > 0)
            sb.Append(AlfabetoBase32[(valor << (5 - bits)) & 0x1F]);

        return sb.ToString();
    }

    private static byte[] DecodificarBase32(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new FormatException("segredo vazio");

        var limpo = texto.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();
        var saida = new List<byte>(limpo.Length * 5 / 8);
        int bits = 0, valor = 0;

        foreach (var c in limpo)
        {
            var indice = AlfabetoBase32.IndexOf(c, StringComparison.Ordinal);
            if (indice < 0)
                throw new FormatException($"caractere base32 inválido: '{c}'");

            valor = (valor << 5) | indice;
            bits += 5;
            if (bits >= 8)
            {
                saida.Add((byte)((valor >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        if (saida.Count == 0)
            throw new FormatException("segredo base32 sem bytes");

        return saida.ToArray();
    }
}
