namespace BSFM.Api.DTOs;

// DTOs de entrada e saída. Nenhum deles expõe a entidade de banco: assim um
// campo novo em Usuario não vaza automaticamente para o cliente.

public sealed record LoginEntrada(string? Email, string? Senha, string? CodigoTotp);

public sealed record RefreshEntrada(string? RefreshToken);

public sealed record LogoutEntrada(string? RefreshToken);

public sealed record EsqueceuSenhaEntrada(string? Email);

public sealed record RedefinirSenhaEntrada(string? Email, string? Codigo, string? NovaSenha);

public sealed record LoginSaida(
    string Token,
    string RefreshToken,
    int ExpiraEmSegundos,
    /// <summary>Chave HMAC desta sessão. Guardar em memória, nunca em localStorage.</summary>
    string ChaveAssinatura,
    UsuarioSaida Usuario);

public sealed record UsuarioSaida(
    int Id,
    string Nome,
    string Email,
    double Imc,
    string ClassificacaoImc,
    double Tmb,
    double GastoTotal,
    double Peso,
    double Altura,
    double PesoMeta,
    int Idade,
    bool TotpHabilitado);

public sealed record RenomearEntrada(string? Nome);

public sealed record TrocarSenhaEntrada(string? SenhaAtual, string? NovaSenha);

public sealed record SaudeEntrada(
    double? Peso,
    double? Altura,
    double? PesoMeta,
    string? Sexo,
    string? NivelAtividade,
    string? Diabetes,
    string? Intolerancia,
    DateTime? DataNascimento,
    bool? AceitouTermos,
    string? VersaoTermos);

public sealed record SaudeSaida(
    double Peso,
    double Altura,
    double Imc,
    string ClassificacaoImc,
    double Tmb,
    double GastoTotal,
    double PesoMeta,
    double AguaAlvoMl,
    int Idade,
    string Sexo,
    string NivelAtividade,
    string Diabetes,
    string Intolerancia,
    DateTime? UltimaAtualizacao);

public sealed record SessaoSaida(
    Guid Id,
    DateTime CriadaEm,
    DateTime ExpiraEm,
    DateTime? UltimoUsoEm,
    string? RevogadaMotivo);

public sealed record CadastroEntrada(
    string? Nome,
    string? Email,
    string? Senha,
    bool? AceitouTermos,
    string? VersaoTermos,
    DateTime? DataNascimento,
    string? Sexo,
    double? Peso,
    double? Altura,
    string? NivelAtividade);

public sealed record VerificarEmailEntrada(string? Email, string? Codigo);

// --- Avaliação nutricional contextual ------------------------------------
// O cliente envia só os números da refeição. Quem completa o contexto (meta,
// consumo do dia, diabetes, intolerância) é o servidor, com o usuário da sessão.
public sealed record AvaliacaoEntrada(
    double? Kcal,
    double? Proteinas,
    double? Carbos,
    double? Gorduras,
    double? Fibra);

// --- Segundo fator (TOTP) ------------------------------------------------
public sealed record Iniciar2faEntrada(string? Senha);
public sealed record Confirmar2faEntrada(string? Codigo);
public sealed record Desativar2faEntrada(string? Senha, string? Codigo);

// --- Exclusão de conta (LGPD) --------------------------------------------
public sealed record ExcluirContaEntrada(string? Senha, string? Confirmacao);