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
