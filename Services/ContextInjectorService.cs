using ClassesBSFM;
using Microsoft.EntityFrameworkCore;
using PonteBanco;

namespace BSFM.CoreAnalytics.Backend.Services
{
    /// <summary>
    /// ContextInjectorService
    /// 
    /// Responsabilidade: Montar o SystemPrompt personalizado para o Groq
    /// com base nos dados biométricos e histórico do usuário.
    /// 
    /// Dados coletados:
    /// - IMC, TMB, Gasto Calórico Total (TDEE)
    /// - Peso Meta
    /// - Ingestão de Água nas últimas 24h
    /// - Histórico do Diário Alimentar nas últimas 48h
    /// - Idade, Sexo, Nível de Atividade
    /// - Intolerâncias Alimentares
    /// - Diabetes (Tipo 1, Tipo 2, Pré-diabetes, Gestacional, Não)
    /// </summary>
    public class ContextInjectorService
    {
        private readonly PonteDB _db;

        public ContextInjectorService(PonteDB db)
        {
            _db = db;
        }

        /// <summary>
        /// Monta o contexto completo do usuário para o prompt do Groq
        /// </summary>
        public async Task<UserContext> BuildContextAsync(int usuarioId)
        {
            var user = await _db.Usuarios.FindAsync(usuarioId);
            if (user == null)
                throw new ArgumentException($"Usuário {usuarioId} não encontrado.");

            // 1. Dados Biométricos
            var imc = user.IMC;
            var tmb = user.TMB;
            var gastoTotal = user.GastoTotal;
            var pesoMeta = user.PesoMeta;
            var pesoAtual = user.Peso;
            var altura = user.Altura;
            var idade = user.CalcularIdade();
            var sexo = user.Sexo;
            var nivelAtividade = user.TipoPessoa;

            // 2. Classificação do IMC
            var classificacaoImc = ClassificarIMC(imc);

            // 3. Consumo de Água nas últimas 24h
            var aguaHoje = await _db.ConsumoAgua
                .Where(c => c.UsuarioId == usuarioId && c.DataRegistro >= DateTime.Today)
                .SumAsync(c => c.Ml);

            // 4. Meta de Água (padrão: 2000ml, ajustável)
            var metaAgua = 2000.0;

            // 5. Histórico do Diário Alimentar nas últimas 48h
            var limite48h = DateTime.Now.AddHours(-48);
            var historicoRefeicoes = await _db.AnalisesIA
                .Where(a => a.UsuarioID == usuarioId && a.DataAnalise >= limite48h)
                .OrderByDescending(a => a.DataAnalise)
                .Take(10)
                .ToListAsync();

            // 6. Intolerâncias Alimentares e Diabetes
            var intolerancia = string.IsNullOrWhiteSpace(user.Intolerancia) 
                ? "Não informado" 
                : user.Intolerancia;
            var diabetes = string.IsNullOrWhiteSpace(user.Diabetes) 
                ? "Não informado" 
                : user.Diabetes;

            // 7. Monta o resumo do histórico alimentar
            var resumoHistorico = string.Join("\n", historicoRefeicoes.Select(r =>
                $"- [{r.DataAnalise:dd/MM HH:mm}] {r.Alimento}: {r.Calorias}kcal, " +
                $"P:{r.Proteinas}g, C:{r.Carbos}g, G:{r.Gorduras}g"
            ));

            if (string.IsNullOrEmpty(resumoHistorico))
                resumoHistorico = "Nenhum registro alimentar nas últimas 48 horas.";

            return new UserContext
            {
                Nome = user.Nome,
                Idade = idade,
                Sexo = sexo,
                PesoAtual = pesoAtual,
                Altura = altura,
                IMC = imc,
                ClassificacaoIMC = classificacaoImc,
                TMB = tmb,
                GastoTotal = gastoTotal,
                PesoMeta = pesoMeta,
                NivelAtividade = nivelAtividade,
                AguaConsumidaHoje = aguaHoje,
                MetaAgua = metaAgua,
                HistoricoAlimentar48h = resumoHistorico,
                Intolerancia = intolerancia,
                Diabetes = diabetes
            };
        }

        /// <summary>
        /// Classifica o IMC segundo a OMS
        /// </summary>
        private string ClassificarIMC(double imc)
        {
            if (imc < 18.5) return "Abaixo do peso";
            if (imc < 25) return "Peso normal";
            if (imc < 30) return "Sobrepeso";
            if (imc < 35) return "Obesidade Grau I";
            if (imc < 40) return "Obesidade Grau II";
            return "Obesidade Grau III";
        }

