using BSFM.Api.DTOs;
using BSFM.Api.Filtros;
using BSFM.Api.Servicos;
using BSFM.Dados;
using BSFM.Dados.Entidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Controllers;

/// <summary>
/// Dados de saúde do próprio usuário. Substitui `/api/saude/{id}` e
/// `PUT /api/saude/atualizar`, ambos anônimos.
///
/// Dado de saúde é categoria especial na LGPD: toda leitura e escrita aqui
/// é auditada e a aceitação de termos gera um registro de consentimento
/// versionado, não um booleano solto.
/// </summary>
[ApiController]
[Route("api/eu/saude")]
[SessaoObrigatoria]
public sealed class SaudeController : ControllerBase
{
    private readonly BsfmDbContext _db;
    private readonly ServicoAuditoria _auditoria;

    public SaudeController(BsfmDbContext db, ServicoAuditoria auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private int UsuarioId => HttpContext.UsuarioId();
    private string? IpHash => ServicoSessao.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString());

    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken ct)
    {
        var u = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.ID == UsuarioId, ct);
        if (u is null) return NotFound(new { erro = "usuario_inexistente" });

        await _auditoria.RegistrarAsync("saude_lida", u.ID, "Usuario", u.ID.ToString(), null, IpHash, ct);

        return Ok(new SaudeSaida(
            u.Peso, u.Altura, u.IMC,
            CalculadoraNutricional.ClassificarImc(u.IMC),
            u.TMB, u.GastoTotal, u.PesoMeta,
            CalculadoraNutricional.CalcularAguaAlvoMl(u.Peso),
            u.CalcularIdade(), u.Sexo, u.TipoPessoa, u.Diabetes, u.Intolerancia,
            u.AtualizadoEm));
    }

    // -----------------------------------------------------------------------
    [HttpPut]
    public async Task<IActionResult> Atualizar([FromBody] SaudeEntrada entrada, CancellationToken ct)
    {
        var u = await _db.Usuarios.SingleOrDefaultAsync(x => x.ID == UsuarioId, ct);
        if (u is null) return NotFound(new { erro = "usuario_inexistente" });

        if (entrada.Peso is { } peso && (!double.IsFinite(peso) || peso <= 0 || peso > 500))
            return BadRequest(new { erro = "peso_invalido" });

        if (entrada.Altura is { } altura && (!double.IsFinite(altura) || altura <= 0 || altura > 3))
            return BadRequest(new { erro = "altura_invalida" });

        if (entrada.PesoMeta is { } metaPeso && (!double.IsFinite(metaPeso) || metaPeso <= 0 || metaPeso > 500))
            return BadRequest(new { erro = "peso_meta_invalido" });

        var agora = DateTime.UtcNow;
        var pesoAnterior = u.Peso;

        if (entrada.Peso is not null) u.Peso = entrada.Peso.Value;
        if (entrada.Altura is not null) u.Altura = entrada.Altura.Value;
        if (entrada.PesoMeta is not null) u.PesoMeta = entrada.PesoMeta.Value;
        if (!string.IsNullOrWhiteSpace(entrada.Sexo)) u.Sexo = entrada.Sexo!;
        if (!string.IsNullOrWhiteSpace(entrada.NivelAtividade)) u.TipoPessoa = entrada.NivelAtividade!;
        if (entrada.Diabetes is not null) u.Diabetes = entrada.Diabetes;
        if (entrada.Intolerancia is not null) u.Intolerancia = entrada.Intolerancia;
        if (entrada.DataNascimento is not null) u.DataNascimento = entrada.DataNascimento;

        // Recalcula sempre a partir do estado final, nunca do payload.
        u.IMC = CalculadoraNutricional.CalcularImc(u.Peso, u.Altura);
        u.TMB = CalculadoraNutricional.CalcularTmb(u.Sexo, u.Peso, u.Altura, u.CalcularIdade());
        u.GastoTotal = CalculadoraNutricional.CalcularGastoTotal(u.TipoPessoa, u.TMB);
        u.AtualizadoEm = agora;

        // Toda mudança de peso vira ponto na série histórica.
        if (entrada.Peso is not null && Math.Abs(pesoAnterior - u.Peso) > 0.001)
        {
            _db.Historicos.Add(new HistoricoProgresso
            {
                UsuarioID = u.ID,
                Peso = u.Peso,
                Altura = u.Altura,
                IMC = u.IMC,
                DataRegistro = agora,
            });
        }

        // Consentimento versionado — não um booleano sem rastro.
        if (entrada.AceitouTermos == true)
        {
            var versao = string.IsNullOrWhiteSpace(entrada.VersaoTermos) ? "1.0" : entrada.VersaoTermos!;

            u.AceitouTermos = true;
            u.DataAceite = agora;
            u.VersaoTermos = versao;

            _db.ConsentimentosLGPD.Add(new ConsentimentoLGPD
            {
                UsuarioId = u.ID,
                Finalidade = "dados_saude",
                VersaoTermo = versao,
                Concedido = true,
                DataHora = agora,
                IpHash = IpHash,
                Origem = "tela_saude",
            });
        }

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync("saude_atualizada", u.ID, "Usuario", u.ID.ToString(),
            $"{{\"peso\":{u.Peso.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)},\"imc\":{u.IMC.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}}}",
            IpHash, ct);

        return Ok(new SaudeSaida(
            u.Peso, u.Altura, u.IMC,
            CalculadoraNutricional.ClassificarImc(u.IMC),
            u.TMB, u.GastoTotal, u.PesoMeta,
            CalculadoraNutricional.CalcularAguaAlvoMl(u.Peso),
            u.CalcularIdade(), u.Sexo, u.TipoPessoa, u.Diabetes, u.Intolerancia,
            u.AtualizadoEm));
    }

    // -----------------------------------------------------------------------
    [HttpGet("historico")]
    public async Task<IActionResult> Historico([FromQuery] int limite = 30, CancellationToken ct = default)
    {
        limite = Math.Clamp(limite, 1, 365);

        var serie = await _db.Historicos.AsNoTracking()
            .Where(h => h.UsuarioID == UsuarioId)
            .OrderByDescending(h => h.DataRegistro)
            .Take(limite)
            .Select(h => new { data = h.DataRegistro, peso = h.Peso, imc = h.IMC })
            .ToListAsync(ct);

        return Ok(serie);
    }
}
