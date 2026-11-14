using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BSFM.Seguranca;

/// <summary>
/// Claims que viajam dentro do token de sessão. São as únicas informações
/// que o servidor aceita de volta do cliente — nunca o corpo que o front enviou.
/// </summary>
public sealed record ClaimsSessao(
    int UsuarioId,
    string Jti,
    long Iat,
    long Exp,
    int Versao,
    string Papel = "usuario");

/// <summary>Resultado de uma validação. Erro nunca é nulo quando Ok é falso.</summary>
public readonly record struct ResultadoSessao(bool Ok, ClaimsSessao? Claims, string? Erro)
{
    public static ResultadoSessao Sucesso(ClaimsSessao c) => new(true, c, null);
    public static ResultadoSessao Falha(string erro) => new(false, null, erro);
}

/// <summary>
/// Emissor e validador de token de sessão no formato JWT HS256.
///
/// Decisão de projeto: HS256 (chave simétrica) em vez de RS256 porque o mesmo
/// serviço assina e valida. Se um dia houver outro consumidor que só valida,
/// troque para RS256 sem mudar o contrato público desta classe.
/// </summary>
public sealed class EmissorSessao
{
    private const string AlgoritmoEsperado = "HS256";
    private const string TipoEsperado = "JWT";
    private const int VersaoAtual = 1;

    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly byte[] _chave;
    private readonly TimeSpan _validade;

    /// <param name="segredo">Segredo compartilhado (mínimo 32 bytes).</param>
    /// <param name="validade">Tempo de vida do token. Padrão: 2 horas.</param>
    public EmissorSessao(string segredo, TimeSpan? validade = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segredo);

        var bytes = Encoding.UTF8.GetBytes(segredo);
        if (bytes.Length < 32)
            throw new ArgumentException("O segredo de assinatura precisa ter ao menos 32 bytes (256 bits).", nameof(segredo));

        _chave = bytes;
        _validade = validade ?? TimeSpan.FromHours(2);
    }

    /// <summary>Emite um token novo para a sessão informada.</summary>
    public string Emitir(int usuarioId, string papel = "usuario", string? jti = null)
    {
        if (usuarioId <= 0)
            throw new ArgumentOutOfRangeException(nameof(usuarioId), "O identificador do usuário precisa ser positivo.");

        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new ClaimsSessao(
            UsuarioId: usuarioId,
            Jti: jti ?? GerarJti(),
            Iat: agora,
            Exp: agora + (long)_validade.TotalSeconds,
            Versao: VersaoAtual,
            Papel: papel);

        var cabecalho = Base64Url.Codificar(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = AlgoritmoEsperado,
            typ = TipoEsperado,
        }));

        var corpo = Base64Url.Codificar(JsonSerializer.SerializeToUtf8Bytes(claims, OpcoesJson));
        var assinatura = Assinar($"{cabecalho}.{corpo}");

        return $"{cabecalho}.{corpo}.{assinatura}";
    }

    /// <summary>
    /// Valida estrutura, algoritmo, assinatura e expiração, nessa ordem.
    /// A comparação de assinatura é feita em tempo constante.
    /// </summary>
    public ResultadoSessao Validar(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ResultadoSessao.Falha("token_ausente");

        var partes = token.Split('.');
        if (partes.Length != 3)
            return ResultadoSessao.Falha("formato_invalido");

        var (cabecalhoB64, corpoB64, assinaturaB64) = (partes[0], partes[1], partes[2]);

        // 1. O algoritmo vem do próprio token: precisa ser exatamente o esperado,
        //    senão um atacante troca para "none" e a assinatura deixa de ser verificada.
        try
        {
            using var docCabecalho = JsonDocument.Parse(Base64Url.Decodificar(cabecalhoB64));
            if (!docCabecalho.RootElement.TryGetProperty("alg", out var alg) || alg.GetString() != AlgoritmoEsperado)
                return ResultadoSessao.Falha("algoritmo_nao_permitido");

            if (!docCabecalho.RootElement.TryGetProperty("typ", out var typ) || typ.GetString() != TipoEsperado)
                return ResultadoSessao.Falha("tipo_invalido");
        }
        catch (JsonException)
        {
            return ResultadoSessao.Falha("cabecalho_invalido");
        }
        catch (FormatException)
        {
            return ResultadoSessao.Falha("cabecalho_invalido");
        }

        // 2. Assinatura sobre o texto exato recebido.
        var esperada = Encoding.ASCII.GetBytes(Assinar($"{cabecalhoB64}.{corpoB64}"));
        byte[] recebida;
        try
        {
            recebida = Encoding.ASCII.GetBytes(assinaturaB64);
        }
        catch (FormatException)
        {
            return ResultadoSessao.Falha("assinatura_invalida");
        }

        if (esperada.Length != recebida.Length ||
            !CryptographicOperations.FixedTimeEquals(esperada, recebida))
            return ResultadoSessao.Falha("assinatura_invalida");

        // 3. Conteúdo — só depois de provar que veio de nós.
        ClaimsSessao? claims;
        try
        {
            claims = JsonSerializer.Deserialize<ClaimsSessao>(Base64Url.Decodificar(corpoB64), OpcoesJson);
        }
        catch (JsonException)
        {
            return ResultadoSessao.Falha("corpo_invalido");
        }
        catch (FormatException)
        {
            return ResultadoSessao.Falha("corpo_invalido");
        }

        if (claims is null)
            return ResultadoSessao.Falha("corpo_invalido");

        if (claims.Versao != VersaoAtual)
            return ResultadoSessao.Falha("versao_nao_suportada");

        if (claims.UsuarioId <= 0)
            return ResultadoSessao.Falha("usuario_invalido");

        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (claims.Exp <= agora)
            return ResultadoSessao.Falha("expirado");

        // Tolera até 60 s de relógio adiantado no emissor.
        if (claims.Iat > agora + 60)
            return ResultadoSessao.Falha("emitido_no_futuro");

        return ResultadoSessao.Sucesso(claims);
    }

    private string Assinar(string entrada)
    {
        var dados = Encoding.ASCII.GetBytes(entrada);
        return Base64Url.Codificar(HMACSHA256.HashData(_chave, dados));
    }

    private static string GerarJti() => Base64Url.Codificar(RandomNumberGenerator.GetBytes(16));
}