        /// <summary>
        /// Monta o SystemPrompt completo para o Groq
        /// </summary>
        public string BuildSystemPrompt(UserContext ctx)
        {
            return $@"Você é um Nutricionista Clínico especialista em análise de rótulos de alimentos. 
Sua função é analisar a Tabela Nutricional de um produto e fornecer um feedback personalizado.

## CONTEXTO DO USUÁRIO:
- Nome: {ctx.Nome}
- Idade: {ctx.Idade} anos
- Sexo: {ctx.Sexo}
- Peso Atual: {ctx.PesoAtual}kg
- Altura: {ctx.Altura}m
- IMC: {ctx.IMC:F1} ({ctx.ClassificacaoIMC})
- TMB (Metabolismo Basal): {ctx.TMB:F0} kcal/dia
- Gasto Calórico Total (TDEE): {ctx.GastoTotal:F0} kcal/dia
- Peso Meta: {ctx.PesoMeta}kg
- Nível de Atividade: {ctx.NivelAtividade}
- Água Consumida Hoje: {ctx.AguaConsumidaHoje}ml de {ctx.MetaAgua}ml
- Intolerâncias Alimentares: {ctx.Intolerancia}
- Diabetes: {ctx.Diabetes}

## HISTÓRICO ALIMENTAR (ÚLTIMAS 48H):
{ctx.HistoricoAlimentar48h}

## INSTRUÇÕES PARA ANÁLISE:
O texto OCR abaixo pode vir bagunçado com caracteres estranhos. SUA TAREFA é:
1. PROCURE por números que pareçam valores nutricionais (kcal, g, mg) no meio do texto bagunçado.
2. IDENTIFIQUE o tipo de produto pelo contexto (ex: se tem "Fibra Alimentar" e "Gorduras Trans" provavelmente é um alimento industrializado).
3. Se encontrar "kcal" ou "Kk" seguido de número, use como Calorias.
4. Se encontrar "Carboidratos" ou "Carboid" seguido de número, use como Carboidratos.
5. Se encontrar "Proteinas" ou "Protein" seguido de número, use como Proteínas.
6. Se encontrar "Gorduras Totais" ou "Gord" seguido de número, use como Gorduras.
7. Se encontrar "Sódio" ou "So" seguido de número, use como Sódio.
8. Se encontrar "Açúcar" ou "Ac" seguido de número, use como Açúcar.
9. Se encontrar "Fibra" seguido de número, use como Fibra.
10. MESMO que o texto esteja muito bagunçado, tente extrair o máximo de informação possível.
11. Se NÃO conseguir identificar NENHUM valor nutricional, use PontuacaoSaude=5 (neutro) e PodeConsumir=null.
12. Considere o contexto do usuário (IMC, meta, histórico, intolerâncias, diabetes) para personalizar o feedback.
13. Se o teor de Sódio for alto (>800mg por porção) e o IMC indicar sobrepeso/obesidade, emita alerta de retenção hídrica.
14. Se o Açúcar for alto (>15g por porção), alerte sobre picos glicêmicos.
15. Se as Gorduras Saturadas forem altas (>5g por porção), alerte sobre saúde cardiovascular.
16. Se o usuário tiver Diabetes, verifique se o produto contém açúcares adicionados e alerte sobre o impacto glicêmico.
17. Se o usuário tiver intolerâncias alimentares registradas, verifique se o produto contém ingredientes incompatíveis e alerte.
18. Seja direto e prático — o usuário quer saber se PODE ou NÃO consumir o produto.
19. SEMPRE retorne APENAS um JSON válido, sem texto adicional, sem markdown, sem explicações fora do JSON.

## EXEMPLO DE RESPOSTA ESPERADA:
{{""ProdutoDetectado"": ""Biscoito integral"", ""PodeConsumir"": true, ""PontuacaoSaude"": 7, ""AnaliseEmRelacaoAMeta"": ""Produto rico em fibras (24g) e com perfil calórico moderado. Compatível com sua meta de peso."", ""DicaBSFM"": ""Consuma com moderação, até 5 unidades por dia.""

## IMPORTANTE:
- ProdutoDetectado: Tente adivinhar o produto pelo contexto. Se não conseguir, use ""Alimento industrializado"".
- PodeConsumir: true = pode, false = evitar, null = moderado.
- PontuacaoSaude: 0-10 (0=péssimo, 10=excelente).
- AnaliseEmRelacaoAMeta: Texto em português explicando a análise.
- DicaBSFM: Dica prática em português.";
        }
    }

    /// <summary>
    /// Modelo de contexto do usuário para o prompt
    /// </summary>
    public class UserContext
    {
        public string Nome { get; set; } = string.Empty;
        public int Idade { get; set; }
        public string Sexo { get; set; } = string.Empty;
        public double PesoAtual { get; set; }
        public double Altura { get; set; }
        public double IMC { get; set; }
        public string ClassificacaoIMC { get; set; } = string.Empty;
        public double TMB { get; set; }
        public double GastoTotal { get; set; }
        public double PesoMeta { get; set; }
        public string NivelAtividade { get; set; } = string.Empty;
        public double AguaConsumidaHoje { get; set; }
        public double MetaAgua { get; set; }
        public string HistoricoAlimentar48h { get; set; } = string.Empty;
        public string Intolerancia { get; set; } = "Não informado";
        public string Diabetes { get; set; } = "Não informado";
    }
}
