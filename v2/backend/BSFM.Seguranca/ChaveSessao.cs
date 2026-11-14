using System.Security.Cryptography;
using System.Text;

namespace BSFM.Seguranca;

/// <summary>
/// Segredo mestre usado para derivar chaves. Fica num tipo próprio para não
/// circular como <c>string</c> solta entre serviços.
/// </summary>
public sealed class SegredoMestre
{
    public SegredoMestre(string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);
        if (Encoding.UTF8.GetByteCount(valor) < 32)
            throw new ArgumentException("O segredo mestre precisa ter ao menos 32 bytes.", nameof(valor));

        Valor = valor;
    }

    public string Valor { get; }
}

/// <summary>
/// Deriva a chave de assinatura de cada sessão.
///
/// Por que não usar um segredo global no cliente: em um navegador ele ficaria no
/// bundle, legível por qualquer visitante. Derivar por sessão resolve isso sem
/// coluna nova no banco — o servidor recalcula a partir do <c>jti</c> do token.
///
/// Consequência boa: revogar a sessão revoga a chave, sem nenhuma operação extra.
/// </summary>
public static class ChaveSessao
{
    private const string Prefixo = "bsfm:sessao:v1:";

    /// <summary>Chave em base64url (32 bytes), devolvida uma única vez no login.</summary>
    public static string Derivar(string segredoMestre, string jti)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var dados = Encoding.UTF8.GetBytes(Prefixo + jti);
        var chave = Encoding.UTF8.GetBytes(segredoMestre);

        return Base64Url.Codificar(HMACSHA256.HashData(chave, dados));
    }
}
