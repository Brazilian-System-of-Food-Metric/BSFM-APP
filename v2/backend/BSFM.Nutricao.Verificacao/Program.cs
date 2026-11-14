using System.Text;
using BSFM.Nutricao;
using BSFM.Nutricao.Visao;

Console.OutputEncoding = Encoding.UTF8;

// ============================================================================
// Verificação da camada nutricional: normalização, busca tolerante, cálculo por
// porção, catálogo CSV e contrato de visão (incluindo "não sei").
// ============================================================================

var passou = 0;
var falhou = 0;

void Verificar(string nome, bool ok, string? detalhe = null)
{
    if (ok) { passou++; Console.WriteLine($"  PASS  {nome}"); }
    else { falhou++; Console.WriteLine($"  FAIL  {nome}{(detalhe is null ? "" : $"  -> {detalhe}")}"); }
}

static bool Perto(double a, double b, double tolerancia = 0.01) => Math.Abs(a - b) <= tolerancia;

// ---------------------------------------------------------------------------
Console.WriteLine("\n[1] Normalizacao de texto");
// ---------------------------------------------------------------------------
Verificar("caixa e espacos", BuscaAlimentos.Normalizar("  Arroz  Branco ") == "arroz branco",
    BuscaAlimentos.Normalizar("  Arroz  Branco "));
Verificar("acento removido", BuscaAlimentos.Normalizar("Feijão") == "feijao",
    BuscaAlimentos.Normalizar("Feijão"));
Verificar("cedilha removida", BuscaAlimentos.Normalizar("MAÇÃ") == "maca",
    BuscaAlimentos.Normalizar("MAÇÃ"));
Verificar("pontuacao vira espaco", BuscaAlimentos.Normalizar("arroz, branco.") == "arroz branco",
    BuscaAlimentos.Normalizar("arroz, branco."));
Verificar("texto vazio", BuscaAlimentos.Normalizar("   ") == "");

// ---------------------------------------------------------------------------
Console.WriteLine("\n[2] Busca tolerante");
// ---------------------------------------------------------------------------
var catalogo = CatalogoAlimentos.Padrao;
Console.WriteLine($"  (catalogo com {catalogo.Count} alimentos)");

var arroz = BuscaAlimentos.Melhor("arroz");
Verificar("'arroz' acha arroz branco", arroz?.Alimento.Nome == "Arroz branco cozido", arroz?.Alimento.Nome);
Verificar("'arroz' pontua alto", arroz is not null && arroz.Pontuacao >= 0.9, arroz?.Pontuacao.ToString("F2"));

var exato = BuscaAlimentos.Melhor("Arroz branco cozido");
Verificar("nome completo pontua 1.0", exato is not null && Perto(exato.Pontuacao, 1.0), exato?.Pontuacao.ToString("F3"));

var integral = BuscaAlimentos.Melhor("arroz integral");
Verificar("'arroz integral' nao cai no branco", integral?.Alimento.Nome == "Arroz integral cozido", integral?.Alimento.Nome);

var feijao = BuscaAlimentos.Melhor("feijão");
Verificar("'feijão' acha feijao carioca", feijao?.Alimento.Nome == "Feijao carioca cozido", feijao?.Alimento.Nome);

var preto = BuscaAlimentos.Melhor("feijao preto");
Verificar("'feijao preto' acha o preto", preto?.Alimento.Nome == "Feijao preto cozido", preto?.Alimento.Nome);

var plural = BuscaAlimentos.Melhor("macas");
Verificar("plural 'macas' acha maca", plural?.Alimento.Nome == "Maca com casca", plural?.Alimento.Nome);

var typo = BuscaAlimentos.Melhor("aroz");
Verificar("erro de digitacao 'aroz' ainda acha arroz",
    typo?.Alimento.Nome == "Arroz branco cozido", typo?.Alimento.Nome ?? "nada");

var frango = BuscaAlimentos.Melhor("frango");
Verificar("'frango' acha peito grelhado", frango?.Alimento.Nome == "Frango peito sem pele grelhado",
    frango?.Alimento.Nome);

var banana = BuscaAlimentos.Melhor("banana");
Verificar("'banana' acha banana prata", banana?.Alimento.Nome == "Banana prata", banana?.Alimento.Nome);

var tomate = BuscaAlimentos.Melhor("tomate");
Verificar("'tomate' acha tomate", tomate?.Alimento.Nome == "Tomate", tomate?.Alimento.Nome);

