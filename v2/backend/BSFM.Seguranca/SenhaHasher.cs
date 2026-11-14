using System.Security.Cryptography;
using System.Text;

namespace BSFM.Seguranca;

/// <summary>
/// Hash de senha com PBKDF2-SHA256.
///
/// Por que não manter BCrypt: o projeto já usa BCrypt.Net-Next, mas ele é um
/// pacote externo e o formato dele não versiona parâmetros. PBKDF2 é nativo do
/// .NET, permite elevar o custo sem quebrar hashes antigos, e o formato abaixo
/// carrega o custo junto — dá para reidratar qualquer senha gravada antes.
///
/// Formato: pbkdf2-sha256$&lt;iterações&gt;$&lt;salt b64&gt;$&lt;hash b64&gt;
/// </summary>
public static class SenhaHasher
{
    private const string Prefixo = "pbkdf2-sha256";
    private const int IteracoesPadrao = 210_000; // OWASP 2023 para PBKDF2-SHA256
    private const int BytesSalt = 16;
    private const int BytesHash = 32;

    /// <summary>Gera o hash de uma senha em texto claro.</summary>
    public static string Gerar(string senha, int iteracoes = IteracoesPadrao)
    {
        ValidarSenha(senha);
        if (iteracoes < 10_000)
            throw new ArgumentOutOfRangeException(nameof(iteracoes), "Use ao menos 10.000 iterações.");

        var salt = RandomNumberGenerator.GetBytes(BytesSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(senha), salt, iteracoes, HashAlgorithmName.SHA256, BytesHash);

        return $"{Prefixo}${iteracoes}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Confere a senha contra um hash gravado. Comparação em tempo constante.
    /// Devolve false (sem lançar) para hash corrompido ou formato desconhecido.
    /// </summary>
    public static bool Verificar(string senha, string hashArmazenado)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hashArmazenado))
            return false;

        var partes = hashArmazenado.Split('$');
        if (partes.Length != 4 || partes[0] != Prefixo)
            return false;

        if (!int.TryParse(partes[1], out var iteracoes) || iteracoes < 1)
            return false;

        byte[] salt, esperado;
        try
        {
            salt = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (esperado.Length == 0)
            return false;

        var calculado = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(senha), salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);

        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    /// <summary>
    /// Indica se o hash foi gerado com custo abaixo do atual e deve ser
    /// regravado no próximo login bem-sucedido.
    /// </summary>
    public static bool PrecisaRehash(string hashArmazenado, int iteracoesAtuais = IteracoesPadrao)
    {
        var partes = hashArmazenado?.Split('$');
        if (partes is null || partes.Length != 4 || partes[0] != Prefixo)
            return true;

        return !int.TryParse(partes[1], out var iteracoes) || iteracoes < iteracoesAtuais;
    }

    private static void ValidarSenha(string senha)
    {
        ArgumentNullException.ThrowIfNull(senha);
        if (senha.Length < 8)
            throw new ArgumentException("A senha precisa ter ao menos 8 caracteres.", nameof(senha));
        if (senha.Length > 256)
            throw new ArgumentException("A senha não pode passar de 256 caracteres.", nameof(senha));
    }
}
