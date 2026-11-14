using BSFM.Seguranca;

namespace BSFM.Api.Servicos;

/// <summary>
/// Verificação de senha com suporte aos hashes legados.
///
/// O banco atual do BSFM guarda hashes BCrypt (`$2a$…`). A camada nova grava
/// PBKDF2 (`pbkdf2-sha256$…`). Sem esta ponte, todo usuário existente ficaria
/// trancado para fora no primeiro login após a migração.
///
/// Estratégia: verificar no formato em que o hash está; se for legado e a senha
/// estiver correta, regravar em PBKDF2 no mesmo login (upgrade silencioso).
/// </summary>
public static class ServicoSenha
{
    public static bool Verificar(string senha, string hashArmazenado)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hashArmazenado))
            return false;

        if (EhPbkdf2(hashArmazenado))
            return SenhaHasher.Verificar(senha, hashArmazenado);

        if (EhBcrypt(hashArmazenado))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(senha, hashArmazenado);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Hash legado ou custo defasado que deve ser regravado neste login.</summary>
    public static bool PrecisaRehash(string hashArmazenado)
    {
        if (EhBcrypt(hashArmazenado))
            return true;

        return SenhaHasher.PrecisaRehash(hashArmazenado);
    }

    public static string Gerar(string senha) => SenhaHasher.Gerar(senha);

    private static bool EhPbkdf2(string hash) =>
        hash.StartsWith("pbkdf2-sha256$", StringComparison.Ordinal);

    private static bool EhBcrypt(string hash) =>
        hash.StartsWith("$2a$", StringComparison.Ordinal)
        || hash.StartsWith("$2b$", StringComparison.Ordinal)
        || hash.StartsWith("$2y$", StringComparison.Ordinal);
}