var lixo = BuscaAlimentos.Buscar("xilofone quantico");
Verificar("texto absurdo nao retorna nada", lixo.Count == 0, $"{lixo.Count} resultados");

var vazio = BuscaAlimentos.Buscar("");
Verificar("consulta vazia nao retorna nada", vazio.Count == 0);

var limite = BuscaAlimentos.Buscar("arroz", catalogo, 10);
Verificar("busca respeita limite e ordena", limite.Count >= 1 && limite[0].Pontuacao >= limite[^1].Pontuacao);

// ---------------------------------------------------------------------------
Console.WriteLine("\n[3] Calculo por porcao");
// ---------------------------------------------------------------------------
var arrozBase = catalogo.First(a => a.Nome == "Arroz branco cozido");

var cem = arrozBase.Por100g.Escalar(100);
Verificar("100 g mantem os valores da base", Perto(cem.Kcal, 128) && Perto(cem.Proteina, 2.5), $"{cem}");

var centoECinquenta = arrozBase.Por100g.Escalar(150);
Verificar("150 g escala calorias (192)", Perto(centoECinquenta.Kcal, 192), $"{centoECinquenta.Kcal}");
Verificar("150 g escala proteina (3.75)", Perto(centoECinquenta.Proteina, 3.75), $"{centoECinquenta.Proteina}");

var zero = arrozBase.Por100g.Escalar(0);
Verificar("porcao zero devolve zero", Perto(zero.Kcal, 0) && Perto(zero.Proteina, 0));

var cinquenta = arrozBase.Por100g.Escalar(50);
Verificar("50 g devolve metade", Perto(cinquenta.Kcal, 64) && Perto(cinquenta.Carboidrato, 14.05), $"{cinquenta}");

// ---------------------------------------------------------------------------
Console.WriteLine("\n[4] Carga de catalogo por CSV");
// ---------------------------------------------------------------------------
var csv = new[]
{
    "# nome;grupo;kcal;proteina;carbo;gordura;fibra;sinonimos",
    "Tapioca;Cereais;330;1.0;80.0;0.0;1.0;tapioca|beiju",
    "Cuscuz de milho;Cereais;113;2.2;25.0;0.5;1.5;cuscuz",
    "linha quebrada;sem;dados",
    "",
};
var carregado = CatalogoAlimentos.CarregarCsv(csv);
Verificar("carrega apenas as linhas validas", carregado.Count == 2, $"{carregado.Count}");
Verificar("ignora comentario e linha invalida", carregado.All(a => a.Nome.StartsWith("Tapioca") || a.Nome.StartsWith("Cuscuz")));
Verificar("preserva sinonimos", carregado[0].Sinonimos.Contains("beiju"));

var buscaCsv = BuscaAlimentos.Melhor("beiju", carregado);
Verificar("sinonimo do CSV encontra o alimento", buscaCsv?.Alimento.Nome == "Tapioca", buscaCsv?.Alimento.Nome ?? "nada");

// ---------------------------------------------------------------------------
Console.WriteLine("\n[5] Analise por nome");
// ---------------------------------------------------------------------------
var porNome = AnalisadorAlimento.PorNome("arroz", 200, catalogo);
Verificar("por nome identifica", porNome.Identificado, porNome.Motivo);
Verificar("por nome calcula 200 g (256 kcal)",
    porNome.NaPorcao is not null && Perto(porNome.NaPorcao.Kcal, 256), $"{porNome.NaPorcao?.Kcal}");
Verificar("por nome traz candidatos para o usuario escolher", porNome.Candidatos.Count >= 1);

var foraDoCatalogo = AnalisadorAlimento.PorNome("xilofone quantico", 100, catalogo);
Verificar("fora do catalogo nao inventa nutriente",
    !foraDoCatalogo.Identificado && foraDoCatalogo.NaPorcao is null
    && foraDoCatalogo.Motivo == "alimento_fora_do_catalogo", foraDoCatalogo.Motivo);

var semNome = AnalisadorAlimento.PorNome("", 100, catalogo);
Verificar("nome vazio e recusado", !semNome.Identificado && semNome.Motivo == "nome_ausente");

var porcaoAbsurda = AnalisadorAlimento.PorNome("arroz", 99999, catalogo);
Verificar("porcao acima do teto e limitada a 3000 g",
    porcaoAbsurda.Gramas == 3000, $"{porcaoAbsurda.Gramas}");

