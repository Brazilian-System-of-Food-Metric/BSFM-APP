using BSFM.Api.DTOs;
using BSFM.Api.Filtros;
using BSFM.Api.Servicos;
using BSFM.Dados;
using BSFM.Seguranca;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Controllers;

/// <summary>
/// Área do próprio usuário. Substitui as rotas por id (`/usuario/{id}`,
/// `/historico-analises/{usuarioId}`) que permitiam ler e alterar a conta de
/// qualquer pessoa trocando um número na URL.
///
/// Aqui o id vem sempre do token, nunca da rota.
/// </summary>
[ApiController]
[Route("api/eu")]
[SessaoObrigatoria]
public sealed class PerfilController : ControllerBase
{
    private readonly BsfmDbContext _db;
    private readonly ServicoSessao _sessoes;
    private readonly ServicoAuditoria _auditoria;

    public PerfilController(BsfmDbContext db, ServicoSessao sessoes, ServicoAuditoria auditoria)
    {
        _db = db;
        _sessoes = sessoes;
        _auditoria = auditoria;
    }

    private int UsuarioId => HttpContext.UsuarioId();

    // -----------------------------------------------------------------------
    [HttpGet("perfil")]
    public async Task<IActionResult> Perfil(CancellationToken ct)
    {
        var usuario = await _db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);

        if (usuario is null)
            return NotFound(new { erro = "usuario_inexistente" });

