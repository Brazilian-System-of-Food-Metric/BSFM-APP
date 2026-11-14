using System.Text;
using BSFM.Seguranca;

// ============================================================================
// Harness de verificação da camada de segurança do BSFM.
//
// Não é um teste de "compila e roda": cada caso abaixo afirma um comportamento
// de segurança específico. Se qualquer um falhar, o processo sai com código 1.
// ============================================================================

Console.OutputEncoding = Encoding.UTF8;

var passou = 0;
var falhou = 0;

void Verificar(string nome, bool condicao, string? detalhe = null)
{
    if (condicao)
    {
        passou++;
        Console.WriteLine($"  PASS  {nome}");
    }
    else
    {
        falhou++;
        Console.WriteLine($"  FAIL  {nome}{(detalhe is null ? "" : $"  -> {detalhe}")}");
    }
}

const string SegredoFortes = "chave-de-teste-com-pelo-menos-32-bytes-para-hmac!!";
const string SegredoDiferente = "outra-chave-de-teste-com-32-bytes-ou-mais-diferente!";
const string SegredoAssinatura = "segredo-de-assinatura-de-requisicao-com-32-bytes!!";

// ---------------------------------------------------------------------------
Console.WriteLine("\n[1] Token de sessão (JWT HS256)");
// ---------------------------------------------------------------------------
var emissor = new EmissorSessao(SegredoFortes);
var token = emissor.Emitir(42, "usuario");
var partes = token.Split('.');

Verificar("token tem 3 segmentos", partes.Length == 3, $"segmentos={partes.Length}");

var headerJson = Encoding.UTF8.GetString(Base64Url.Decodificar(partes[0]));
Verificar("cabeçalho declara HS256", headerJson.Contains("\"alg\":\"HS256\""), headerJson);
Verificar("cabeçalho declara JWT", headerJson.Contains("\"typ\":\"JWT\""), headerJson);

var valido = emissor.Validar(token);
Verificar("token íntegro é aceito", valido.Ok, valido.Erro);
Verificar("identidade do usuário é preservada", valido.Claims?.UsuarioId == 42, $"{valido.Claims?.UsuarioId}");
Verificar("jti está presente", !string.IsNullOrEmpty(valido.Claims?.Jti));
Verificar("papel é preservado", valido.Claims?.Papel == "usuario");

// Adulteração do corpo (troca o id do usuário mantendo a assinatura antiga).
var corpoOriginal = Encoding.UTF8.GetString(Base64Url.Decodificar(partes[1]));
var corpoAdulterado = Base64Url.Codificar(
    Encoding.UTF8.GetBytes(corpoOriginal.Replace("\"usuarioId\":42", "\"usuarioId\":1")));
var tokenAdulterado = $"{partes[0]}.{corpoAdulterado}.{partes[2]}";
Verificar("corpo adulterado é rejeitado",
    emissor.Validar(tokenAdulterado).Erro == "assinatura_invalida",
    emissor.Validar(tokenAdulterado).Erro);