// ---------------------------------------------------------------------------
Console.WriteLine("\n[6] Contrato de visao");
// ---------------------------------------------------------------------------
var indisponivel = new VisaoIndisponivel();
Verificar("provedor indisponivel se declara", !indisponivel.Disponivel);

var semProvedor = await AnalisadorAlimento.PorImagemAsync(new byte[] { 1, 2, 3 }, indisponivel, null, 100, catalogo);
Verificar("imagem sem provedor nao inventa resultado",
    !semProvedor.Identificado && semProvedor.Motivo == "provedor_de_visao_indisponivel", semProvedor.Motivo);

var geminiSemChave = new VisaoGemini(new HttpClient(), null);
Verificar("gemini sem chave se declara indisponivel", !geminiSemChave.Disponivel);

var respostaSemChave = await geminiSemChave.ReconhecerAsync(new byte[] { 1, 2, 3 }, null);
Verificar("gemini sem chave devolve motivo", !respostaSemChave.Identificado && respostaSemChave.Motivo == "chave_api_ausente");

// Provedor falso: simula a visão acertando o nome, sem rede.
var visaoFalsa = new VisaoFalsa("arroz branco", 0.88);
var porImagem = await AnalisadorAlimento.PorImagemAsync(new byte[] { 1, 2, 3 }, visaoFalsa, null, 150, catalogo);
Verificar("visao que acerta o nome resolve no catalogo", porImagem.Identificado, porImagem.Motivo);
Verificar("visao + catalogo calcula a porcao",
    porImagem.NaPorcao is not null && Perto(porImagem.NaPorcao.Kcal, 192), $"{porImagem.NaPorcao?.Kcal}");
Verificar("confianca da visao e preservada", Perto(porImagem.Confianca, 0.88), $"{porImagem.Confianca}");

var visaoConfusa = new VisaoFalsa("comida caseira", 0.3);
var foraDoCatalogoVisao = await AnalisadorAlimento.PorImagemAsync(new byte[] { 1 }, visaoConfusa, null, 100, catalogo);
Verificar("visao que acerta nome desconhecido nao inventa nutriente",
    !foraDoCatalogoVisao.Identificado && foraDoCatalogoVisao.Motivo == "alimento_fora_do_catalogo",
    foraDoCatalogoVisao.Motivo);

// ---------------------------------------------------------------------------
Console.WriteLine("\n[7] Interpretacao da resposta do Gemini");
// ---------------------------------------------------------------------------
var ok = VisaoGemini.Interpretar("""{"alimento":"arroz branco","confianca":0.92}""");
Verificar("json direto", ok.Identificado && ok.Nome == "arroz branco" && Perto(ok.Confianca, 0.92));

var comCerca = VisaoGemini.Interpretar("```json\n{\"alimento\":\"feijao\",\"confianca\":0.8}\n```");
Verificar("json dentro de cerca markdown", comCerca.Identificado && comCerca.Nome == "feijao", comCerca.Nome);

var nulo = VisaoGemini.Interpretar("""{"alimento":null,"confianca":0}""");
Verificar("alimento nulo vira nao identificado", !nulo.Identificado && nulo.Motivo == "sem_alimento");

var confiancaZero = VisaoGemini.Interpretar("""{"alimento":"xyz","confianca":0}""");
Verificar("confianca zero vira nao identificado", !confiancaZero.Identificado && confiancaZero.Motivo == "confianca_zero");

var confiancaEstourada = VisaoGemini.Interpretar("""{"alimento":"arroz","confianca":7.5}""");
Verificar("confianca acima de 1 e limitada", confiancaEstourada.Identificado && Perto(confiancaEstourada.Confianca, 1.0));

var invalido = VisaoGemini.Interpretar("isto nao e json");
Verificar("json invalido vira nao identificado", !invalido.Identificado && invalido.Motivo == "json_invalido");

var semCampo = VisaoGemini.Interpretar("""{"fruta":"banana"}""");
Verificar("json sem campo alimento vira nao identificado", !semCampo.Identificado && semCampo.Motivo == "sem_alimento");

