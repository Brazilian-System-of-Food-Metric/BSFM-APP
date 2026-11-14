namespace BSFM.Seguranca;

/// <summary>
/// Base64URL sem preenchimento (RFC 4648 §5), usado por todos os segmentos do
/// token e por identificadores opacos. Fica num tipo próprio para que qualquer
/// camada (API, cliente, verificação) use exatamente a mesma codificação.
/// </summary>
public static class Base64Url
{
    public static string Codificar(ReadOnlySpan<byte> dados) =>
        Convert.ToBase64String(dados).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decodificar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var s = texto.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            0 => s,
            _ => throw new FormatException("comprimento base64url inválido"),
        };

        return Convert.FromBase64String(s);
    }
}