        return Ok(new
        {
            id = usuario.ID,
            nome = usuario.Nome,
            email = usuario.Email,
            emailVerificado = usuario.EmailVerificado,
            imc = usuario.IMC,
            classificacaoImc = CalculadoraNutricional.ClassificarImc(usuario.IMC),
            tmb = usuario.TMB,
            gastoTotal = usuario.GastoTotal,
            peso = usuario.Peso,
            altura = usuario.Altura,
            pesoMeta = usuario.PesoMeta,
            idade = usuario.CalcularIdade(),
            totpHabilitado = usuario.TotpHabilitado,
            dataNascimento = usuario.DataNascimento,
            diabetes = usuario.Diabetes,
            intolerancia = usuario.Intolerancia,
            saudeCompleta = usuario.Diabetes != "Não informado" || !string.IsNullOrWhiteSpace(usuario.Intolerancia),
            papel = usuario.Papel,
            criadoEm = usuario.CriadoEm,
            jti = HttpContext.Jti(),
        });
    }

    // -----------------------------------------------------------------------
    [HttpPut("nome")]
    public async Task<IActionResult> Renomear([FromBody] RenomearEntrada entrada, CancellationToken ct)
    {
        var nome = (entrada.Nome ?? string.Empty).Trim();

        if (nome.Length < 3 || nome.Length > 200)
            return BadRequest(new { erro = "nome_invalido" });

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null)
            return NotFound(new { erro = "usuario_inexistente" });

        usuario.Nome = nome;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("nome_alterado", usuario.ID, "Usuario", usuario.ID.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new { mensagem = "Nome atualizado.", nome });
    }

    // -----------------------------------------------------------------------
    [HttpPut("senha")]
    public async Task<IActionResult> TrocarSenha([FromBody] TrocarSenhaEntrada entrada, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entrada.SenhaAtual) || string.IsNullOrWhiteSpace(entrada.NovaSenha))
            return BadRequest(new { erro = "dados_incompletos" });

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null)
            return NotFound(new { erro = "usuario_inexistente" });

        if (!ServicoSenha.Verificar(entrada.SenhaAtual, usuario.SenhaHash))
            return BadRequest(new { erro = "senha_atual_invalida" });

        if (entrada.NovaSenha.Length < 8)
            return BadRequest(new { erro = "senha_curta" });

        usuario.SenhaHash = ServicoSenha.Gerar(entrada.NovaSenha);
        usuario.SenhaAtualizadaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Derruba as outras sessões, mantendo a atual.
        var sessaoAtual = HttpContext.SessaoId();
        var outras = await _db.Sessoes
            .Where(s => s.UsuarioId == usuario.ID && s.Id != sessaoAtual && s.RevogadaEm == null)
            .ToListAsync(ct);

        foreach (var s in outras)
        {
            s.RevogadaEm = DateTime.UtcNow;
            s.RevogadaMotivo = "senha_alterada";
        }
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("senha_alterada", usuario.ID, "Usuario", usuario.ID.ToString(),
            $"{{\"sessoesRevogadas\":{outras.Count}}}",
            ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new { mensagem = "Senha atualizada.", sessoesRevogadas = outras.Count });
    }

    // -----------------------------------------------------------------------
    [HttpGet("sessoes")]
    public async Task<IActionResult> Sessoes(CancellationToken ct)
    {
        var atual = HttpContext.SessaoId();

        var lista = await _db.Sessoes.AsNoTracking()
            .Where(s => s.UsuarioId == UsuarioId)
            .OrderByDescending(s => s.CriadaEm)
            .Select(s => new
            {
                id = s.Id,
                criadaEm = s.CriadaEm,
                expiraEm = s.ExpiraEm,
                ultimoUsoEm = s.UltimoUsoEm,
                revogadaEm = s.RevogadaEm,
                revogadaMotivo = s.RevogadaMotivo,
                atual = s.Id == atual,
            })
            .ToListAsync(ct);

        return Ok(lista);
    }

    // -----------------------------------------------------------------------
    [HttpDelete("sessoes/{id:guid}")]
    public async Task<IActionResult> EncerrarSessao(Guid id, CancellationToken ct)
    {
        var sessao = await _db.Sessoes
            .SingleOrDefaultAsync(s => s.Id == id && s.UsuarioId == UsuarioId, ct);

        if (sessao is null)
            return NotFound(new { erro = "sessao_inexistente" });

        await _sessoes.RevogarSessaoAsync(id, "encerrada_pelo_usuario", ct);

        return NoContent();
    }

    // -----------------------------------------------------------------------
    /// <summary>Troca o e-mail. Exige senha atual e derruba a verificação.</summary>
    [HttpPut("email")]
    public async Task<IActionResult> TrocarEmail([FromBody] TrocarEmailEntrada entrada, CancellationToken ct)
    {
        var email = (entrada.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (email.Length < 5 || email.Length > 320 || !email.Contains('@') || !email.Contains('.'))
            return BadRequest(new { erro = "email_invalido" });

        if (string.IsNullOrWhiteSpace(entrada.Senha))
            return BadRequest(new { erro = "senha_obrigatoria" });

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        if (!ServicoSenha.Verificar(entrada.Senha, usuario.SenhaHash))
            return BadRequest(new { erro = "senha_incorreta" });

        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == email && u.ID != UsuarioId, ct))
            return Conflict(new { erro = "email_em_uso" });

        usuario.Email = email;
        usuario.EmailVerificado = false;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("email_alterado", usuario.ID, "Usuario", usuario.ID.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new { mensagem = "E-mail atualizado. Verifique a nova caixa.", email, emailVerificado = false });
    }

    // -----------------------------------------------------------------------
    /// <summary>Define a data de nascimento (a idade passa a ser derivada dela).</summary>
    [HttpPut("nascimento")]
    public async Task<IActionResult> DefinirNascimento([FromBody] NascimentoEntrada entrada, CancellationToken ct)
    {
        if (entrada.DataNascimento is null)
            return BadRequest(new { erro = "data_obrigatoria" });

        var data = entrada.DataNascimento.Value.Date;
        var hoje = DateTime.UtcNow.Date;

        if (data > hoje) return BadRequest(new { erro = "data_futura" });
        if (data < hoje.AddYears(-120)) return BadRequest(new { erro = "data_antiga" });

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        usuario.DataNascimento = data;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { mensagem = "Data de nascimento atualizada.", dataNascimento = data, idade = usuario.Idade });
    }

    // -----------------------------------------------------------------------
    /// <summary>Perfil de saúde usado pelo feedback alimentar (v1 chamava de "alerta pendente").</summary>
    [HttpPut("perfil/saude")]
    public async Task<IActionResult> AtualizarSaude([FromBody] SaudePerfilEntrada entrada, CancellationToken ct)
    {
        var diabetes = (entrada.Diabetes ?? "Não informado").Trim();
        var intolerancia = (entrada.Intolerancia ?? string.Empty).Trim();

        string[] permitidos = { "Não informado", "Não", "Tipo 1", "Tipo 2", "Pré-diabetes", "Gestacional" };
        if (!permitidos.Contains(diabetes, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { erro = "diabetes_invalido" });

        if (intolerancia.Length > 400)
            return BadRequest(new { erro = "intolerancia_longa" });

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        usuario.Diabetes = diabetes;
        usuario.Intolerancia = intolerancia;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("saude_atualizada", usuario.ID, "Usuario", usuario.ID.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new { mensagem = "Perfil de saúde atualizado.", diabetes, intolerancia, saudeCompleta = true });
    }
    // -----------------------------------------------------------------------
    // Segundo fator (TOTP).
    //
    // Ativar exige a senha atual (não basta ter a sessão aberta) e a confirmação
    // com um código do aplicativo autenticador. Desativar exige senha + código:
    // um token roubado sozinho não consegue desligar a proteção.
    [HttpPost("2fa/iniciar")]
    public async Task<IActionResult> Iniciar2fa([FromBody] Iniciar2faEntrada entrada, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        if (!ServicoSenha.Verificar(entrada.Senha ?? string.Empty, usuario.SenhaHash))
            return BadRequest(new { erro = "senha_incorreta" });

        if (usuario.TotpHabilitado)
            return Conflict(new { erro = "2fa_ja_ativo" });

        usuario.TotpSegredo = Totp.GerarSegredo();
        usuario.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var conta = Uri.EscapeDataString(usuario.Email);
        var otpauth = $"otpauth://totp/BSFM:{conta}?secret={usuario.TotpSegredo}&issuer=BSFM&algorithm=SHA1&digits=6&period=30";

        return Ok(new
        {
            segredo = usuario.TotpSegredo,
            conta = usuario.Email,
            emissor = "BSFM",
            otpauth,
            mensagem = "Adicione a conta no aplicativo autenticador e confirme o código.",
        });
    }

    // -----------------------------------------------------------------------
    [HttpPost("2fa/ativar")]
    public async Task<IActionResult> Ativar2fa([FromBody] Confirmar2faEntrada entrada, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        if (usuario.TotpHabilitado)
            return Conflict(new { erro = "2fa_ja_ativo" });

        if (!Totp.Verificar(usuario.TotpSegredo ?? string.Empty, entrada.Codigo))
            return BadRequest(new { erro = "codigo_invalido" });

        usuario.TotpHabilitado = true;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("2fa_ativado", usuario.ID, "Usuario", usuario.ID.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new { mensagem = "Verificação em duas etapas ativada.", totpHabilitado = true });
    }

    // -----------------------------------------------------------------------
    [HttpPost("2fa/desativar")]
    public async Task<IActionResult> Desativar2fa([FromBody] Desativar2faEntrada entrada, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        if (!usuario.TotpHabilitado)
            return Ok(new { mensagem = "A verificação em duas etapas já está desligada.", totpHabilitado = false });

        if (!ServicoSenha.Verificar(entrada.Senha ?? string.Empty, usuario.SenhaHash))
            return BadRequest(new { erro = "senha_incorreta" });

        if (!Totp.Verificar(usuario.TotpSegredo ?? string.Empty, entrada.Codigo))
            return BadRequest(new { erro = "codigo_invalido" });

        usuario.TotpHabilitado = false;
        // Rotaciona o segredo: códigos antigos não continuam válidos para e-mail.
        usuario.TotpSegredo = Totp.GerarSegredo();
        usuario.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("2fa_desativado", usuario.ID, "Usuario", usuario.ID.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new { mensagem = "Verificação em duas etapas desligada.", totpHabilitado = false });
    }

    // -----------------------------------------------------------------------
    /// <summary>
    /// Exporta os dados do próprio usuário em JSON (portabilidade, LGPD art. 18).
    /// Não inclui hash de senha, segredo TOTP nem auditoria de terceiros.
    /// </summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(CancellationToken ct)
    {
        var usuario = await _db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);

        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        var analises = await _db.AnalisesIA.AsNoTracking()
            .Where(a => a.UsuarioID == UsuarioId)
            .OrderByDescending(a => a.DataAnalise)
            .Take(5000)
            .Select(a => new
            {
                a.ID, a.Alimento, a.Calorias, a.Proteinas, a.Carbos, a.Gorduras,
                a.Porcao, a.DataAnalise, a.PodeConsumir, a.PontuacaoSaude,
                a.AnaliseEmRelacaoAMeta, a.DicaBSFM, a.Confianca, a.ModeloVersao, a.Origem,
            })
            .ToListAsync(ct);

        var agua = await _db.ConsumoAgua.AsNoTracking()
            .Where(a => a.UsuarioId == UsuarioId)
            .OrderByDescending(a => a.DataRegistro)
            .Take(5000)
            .Select(a => new { a.Id, a.Ml, a.DataRegistro })
            .ToListAsync(ct);

        var refeicoes = await _db.RefeicoesAgendadas.AsNoTracking()
            .Where(r => r.UsuarioId == UsuarioId)
            .Select(r => new
            {
                r.Id, r.DiaSemana, r.TipoRefeicao, r.NomePrato, r.Ingredientes,
                r.ModoPreparo, r.Calorias, r.Proteinas, r.Carboidratos, r.Gorduras,
                r.Concluida, r.ConcluidaEm, r.DataCriacao,
            })
            .ToListAsync(ct);

        var metas = await _db.Metas.AsNoTracking()
            .Where(m => m.UsuarioId == UsuarioId)
            .OrderByDescending(m => m.CriadoEm)
            .Select(m => new { m.Id, m.CaloriasAlvo, m.ProteinasAlvo, m.AguaAlvoMl, m.PesoAlvo, m.CriadoEm, m.Ativa })
            .ToListAsync(ct);

        var historico = await _db.Historicos.AsNoTracking()
            .Where(h => h.UsuarioID == UsuarioId)
            .OrderByDescending(h => h.DataRegistro)
            .Take(5000)
            .Select(h => new { h.ID, h.Peso, h.Altura, h.IMC, h.DataRegistro })
            .ToListAsync(ct);

        var sessoes = await _db.Sessoes.AsNoTracking()
            .Where(s => s.UsuarioId == UsuarioId)
            .OrderByDescending(s => s.CriadaEm)
            .Select(s => new { s.Id, s.CriadaEm, s.ExpiraEm, s.RevogadaEm, s.RevogadaMotivo })
            .ToListAsync(ct);

        var consentimentos = await _db.ConsentimentosLGPD.AsNoTracking()
            .Where(c => c.UsuarioId == UsuarioId)
            .Select(c => new { c.Finalidade, c.VersaoTermo, c.Concedido, c.DataHora, c.Origem })
            .ToListAsync(ct);

        await _auditoria.RegistrarAsync("dados_exportados", usuario.ID, "Usuario", usuario.ID.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new
        {
            geradoEm = DateTime.UtcNow,
            formato = "bsfm-lgpd-v1",
            conta = new
            {
                usuario.ID, usuario.Nome, usuario.Email, usuario.EmailVerificado,
                usuario.Peso, usuario.Altura, usuario.PesoMeta, usuario.IMC, usuario.TMB,
                usuario.GastoTotal, usuario.Sexo, usuario.TipoPessoa, usuario.Diabetes,
                usuario.Intolerancia, usuario.DataNascimento, usuario.Idade,
                usuario.AceitouTermos, usuario.DataAceite, usuario.VersaoTermos,
                usuario.CriadoEm,
            },
            analises,
            agua,
            refeicoes,
            metas,
            historico,
            sessoes,
            consentimentos,
        });
    }

    // -----------------------------------------------------------------------
    /// <summary>
    /// Exclui a conta e os dados pessoais (LGPD art. 18, VI). Exige senha e a
    /// palavra EXCLUIR digitada — não é uma ação de um clique.
    ///
    /// A trilha de auditoria permanece, mas sem vínculo nominal: o usuário é
    /// removido e os eventos ficam anônimos.
    /// </summary>
    [HttpPost("conta/excluir")]
    public async Task<IActionResult> ExcluirConta([FromBody] ExcluirContaEntrada entrada, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.ID == UsuarioId, ct);
        if (usuario is null) return NotFound(new { erro = "usuario_inexistente" });

        if (!ServicoSenha.Verificar(entrada.Senha ?? string.Empty, usuario.SenhaHash))
            return BadRequest(new { erro = "senha_incorreta" });

        if (!string.Equals((entrada.Confirmacao ?? string.Empty).Trim(), "EXCLUIR", StringComparison.Ordinal))
            return BadRequest(new { erro = "confirmacao_invalida" });

        var id = usuario.ID;

        await using var transacao = await _db.Database.BeginTransactionAsync(ct);

        var sessoesIds = await _db.Sessoes.Where(s => s.UsuarioId == id).Select(s => s.Id).ToListAsync(ct);
        _db.RefreshTokens.RemoveRange(_db.RefreshTokens.Where(r => sessoesIds.Contains(r.SessaoId)));
        _db.Sessoes.RemoveRange(_db.Sessoes.Where(s => s.UsuarioId == id));

        _db.AnalisesIA.RemoveRange(_db.AnalisesIA.Where(a => a.UsuarioID == id));
        _db.ConsumoAgua.RemoveRange(_db.ConsumoAgua.Where(a => a.UsuarioId == id));
        _db.RefeicoesAgendadas.RemoveRange(_db.RefeicoesAgendadas.Where(r => r.UsuarioId == id));
        _db.Metas.RemoveRange(_db.Metas.Where(m => m.UsuarioId == id));
        _db.Historicos.RemoveRange(_db.Historicos.Where(h => h.UsuarioID == id));

        var cronogramas = await _db.CronogramasSemanais.Where(c => c.UsuarioId == id).Select(c => c.Id).ToListAsync(ct);
        _db.RefeicoesDiarias.RemoveRange(_db.RefeicoesDiarias.Where(r => cronogramas.Contains(r.CronogramaSemanalId)));
        _db.CronogramasSemanais.RemoveRange(_db.CronogramasSemanais.Where(c => c.UsuarioId == id));

        _db.ConsentimentosLGPD.RemoveRange(_db.ConsentimentosLGPD.Where(c => c.UsuarioId == id));
        _db.Usuarios.Remove(usuario);

        await _db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        // Auditoria sem vínculo: o id do usuário já não existe.
        await _auditoria.RegistrarAsync("conta_excluida", null, "Usuario", id.ToString(),
            null, ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new { mensagem = "Conta e dados pessoais excluídos." });
    }

    // -----------------------------------------------------------------------
    [HttpDelete("sessoes")]
    public async Task<IActionResult> EncerrarTodas(CancellationToken ct)
    {
        await _sessoes.RevogarTodasDoUsuarioAsync(UsuarioId, "encerradas_pelo_usuario", ct);
        return NoContent();
    }
}

public sealed record TrocarEmailEntrada(string? Email, string? Senha);
public sealed record NascimentoEntrada(DateTime? DataNascimento);
public sealed record SaudePerfilEntrada(string? Diabetes, string? Intolerancia);
