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
    [HttpDelete("sessoes")]
    public async Task<IActionResult> EncerrarTodas(CancellationToken ct)
    {
        await _sessoes.RevogarTodasDoUsuarioAsync(UsuarioId, "encerradas_pelo_usuario", ct);
        return NoContent();
    }
}