// ---------------------------------------------------------------------------
Console.WriteLine("\n[8] Consistencia do catalogo");
// ---------------------------------------------------------------------------
Verificar("catalogo tem ao menos 45 itens", catalogo.Count >= 45, $"{catalogo.Count}");
Verificar("nenhum nome vazio", catalogo.All(a => a.Nome.Trim().Length >= 2));
Verificar("nenhum valor negativo", catalogo.All(a =>
    a.Por100g.Kcal >= 0 && a.Por100g.Proteina >= 0 && a.Por100g.Carboidrato >= 0
    && a.Por100g.Gordura >= 0 && a.Por100g.Fibra >= 0));
Verificar("calorias coerentes com os macros (ate 25% de desvio)", catalogo.All(a =>
{
    var calculado = (a.Por100g.Proteina * 4) + (a.Por100g.Carboidrato * 4) + (a.Por100g.Gordura * 9);
    if (a.Por100g.Kcal < 20) return true; // bebidas e itens de baixa caloria
    return Math.Abs(calculado - a.Por100g.Kcal) <= a.Por100g.Kcal * 0.25;
}), string.Join("; ", catalogo
    .Where(a =>
    {
        var c = (a.Por100g.Proteina * 4) + (a.Por100g.Carboidrato * 4) + (a.Por100g.Gordura * 9);
        return a.Por100g.Kcal >= 20 && Math.Abs(c - a.Por100g.Kcal) > a.Por100g.Kcal * 0.25;
    })
    .Select(a => a.Nome)));

Verificar("nenhum nome duplicado",
    catalogo.Count == catalogo.Select(a => BuscaAlimentos.Normalizar(a.Nome)).Distinct().Count());

// ---------------------------------------------------------------------------
Console.WriteLine("\n[9] Catalogo TACO real (CSV versionado)");
// ---------------------------------------------------------------------------
var caminhoTaco = Path.Combine(AppContext.BaseDirectory, "dados", "taco.csv");
if (!File.Exists(caminhoTaco))
{
    Console.WriteLine($"  AVISO  CSV nao encontrado em {caminhoTaco} - secao ignorada");
}
else
{
    var servico = new CatalogoNutricional(caminhoTaco);
    var taco = servico.Alimentos;
    Console.WriteLine($"  (origem={servico.Origem}, {taco.Count} alimentos)");

    Verificar("carrega a TACO pelo CSV", servico.Origem == "taco", servico.Origem);
    Verificar("TACO tem mais de 500 itens", taco.Count > 500, $"{taco.Count}");

    // O primeiro resultado precisa ser o prato que a pessoa quer dizer.
    var casos = new (string Consulta, string DeveConter)[]
    {
        ("arroz", "cozid"),
        ("feijao", "carioca"),
        ("banana", "prata"),
        ("leite", "vaca"),
        ("frango", "peito"),
        ("ovo", "inteiro"),
        ("pao de queijo", "queijo"),
        ("batata", "cozid"),
    };

    foreach (var (consulta, esperado) in casos)
    {
        var topo = BuscaAlimentos.Buscar(consulta, taco, 3).FirstOrDefault();
        var nome = BuscaAlimentos.Normalizar(topo?.Alimento.Nome);
        Verificar($"'{consulta}' tem '{esperado}' no topo", nome.Contains(esperado), topo?.Alimento.Nome ?? "nada");
    }

    Verificar("consulta absurda continua vazia na TACO",
        BuscaAlimentos.Buscar("xilofone quantico", taco).Count == 0);

    var porNomeTaco = AnalisadorAlimento.PorNome("feijao", 100, taco);
    Verificar("analise por nome usa a TACO",
        porNomeTaco.Identificado && porNomeTaco.Alimento!.Nome.Contains("carioca"),
        porNomeTaco.Alimento?.Nome ?? "nada");
}

Console.WriteLine($"\n================  {passou} PASS / {falhou} FAIL  ================");
return falhou == 0 ? 0 : 1;

/// <summary>Provedor de visão de teste: devolve sempre o mesmo nome.</summary>
internal sealed class VisaoFalsa : IVisaoAlimento
{
    private readonly string _nome;
    private readonly double _confianca;

    public VisaoFalsa(string nome, double confianca)
    {
        _nome = nome;
        _confianca = confianca;
    }

    public string Identificacao => "falsa";

    public bool Disponivel => true;

    public Task<Reconhecimento> ReconhecerAsync(ReadOnlyMemory<byte> imagem, string? dica, CancellationToken ct = default)
        => Task.FromResult(new Reconhecimento(true, _nome, _confianca, Identificacao));
}
