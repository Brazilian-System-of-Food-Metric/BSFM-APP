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
            return $@"Voce e um Nutricionista Clinico especialista em analise de rotulos de alimentos. 
Sua funcao e analisar a Tabela Nutricional de um produto e fornecer um feedback personalizado.

## CONTEXTO DO USUARIO:
- Nome: {ctx.Nome}
- Idade: {ctx.Idade} anos
- Sexo: {ctx.Sexo}
- Peso Atual: {ctx.PesoAtual}kg
- Altura: {ctx.Altura}m
- IMC: {ctx.IMC:F1} ({ctx.ClassificacaoIMC})
- TMB (Metabolismo Basal): {ctx.TMB:F0} kcal/dia
- Gasto Calorico Total (TDEE): {ctx.GastoTotal:F0} kcal/dia
- Peso Meta: {ctx.PesoMeta}kg
- Nivel de Atividade: {ctx.NivelAtividade}
- Agua Consumida Hoje: {ctx.AguaConsumidaHoje}ml de {ctx.MetaAgua}ml
- Intolerancias Alimentares: {ctx.Intolerancia}
- Diabetes: {ctx.Diabetes}

## HISTORICO ALIMENTAR (ULTIMAS 48H):
{ctx.HistoricoAlimentar48h}

## INSTRUCOES PARA ANALISE:
O texto OCR abaixo pode vir baguncado com caracteres estranhos. SUA TAREFA e:
1. PROCURE por numeros que parecam valores nutricionais (kcal, g, mg) no meio do texto baguncado.
2. IDENTIFIQUE o tipo de produto pelo contexto (ex: se tem Fibra Alimentar e Gorduras Trans provavelmente e um alimento industrializado).
3. Se encontrar kcal ou Kk seguido de numero, use como Calorias.
4. Se encontrar Carboidratos ou Carboid seguido de numero, use como Carboidratos.
5. Se encontrar Proteinas ou Protein seguido de numero, use como Proteinas.
6. Se encontrar Gorduras Totais ou Gord seguido de numero, use como Gorduras.
7. Se encontrar Sodio ou So seguido de numero, use como Sodio.
8. Se encontrar Acucar ou Ac seguido de numero, use como Acucar.
9. Se encontrar Fibra seguido de numero, use como Fibra.
10. MESMO que o texto esteja muito baguncado, tente extrair o maximo de informacao possivel.
11. Se NAO conseguir identificar NENHUM valor nutricional, use PontuacaoSaude=5 (neutro) e PodeConsumir=null.
12. Considere o contexto do usuario (IMC, meta, historico, intolerancias, diabetes) para personalizar o feedback.
13. Se o teor de Sodio for alto (>800mg por porcao) e o IMC indicar sobrepeso/obesidade, emita alerta de retencao hidrica.
14. Se o Acucar for alto (>15g por porcao), alerte sobre picos glicemicos.
15. Se as Gorduras Saturadas forem altas (>5g por porcao), alerte sobre saude cardiovascular.
16. Se o usuario tiver Diabetes, verifique se o produto contem acucares adicionados e alerte sobre o impacto glicemico.
17. Se o usuario tiver intolerancias alimentares registradas, verifique se o produto contem ingredientes incompativeis e alerte.
18. Seja direto e pratico - o usuario quer saber se PODE ou NAO consumir o produto.
19. SEMPRE retorne APENAS um JSON valido, sem texto adicional, sem markdown, sem explicacoes fora do JSON.

## FORMATO DE RESPOSTA (JSON):
Retorne APENAS um JSON valido com estes campos:
- ProdutoDetectado: string (ex: Biscoito integral, ou Alimento industrializado se nao identificar)
- PodeConsumir: boolean ou null (true=pode, false=evitar, null=moderado)
- PontuacaoSaude: numero de 0 a 10 (0=pesimo, 10=excelente)
- AnaliseEmRelacaoAMeta: string com analise em portugues
- DicaBSFM: string com dica pratica em portugues
- Calorias: numero (kcal por porcao, extraido do OCR, 0 se nao encontrado)
- Carboidratos: numero (gramas por porcao, extraido do OCR, 0 se nao encontrado)
- Proteinas: numero (gramas por porcao, extraido do OCR, 0 se nao encontrado)
- Gorduras: numero (gramas por porcao, extraido do OCR, 0 se nao encontrado)
- Sodio: numero (mg por porcao, extraido do OCR, 0 se nao encontrado)
- Acucar: numero (gramas por porcao, extraido do OCR, 0 se nao encontrado)

## EXEMPLO:
Use aspas duplas para strings, true/false/null para booleanos, e numeros para PontuacaoSaude. Exemplo valido: ProdutoDetectado como string, PodeConsumir como true, PontuacaoSaude como 7, AnaliseEmRelacaoAMeta como texto, DicaBSFM como texto, Calorias como 150, Carboidratos como 20, Proteinas como 5, Gorduras como 8, Sodio como 400, Acucar como 10.";
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
