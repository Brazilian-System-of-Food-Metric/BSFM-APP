using System.Security.Cryptography;
using System.Text;

namespace BSFM.Api.Servicos;

/// <summary>
/// Resolve os segredos de execução (JWT e HMAC) com três níveis, em ordem de
/// preferência:
///
///   1. variável de ambiente — o caminho recomendado e o único que sobrevive a
///      rotação planejada;
///   2. derivação estável a partir de <c>DATABASE_URL</c> — quando o serviço foi
///      criado sem as variáveis, o app sobe em vez de morrer, e os tokens
///      continuam válidos entre deploys porque a derivação é determinística;
///   3. aleatório por processo — último recurso: o app sobe, mas cada reinício
///      invalida as sessões.
///
/// Por que não usar valor fixo no código: os padrões antigos eram públicos
/// (estavam no arquivo), então qualquer pessoa poderia forjar um token. Derivar
/// de um segredo que o atacante não tem (a senha do banco) mantém a garantia
/// sem exigir configuração manual.
/// </summary>
public static class SegredosDeExecucao
{
    public const string VariavelJwt = "BSFM_JWT_SECRET";
    public const string VariavelHmac = "BSFM_HMAC_SECRET";

    public sealed record Resultado(string Jwt, string Hmac, IReadOnlyList<string> Avisos);

    public static Resultado Resolver(bool producao, string? jwt, string? hmac, string? databaseUrl)
    {
        // Desenvolvimento: valores fixos tornam o teste reproduzível.
        if (!producao)
        {
            return new Resultado(
                jwt ?? "dev-jwt-secret-com-no-minimo-32-bytes-para-hmac!",
                hmac ?? "dev-hmac-secret-com-no-minimo-32-bytes-aqui!!",
                Array.Empty<string>());
        }

        var avisos = new List<string>();

        var jwtFinal = jwt ?? Derivar("bsfm/jwt/v1", databaseUrl, VariavelJwt, avisos);
        var hmacFinal = hmac ?? Derivar("bsfm/hmac/v1", databaseUrl, VariavelHmac, avisos);

        if (databaseUrl is null && (jwt is null || hmac is null))
        {
            avisos.Add(
                "Sem DATABASE_URL e sem segredos explícitos: os segredos são aleatórios por processo " +
                "e todas as sessões serão encerradas a cada reinício.");
        }

        return new Resultado(jwtFinal, hmacFinal, avisos);
    }

    private static string Derivar(string finalidade, string? databaseUrl, string variavel, List<string> avisos)
    {
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            avisos.Add(
                $"{variavel} não configurada: derivando de DATABASE_URL. " +
                "Funciona e é estável, mas o recomendado é definir a variável no serviço.");

            var derivado = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: Encoding.UTF8.GetBytes(databaseUrl),
                outputLength: 48,
                salt: null,
                info: Encoding.UTF8.GetBytes(finalidade));

            return Convert.ToBase64String(derivado);
        }

        avisos.Add($"{variavel} ausente e sem DATABASE_URL: usando segredo aleatório temporário.");
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    }
}