// Ataque "alg: none".
var cabecalhoNone = Base64Url.Codificar(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
Verificar("algoritmo 'none' é rejeitado",
    emissor.Validar($"{cabecalhoNone}.{partes[1]}.").Erro == "algoritmo_nao_permitido");

// Token assinado por outra chave.
Verificar("chave estranha é rejeitada",
    new EmissorSessao(SegredoDiferente).Validar(token).Erro == "assinatura_invalida");

// Token expirado (validade zero).
var emissorExpirado = new EmissorSessao(SegredoFortes, TimeSpan.Zero);
Verificar("token expirado é rejeitado",
    emissorExpirado.Validar(emissorExpirado.Emitir(7)).Erro == "expirado");

// Entradas degeneradas.
Verificar("token ausente é rejeitado", emissor.Validar(null).Erro == "token_ausente");
Verificar("token vazio é rejeitado", emissor.Validar("").Erro == "token_ausente");
Verificar("lixo é rejeitado", !emissor.Validar("a.b.c").Ok);

// Segredo fraco na construção.
var segredoCurtoRejeitado = false;
try { _ = new EmissorSessao("curto"); } catch (ArgumentException) { segredoCurtoRejeitado = true; }
Verificar("segredo com menos de 32 bytes é recusado", segredoCurtoRejeitado);

// ---------------------------------------------------------------------------
Console.WriteLine("\n[2] Hash de senha (PBKDF2-SHA256)");
// ---------------------------------------------------------------------------
const string senha = "SenhaForte#2026";
var hash = SenhaHasher.Gerar(senha, 100_000);

Verificar("formato versionado", hash.StartsWith("pbkdf2-sha256$100000$"), hash[..24] + "...");
Verificar("senha correta é aceita", SenhaHasher.Verificar(senha, hash));
Verificar("senha errada é recusada", !SenhaHasher.Verificar("SenhaForte#2025", hash));
Verificar("hash vazio é recusado", !SenhaHasher.Verificar(senha, ""));
Verificar("hash corrompido é recusado", !SenhaHasher.Verificar(senha, "pbkdf2-sha256$abc$xx$yy"));
Verificar("formato desconhecido é recusado", !SenhaHasher.Verificar(senha, "bcrypt$2a$10$abcdef"));
Verificar("mesma senha gera hashes diferentes (salt)", SenhaHasher.Gerar(senha, 100_000) != hash);
Verificar("hash com custo menor pede rehash", SenhaHasher.PrecisaRehash(hash));
Verificar("hash no custo atual não pede rehash", !SenhaHasher.PrecisaRehash(hash, 100_000));

var senhaCurtaRejeitada = false;
try { SenhaHasher.Gerar("1234567"); } catch (ArgumentException) { senhaCurtaRejeitada = true; }
Verificar("senha com menos de 8 caracteres é recusada", senhaCurtaRejeitada);

// ---------------------------------------------------------------------------
Console.WriteLine("\n[3] Assinatura por requisição (HMAC-SHA256 + anti-replay)");
// ---------------------------------------------------------------------------
var verificador = new VerificadorAssinatura(SegredoAssinatura);
var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var corpo = Encoding.UTF8.GetBytes("{\"peso\":82.4}");
const string nonce = "nonce-abc-123";
var assinatura = verificador.Calcular("POST", "/api/medicao", agora, nonce, corpo);

Verificar("assinatura válida é aceita",
    verificador.Validar("POST", "/api/medicao", agora.ToString(), nonce, corpo, assinatura).Ok);

// Replay do mesmo nonce.
Verificar("nonce reutilizado é rejeitado",
    verificador.Validar("POST", "/api/medicao", agora.ToString(), nonce, corpo, assinatura).Erro == "nonce_reutilizado");

// Corpo alterado.
var outroCorpo = Encoding.UTF8.GetBytes("{\"peso\":50.0}");
Verificar("corpo alterado é rejeitado",
    verificador.Validar("POST", "/api/medicao", agora.ToString(), "nonce-novo-1", outroCorpo, assinatura).Erro == "assinatura_invalida");

// Rota trocada.
Verificar("rota trocada é rejeitada",
    verificador.Validar("POST", "/api/admin", agora.ToString(), "nonce-novo-2", corpo, assinatura).Erro == "assinatura_invalida");

// Método trocado.
Verificar("método trocado é rejeitado",
    verificador.Validar("DELETE", "/api/medicao", agora.ToString(), "nonce-novo-3", corpo, assinatura).Erro == "assinatura_invalida");

// Relógio fora da janela.
var antigo = agora - 3600;
var assinaturaAntiga = verificador.Calcular("POST", "/api/medicao", antigo, "nonce-antigo-1", corpo);
Verificar("timestamp fora da janela é rejeitado",
    verificador.Validar("POST", "/api/medicao", antigo.ToString(), "nonce-antigo-1", corpo, assinaturaAntiga).Erro == "timestamp_fora_da_janela");

// Nonce queimado por tentativa forjada não deve bloquear o legítimo.
var nonceDisputado = "nonce-disputado-9";
verificador.Validar("POST", "/api/medicao", agora.ToString(), nonceDisputado, corpo, "assinatura-forjada");
Verificar("nonce sobrevive a tentativa forjada",
    verificador.Validar("POST", "/api/medicao", agora.ToString(), nonceDisputado, corpo,
        verificador.Calcular("POST", "/api/medicao", agora, nonceDisputado, corpo)).Ok);

Verificar("assinatura ausente é rejeitada",
    verificador.Validar("POST", "/api/medicao", agora.ToString(), "nonce-x1", corpo, null).Erro == "assinatura_ausente");
Verificar("nonce curto é rejeitado",
    verificador.Validar("POST", "/api/medicao", agora.ToString(), "abc", corpo, assinatura).Erro == "nonce_invalido");
Verificar("chave de assinatura é determinística e distinta da de token",
    new VerificadorAssinatura(SegredoAssinatura).Calcular("GET", "/x", agora, "n1", []) !=
    new VerificadorAssinatura(SegredoDiferente).Calcular("GET", "/x", agora, "n1", []));

// ---------------------------------------------------------------------------
Console.WriteLine("\n[4] TOTP (RFC 6238) contra os vetores oficiais");
// ---------------------------------------------------------------------------
// Segredo do RFC: ASCII "12345678901234567890" em Base32.
const string segredoRfc = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

var vetores = new (long Instante, string Codigo6)[]
{
    (59,          "287082"), // RFC: 94287082
    (1111111109,  "081804"), // RFC: 07081804
    (2000000000,  "279037"), // RFC: 69279037
    (20000000000, "353130"), // RFC: 65353130
};

foreach (var (instante, esperado) in vetores)
{
    var calculado = Totp.GerarCodigo(segredoRfc, DateTimeOffset.FromUnixTimeSeconds(instante));
    Verificar($"T={instante} -> {esperado}", calculado == esperado, calculado);
}

var agoraTotp = DateTimeOffset.UtcNow;
var codigoAtual = Totp.GerarCodigo(segredoRfc, agoraTotp);
Verificar("código atual é aceito", Totp.Verificar(segredoRfc, codigoAtual, agoraTotp));
Verificar("código errado é recusado", !Totp.Verificar(segredoRfc, "000000", agoraTotp));
Verificar("código não numérico é recusado", !Totp.Verificar(segredoRfc, "abcdef", agoraTotp));
Verificar("código com tamanho errado é recusado", !Totp.Verificar(segredoRfc, "12345", agoraTotp));
Verificar("passo anterior é aceito (tolerância)",
    Totp.Verificar(segredoRfc, Totp.GerarCodigo(segredoRfc, agoraTotp.AddSeconds(-30)), agoraTotp));
Verificar("dois passos atrás é recusado",
    !Totp.Verificar(segredoRfc, Totp.GerarCodigo(segredoRfc, agoraTotp.AddSeconds(-90)), agoraTotp));

var segredoAleatorio = Totp.GerarSegredo();
Verificar("segredo gerado tem 32 caracteres base32", segredoAleatorio.Length == 32, segredoAleatorio);
Verificar("segredo gerado fecha o ciclo",
    Totp.Verificar(segredoAleatorio, Totp.GerarCodigo(segredoAleatorio, agoraTotp), agoraTotp));

// ---------------------------------------------------------------------------
Console.WriteLine($"\n================  {passou} PASS / {falhou} FAIL  ================");
return falhou == 0 ? 0 : 1;
