namespace BSFM.Api.DTOs;

// DTOs dos grupos migrados no S3: água, refeições, metas, análises e evolução.

public sealed record AguaEntrada(double? Ml);

public sealed record AguaDiaSaida(
    DateOnly Data,
    double TotalMl,
    double MetaMl,
    double Percentual,
    int Registros);

public sealed record RefeicaoEntrada(
    string? DiaSemana,
    string? TipoRefeicao,
    string? NomePrato,
    string? Ingredientes,
    string? ModoPreparo,
    double? Calorias,
    double? Proteinas,
    double? Carboidratos,
    double? Gorduras);

public sealed record RefeicaoSaida(
    int Id,
    string DiaSemana,
    string TipoRefeicao,
    string NomePrato,
    string Ingredientes,
    string ModoPreparo,
    double Calorias,
    double Proteinas,
    double Carboidratos,
    double Gorduras);

public sealed record MetaEntrada(
    double? CaloriasAlvo,
    double? ProteinasAlvo,
    double? AguaAlvoMl,
    double? PesoAlvo);

public sealed record MetaSaida(
    double CaloriasAlvo,
    double ProteinasAlvo,
    double AguaAlvoMl,
    double PesoAlvo,
    double CaloriasConsumidasHoje,
    double PercentualCalorico,
    DateTime CriadaEm);

public sealed record AnaliseEntrada(
    string? Alimento,
    double? Calorias,
    double? Proteinas,
    double? Carbos,
    double? Gorduras,
    string? Porcao,
    double? Confianca,
    string? ModeloVersao,
    string? ImagemHash,
    string? Origem,
    bool? PodeConsumir,
    int? PontuacaoSaude,
    string? AnaliseEmRelacaoAMeta,
    string? DicaBSFM);

public sealed record AnaliseSaida(
    int Id,
    string Alimento,
    double Calorias,
    double Proteinas,
    double Carbos,
    double Gorduras,
    string Porcao,
    DateTime DataAnalise,
    bool? PodeConsumir,
    int PontuacaoSaude,
    string AnaliseEmRelacaoAMeta,
    string DicaBSFM,
    double? Confianca,
    string? ModeloVersao,
    string Origem);

public sealed record ResumoDiaSaida(
    DateOnly Data,
    int Analises,
    double Calorias,
    double Proteinas,
    double Carbos,
    double Gorduras);

public sealed record PontoEvolucao(DateTime Data, double Peso, double Imc);

public sealed record EvolucaoSaida(
    double PesoInicial,
    double PesoAtual,
    double VariacaoPeso,
    double ImcInicial,
    double ImcAtual,
    double VariacaoImc,
    double PesoMeta,
    double DistanciaDaMeta,
    IReadOnlyList<PontoEvolucao> Serie);

public sealed record AnaliseDoCatalogoEntrada(string? Nome, double? Gramas, string? Origem);
