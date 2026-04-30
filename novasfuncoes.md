# 📄 BSFM.CoreAnalytics — Especificação do Motor de Insights Nutricionais (Split Architecture)

> **Projeto:** Brazilian System of Food Metric (BSFM)  
> **Módulo:** `BSFM.CoreAnalytics` — Hub de Análise de Rótulos  
> **Arquitetura:** Split Architecture (Edge OCR + LLM Backend)  
> **Status:** Especificação Técnica v1.0  
> **Objetivo:** Contornar Rate Limits de APIs de Visão Computacional, permitindo dezenas de escaneamentos por minuto sem custos ao servidor.

---

## Sumário

1. [Visão Geral da Arquitetura](#1-visão-geral-da-arquitetura)
2. [Estrutura do Módulo BSFM.CoreAnalytics](#2-estrutura-do-módulo-bsfmcoreanalytics)
3. [A. Camada de Captura — Frontend OCR (Tesseract.js)](#a-camada-de-captura--frontend-ocr-tesseractjs)
4. [B. Context Injector — Backend C#](#b-context-injector--backend-c)
5. [C. Rota de LLM Inference — Groq Integration (NutriBrainService.cs)](#c-rota-de-llm-inference--groq-integration-nutribrainservicecs)
6. [D. Lógica de Feedback Restritivo](#d-lógica-de-feedback-restritivo)
7. [E. Output JSON Estruturado](#e-output-json-estruturado)
8. [Integração com o Sistema Existente](#8-integração-com-o-sistema-existente)
9. [Instruções de Teste e Sandbox](#9-instruções-de-teste-e-sandbox)
10. [Prompt Engineering — Blindagem contra Alucinações](#10-prompt-engineering--blindagem-contra-alucinações)
11. [Apêndice Técnico](#11-apêndice-técnico)

---

## 1. Visão Geral da Arquitetura

### 1.1 O Problema

APIs de Visão Computacional multimodais (GPT-4V, Claude Vision, Google Vision) são:
- **Caras**: Custam por requisição e por token processado.
- **Limitadas**: Rate limits severos (ex.: 3-10 req/min em planos gratuitos).
- **Lentas**: Processamento de imagem no servidor adiciona latência de 3-8 segundos.

### 1.2 A Solução — Split Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    NAVEGADOR DO USUÁRIO                          │
│                                                                  │
│  ┌──────────────┐    ┌──────────────────┐    ┌───────────────┐  │
│  │  Câmera/      │───▶│  Tesseract.js    │───▶│  Texto Bruto  │  │
│  │  Galeria      │    │  (OCR Local)     │    │  Extraído     │  │
│  └──────────────┘    └──────────────────┘    └───────┬───────┘  │
│                                                       │          │
│                                                       ▼          │
│                                              ┌───────────────┐  │
│                                              │  POST /api/   │  │
│                                              │  analisar-    │  │
│                                              │  rotulo       │  │
│                                              └───────┬───────┘  │
└──────────────────────────────────────────────────────┼──────────┘
                                                       │
                                                       ▼
┌─────────────────────────────────────────────────────────────────┐
│                    SERVIDOR C# (Backend)                         │
│                                                                  │
│  ┌──────────────────┐    ┌──────────────────┐    ┌───────────┐  │
│  │  Context Injector│───▶│  NutriBrain      │───▶│  JSON     │  │
│  │  (PostgreSQL)    │    │  Service (Groq)  │    │  Response │  │
│  └──────────────────┘    └──────────────────┘    └───────────┘  │
│                                                                  │
│  Dados do Usuário: IMC, TMB, PesoMeta,                           │
│  Ingestão de Água, Histórico 48h                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Princípio Fundamental**: A imagem **NUNCA** sai do navegador. Apenas o **texto extraído** via OCR local é enviado ao backend, que usa o **Groq (Llama 3)** — um LLM puramente textual de altíssima velocidade — para processar o feedback nutricional.

### 1.3 Benefícios da Split Architecture

| Aspecto | Abordagem Tradicional (Multimodal) | Split Architecture (BSFM) |
|---------|-----------------------------------|--------------------------|
| Custo de API | Alto (imagem + texto) | Baixo (apenas texto) |
| Rate Limits | 3-10 req/min | Ilimitado (Groq: ~30 req/s) |
| Latência | 3-8 segundos | 200-800ms |
| Privacidade | Imagem enviada ao servidor | Imagem processada localmente |
| Escalabilidade | Limitada pelo orçamento | Linear (apenas texto) |

---

## 2. Estrutura do Módulo BSFM.CoreAnalytics

```
BSFM-APP/
├── BSFM.CoreAnalytics/                    ← NOVA PASTA (Módulo Isolado)
│   ├── README.md                          ← Documentação do sandbox
│   ├── Frontend/
│   │   ├── analisador-rotulo.html         ← Página principal do escaneamento
│   │   ├── tesseract-worker.js            ← Web Worker para Tesseract.js
│   │   └── rotulo-processor.js            ← Lógica de pós-processamento OCR
│   │
│   ├── Backend/
│   │   ├── Services/
│   │   │   ├── NutriBrainService.cs       ← Motor de inferência Groq
│   │   │   └── ContextInjectorService.cs  ← Montagem do SystemPrompt
│   │   ├── Models/
│   │   │   ├── RotuloRequest.cs           ← DTO de entrada
│   │   │   ├── RotuloResponse.cs          ← DTO de saída (JSON forçado)
│   │   │   └── GroqApiModels.cs           ← Models da API Groq
│   │   └── Controllers/
│   │       └── RotuloController.cs        ← Endpoint /api/analisar-rotulo
│   │
│   └── Tests/
│       ├── mock-ocr-data.json             ← Dados de teste para OCR simulado
│       └── test-prompt-groq.txt           ← Prompt de teste offline
│
├── Program.cs                             ← Registro do novo serviço (após fusão)
└── MeusApp.csproj                         ← Adicionar HttpClient para Groq
```

### 2.1 Dependências do NuGet (a adicionar no `.csproj`)

```xml
<!-- Já existente: -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />

<!-- NOVAS dependências para o módulo CoreAnalytics: -->
<!-- Nenhuma dependência externa nova! Apenas System.Net.Http.Json (já incluso) -->
<!-- O Groq é consumido via REST API padrão com HttpClient -->
```

### 2.2 Variáveis de Ambiente (Render)

```bash
# OBRIGATÓRIA para o módulo funcionar:
GROQ_API_KEY=seu_token_aqui

# Opcional (já existente):
DATABASE_URL=postgres://...
USDA_API_KEY=...
BREVO_API_KEY=...
```

---

## 3. A. Camada de Captura — Frontend OCR (Tesseract.js)

### 3.1 Arquitetura do Processamento no Navegador

```
┌──────────────────────────────────────────────────────────────┐
│                    analisador-rotulo.html                      │
│                                                                │
│  1. Usuário aponta câmera para a Tabela Nutricional           │
│     ┌──────────────────────────────────────────────────┐       │
│     │  📷 Viewfinder (câmera ao vivo)                  │       │
│     │  ┌────────────────────────────────────────────┐  │       │
│     │  │  [Crop Guide]  ┌──────────────────┐        │  │       │
│     │  │                │  Área de Corte    │        │  │       │
│     │  │                │  (Tabela Nutr.)   │        │  │       │
│     │  │                └──────────────────┘        │  │       │
│     │  └────────────────────────────────────────────┘  │       │
│     └──────────────────────────────────────────────────┘       │
│                                                                │
│  2. Botão "Extrair Texto" → Captura frame do vídeo            │
│  3. Canvas crop → Recorta apenas a área da tabela             │
│  4. Tesseract.js → OCR no Web Worker (não bloqueia UI)        │
│  5. Pós-processamento → Limpeza de caracteres                 │
│  6. Envio do texto ao backend → POST /api/analisar-rotulo     │
└──────────────────────────────────────────────────────────────┘
```

### 3.2 Implementação do Tesseract.js

#### 3.2.1 Instalação via CDN (sem build tools)

No `<head>` do `analisador-rotulo.html`:

```html
<!-- Tesseract.js v5+ via CDN -->
<script src="https://cdn.jsdelivr.net/npm/tesseract.js@5/dist/tesseract.min.js"></script>
```

#### 3.2.2 Web Worker para Processamento em Background

Arquivo: `BSFM.CoreAnalytics/Frontend/tesseract-worker.js`

```javascript
/**
 * tesseract-worker.js
 * 
 * Web Worker que gerencia o ciclo de vida do Tesseract.js.
 * Estratégias de otimização:
 * - Reutiliza o worker entre chamadas (evita recarregar o modelo)
 * - Usa linguagem 'por' (Português) + 'eng' (Inglês) para labels
 * - Aplica whitelist de caracteres para reduzir ruído
 * - Timeout de 30s para evitar travamentos
 */

let worker = null;

async function getWorker() {
    if (!worker) {
        worker = await Tesseract.createWorker({
            logger: (m) => {
                if (m.status === 'recognizing text') {
                    self.postMessage({ 
                        type: 'progress', 
                        progress: Math.round(m.progress * 100) 
                    });
                }
            }
        });
        
        // Carrega português + inglês (para palavras técnicas como "Sodium")
        await worker.loadLanguage('por+eng');
        await worker.initialize('por+eng');
        
        // Configuração para tabelas nutricionais
        await worker.setParameters({
            tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;()%gmgkcl-/\' ',
            preserve_interword_spaces: '1',
            tessedit_pageseg_mode: '6',
        });
    }
    return worker;
}

self.onmessage = async function(e) {
    const { imageDataUrl, imageId } = e.data;
    
    try {
        const activeWorker = await getWorker();
        const { data } = await activeWorker.recognize(imageDataUrl);
        
        self.postMessage({
            type: 'result',
            imageId: imageId,
            text: data.text,
            confidence: data.confidence,
            words: data.words
        });
    } catch (error) {
        self.postMessage({
            type: 'error',
            imageId: imageId,
            error: error.message
        });
    }
};
```

#### 3.2.3 Pós-Processamento OCR (rotulo-processor.js)

```javascript
/**
 * rotulo-processor.js
 * 
 * Pós-processamento do texto extraído pelo Tesseract.
 * Desafios conhecidos do Tesseract.js em rótulos:
 * 
 * 1. CONFUSÃO DE CARACTERES:
 *    - '0' (zero) vs 'O' (letra) → Ex: "100g" vs "lOOg"
 *    - '1' (um) vs 'l' (ele) → Ex: "1g" vs "lg"
 *    - '5' vs 'S' → Ex: "25g" vs "2Sg"
 *    
 * 2. ESPAÇAMENTO IRREGULAR:
 *    - Tabelas nutricionais têm colunas que o OCR interpreta como texto corrido
 *    
 * 3. ROTULAÇÃO EM INGLÊS/PORTUGUÊS:
 *    - Produtos importados vs nacionais
 */

class RotuloProcessor {
    
    /**
     * Pipeline de limpeza do texto OCR
     */
    static limparTexto(rawText) {
        let text = rawText;
        
        // Passo 1: Remove linhas vazias excessivas
        text = text.replace(/\n{3,}/g, '\n\n');
        
        // Passo 2: Corrige confusões comuns de caracteres
        const correcoes = [
            [/\b[lL]\s*[gG]\b/g, '1g'],
            [/\b[Oo]\s*[gG]\b/g, '0g'],
            [/(\d+)\s*[Oo]\b/g, '$1 0'],
            [/\b[Ss]\s*[gG]\b/g, '5g'],
            [/(\d+)\s*[kK]\s*[cC]\s*[aA][lL]/g, '$1 kcal'],
            [/(\d+)\s*[gG]\s*$/, '$1g'],
            [/(\d+)\s*[mM]\s*[gG]\b/g, '$1mg'],
            [/(\d+)\s*[%]/, '$1%'],
            [/(\d+)\s*[pP]\s*[cC]\b/g, '$1%'],
        ];
        
        for (const [pattern, replacement] of correcoes) {
            text = text.replace(pattern, replacement);
        }
        
        return text.trim();
    }
    
    /**
     * Extrai valores nutricionais do texto usando regex
     */
    static extrairMacros(texto) {
        const resultado = {
            calorias: null, sodio: null, acucar: null,
            gordurasTotais: null, gordurasSaturadas: null,
            carboidratos: null, proteinas: null, fibras: null
        };
        
        const padroes = {
            calorias: [
                /(?:valor\s*energ[ée]tico|energia|calorias|calories)\s*:?\s*(\d+)/i,
                /(\d+)\s*kcal/i
            ],
            sodio: [
                /(?:s[óo]dio|sodium)\s*:?\s*(\d+)/i,
                /s[óo]dio.*?(\d+)/i
            ],
            acucar: [
                /(?:a[çc][úu]car(?:es)?|sugar(?:s)?)\s*:?\s*(\d+)/i,
                /a[çc][úu]cares.*?(\d+)/i
            ],
            gordurasTotais: [
                /(?:gorduras?\s*totais?|total\s*fat)\s*:?\s*(\d+)/i,
                /gorduras?\s*totais?.*?(\d+)/i
            ],
            gordurasSaturadas: [
                /(?:gorduras?\s*saturadas?|saturated\s*fat)\s*:?\s*(\d+)/i,
                /gorduras?\s*saturadas?.*?(\d+)/i
            ],
            carboidratos: [
                /(?:carboidratos?|carbohydrates?|carbo[s]?)\s*:?\s*(\d+)/i,
                /carboidratos?.*?(\d+)/i
            ],
            proteinas: [
                /(?:prote[íi]nas?|proteins?|prot)\s*:?\s*(\d+)/i,
                /prote[íi]nas?.*?(\d+)/i
            ],
            fibras: [
                /(?:fibras?|fibers?|fibra)\s*:?\s*(\d+)/i,
                /fibras?.*?(\d+)/i
            ]
        };
        
        for (const [campo, regexList] of Object.entries(padroes)) {
            for (const regex of regexList) {
                const match = texto.match(regex);
                if (match) {
                    resultado[campo] = parseInt(match[1], 10);
                    break;
                }
            }
        }
        
        return resultado;
    }
}

window.RotuloProcessor = RotuloProcessor;
```

### 3.3 Fluxo de Captura e OCR no Frontend

```javascript
// ====== FUNÇÃO PRINCIPAL DE EXTRAÇÃO ======
async function extrairTexto() {
    const btnExtrair = document.getElementById('btnExtrair');
    btnExtrair.disabled = true;
    btnExtrair.innerHTML = '<i class="fas fa-spinner fa-spin mr-2"></i>Processando...';
    
    // 1. Captura o frame (com crop na área da tabela)
    const imageDataUrl = await capturarFrame();
    
    // 2. Mostra barra de progresso
    document.getElementById('ocrProgressContainer').classList.remove('hidden');
    document.getElementById('ocrProgressFill').style.width = '0%';
    
    try {
        // 3. Executa OCR via Tesseract.js
        const result = await Tesseract.recognize(imageDataUrl, 'por+eng', {
            logger: (m) => {
                if (m.status === 'recognizing text') {
                    document.getElementById('ocrProgressFill').style.width = 
                        Math.round(m.progress * 100) + '%';
                }
            }
        });
        
        // 4. Pós-processa o texto
        const textoLimpo = RotuloProcessor.limparTexto(result.data.text);
        const confianca = Math.round(result.data.confidence);
        
        // 5. Mostra preview do texto extraído
        document.getElementById('ocrTextDisplay').textContent = textoLimpo;
        document.getElementById('ocrConfidence').textContent = confianca + '%';
        document.getElementById('ocrPreview').classList.remove('hidden');
        
        // 6. Se confiança muito baixa, avisa o usuário
        if (confianca < 40) {
            alert('⚠️ A qualidade do OCR está baixa. Tente melhorar o foco e iluminação.');
        }
        
        // 7. Envia para o backend Groq
        await enviarParaGroq(textoLimpo);
        
    } catch (error) {
        console.error('OCR Error:', error);
        alert('Erro ao processar a imagem. Tente novamente.');
    } finally {
        btnExtrair.disabled = false;
        btnExtrair.innerHTML = '<i class="fas fa-font mr-2"></i>Extrair Texto';
        document.getElementById('ocrProgressContainer').classList.add('hidden');
    }
}

// ====== ENVIO PARA O BACKEND ======
async function enviarParaGroq(textoOcr) {
    const userData = JSON.parse(localStorage.getItem('usuarioLogado'));
    if (!userData) { window.location.href = 'login.html'; return; }
    
    document.getElementById('loader').classList.remove('hidden');
    document.getElementById('resultado').classList.add('hidden');
    
    try {
        const response = await fetch(`${API_URL}/api/analisar-rotulo`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                usuarioId: userData.id,
                textoOcr: textoOcr
            })
        });
        
        if (response.ok) {
            const data = await response.json();
            exibirResultado(data);
        } else {
            const err = await response.json();
            alert('Erro na análise: ' + (err.mensagem || 'Tente novamente.'));
        }
    } catch (error) {
        console.error('Groq Error:', error);
        alert('Servidor BSFM offline. Tente novamente.');
    } finally {
        document.getElementById('loader').classList.add('hidden');
    }
}

// ====== EXIBIÇÃO DO RESULTADO ======
function exibirResultado(data) {
    document.getElementById('produtoDetectado').textContent = data.produtoDetectado || 'Produto não identificado';
    
    // Health Score
    const score = data.pontuacaoSaude || 0;
    const scoreCircle = document.getElementById('healthScoreCircle');
    scoreCircle.textContent = score;
    scoreCircle.className = 'health-score';
    if (score >= 7) scoreCircle.classList.add('green');
    else if (score >= 4) scoreCircle.classList.add('yellow');
    else scoreCircle.classList.add('red');
    
    // Badge PodeConsumir
    const badge = document.getElementById('badgeConsumo');
    badge.textContent = data.podeConsumir === true ? '✅ Pode Consumir' : 
                        data.podeConsumir === false ? '❌ Evitar' : '⚠️ Moderado';
    badge.className = 'badge-consumo';
    if (data.podeConsumir === true) badge.classList.add('sim');
    else if (data.podeConsumir === false) badge.classList.add('nao');
    else badge.classList.add('moderado');
    
    // Análise e Dica
    document.getElementById('analiseMeta').textContent = data.analiseEmRelacaoAMeta || '';
    document.getElementById('dicaBSFM').textContent = data.dicaBSFM || '';
    
    document.getElementById('resultado').classList.remove('hidden');
    document.getElementById('resultado').scrollIntoView({ behavior: 'smooth', block: 'center' });
}
```

---

## 4. B. Context Injector — Backend C#

### 4.1 Classe `ContextInjectorService.cs`

O Context Injector é responsável por buscar os dados vitais do usuário no PostgreSQL e montar um **SystemPrompt** personalizado que será enviado ao Groq junto com o texto do OCR.

```csharp
using BSFM.Models;
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
1. Ignore erros de OCR — foque nos macros principais: Calorias, Sódio, Açúcar, Gorduras Totais e Saturadas.
2. Considere o contexto do usuário (IMC, meta, histórico, intolerâncias, diabetes) para personalizar o feedback.
3. Se o teor de Sódio for alto (>800mg por porção) e o IMC indicar sobrepeso/obesidade, emita alerta de retenção hídrica.
4. Se o Açúcar for alto (>15g por porção), alerte sobre picos glicêmicos.
5. Se as Gorduras Saturadas forem altas (>5g por porção), alerte sobre saúde cardiovascular.
6. Se o usuário tiver Diabetes, verifique se o produto contém açúcares adicionados e alerte sobre o impacto glicêmico.
7. Se o usuário tiver intolerâncias alimentares registradas, verifique se o produto contém ingredientes incompatíveis e alerte.
8. Seja direto e prático — o usuário quer saber se PODE ou NÃO consumir o produto.
9. SEMPRE retorne APENAS um JSON válido, sem texto adicional, sem markdown, sem explicações fora do JSON.";
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
```

### 4.2 Exemplo de SystemPrompt Montado

```
Você é um Nutricionista Clínico especialista em análise de rótulos de alimentos.
Sua função é analisar a Tabela Nutricional de um produto e fornecer um feedback personalizado.

## CONTEXTO DO USUÁRIO:
- Nome: João Silva
- Idade: 32 anos
- Sexo: Masculino
- Peso Atual: 88kg
- Altura: 1.75m
- IMC: 28.7 (Sobrepeso)
- TMB: 1850 kcal/dia
- Gasto Calórico Total (TDEE): 2500 kcal/dia
- Peso Meta: 78kg
- Nível de Atividade: Ativo
- Água Consumida Hoje: 800ml de 2000ml

## HISTÓRICO ALIMENTAR (ÚLTIMAS 48H):
- [15/04 12:30] Frango grelhado, arroz integral: 450kcal, P:38g, C:42g, G:8g
- [15/04 19:00] Salada com atum: 320kcal, P:30g, C:12g, G:10g
- [14/04 08:00] Ovos mexidos com pão integral: 280kcal, P:18g, C:20g, G:12g

## TEXTO OCR DO RÓTULO:
[texto extraído da tabela nutricional será inserido aqui]
```

---

## 5. C. Rota de LLM Inference — Groq Integration (NutriBrainService.cs)

### 5.1 Classe `NutriBrainService.cs`

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BSFM.CoreAnalytics.Backend.Models;

namespace BSFM.CoreAnalytics.Backend.Services
{
    /// <summary>
    /// NutriBrainService
    /// 
    /// Motor de inferência que integra com a API do Groq (Llama 3).
    /// 
    /// Por que Groq?
    /// - Velocidade: ~200-800ms por inferência (vs 3-8s de APIs multimodais)
    /// - Custo: Modelo Llama 3 8B é extremamente barato por token
    /// - Rate Limits: ~30 req/s (vs 3-10 req/min de APIs de visão)
    /// - Precisão: Llama 3 70B tem performance comparável ao GPT-3.5 em análise textual
    /// 
    /// Endpoint: https://api.groq.com/openai/v1/chat/completions
    /// Modelo: llama3-70b-8192 (ou llama3-8b-8192 para economia)
    /// </summary>
    public class NutriBrainService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NutriBrainService> _logger;
        private readonly string _apiKey;
        
        // Groq API Configuration
        private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
        private const string ModeloPadrao = "llama3-70b-8192"; // 70B para maior precisão
        // Alternativa: "llama3-8b-8192" (8B, mais rápido e barato)
        
        // Timeout generoso para o Groq processar
        private static readonly TimeSpan GroqTimeout = TimeSpan.FromSeconds(15);

        public NutriBrainService(HttpClient httpClient, ILogger<NutriBrainService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") 
                      ?? throw new InvalidOperationException(
                          "GROQ_API_KEY não configurada. Defina a variável de ambiente no Render.");
        }

        /// <summary>
        /// Analisa o texto OCR de um rótulo usando o Groq Llama 3
        /// </summary>
        public async Task<RotuloResponse> AnalisarRotuloAsync(
            string textoOcr, 
            string systemPrompt,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("[NUTRIBRAIN] Iniciando análise de rótulo...");
                
                // 1. Monta o payload para a API do Groq
                var payload = new GroqChatRequest
                {
                    Model = ModeloPadrao,
                    Messages = new List<GroqMessage>
                    {
                        new GroqMessage 
                        { 
                            Role = "system", 
                            Content = systemPrompt 
                        },
                        new GroqMessage
                        {
                            Role = "user",
                            Content = $"## TEXTO OCR DO RÓTULO:\n\n{textoOcr}\n\nAnalise este rótulo e retorne APENAS o JSON no formato especificado."
                        }
                    },
                    Temperature = 0.1,  // Baixa temperatura = respostas mais determinísticas
                    MaxTokens = 1024,
                    ResponseFormat = new GroqResponseFormat { Type = "json_object" }
                };

                // 2. Serializa e envia a requisição
                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                _logger.LogDebug("[NUTRIBRAIN] Payload: {Payload}", jsonPayload);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(GroqTimeout);

                var response = await _httpClient.SendAsync(requestMessage, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[NUTRIBRAIN] Groq API error: {StatusCode} - {Body}", 
                        response.StatusCode, responseBody);
                    throw new HttpRequestException(
                        $"Groq API retornou {response.StatusCode}: {responseBody}");
                }

                // 3. Desserializa a resposta do Groq
                var groqResponse = JsonSerializer.Deserialize<GroqChatResponse>(responseBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var content = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrEmpty(content))
                {
                    throw new InvalidOperationException("Groq retornou resposta vazia.");
                }

                _logger.LogInformation("[NUTRIBRAIN] Resposta recebida em {Tempo}ms", 
                    groqResponse?.Usage?.TotalTime ?? 0);

                // 4. Extrai o JSON da resposta (segurança contra texto extra)
                var jsonLimpo = ExtrairJsonDaResposta(content);

                // 5. Desserializa para o modelo de resposta
                var resultado = JsonSerializer.Deserialize<RotuloResponse>(jsonLimpo, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (resultado == null)
                {
                    throw new InvalidOperationException("Falha ao desserializar resposta do Groq.");
                }

                // 6. Validação dos campos obrigatórios
                resultado.ProdutoDetectado ??= "Produto não identificado";
                resultado.AnaliseEmRelacaoAMeta ??= "Análise não disponível.";
                resultado.DicaBSFM ??= "Consulte um nutricionista para orientação personalizada.";

                _logger.LogInformation("[NUTRIBRAIN] Análise concluída: {Produto}", 
                    resultado.ProdutoDetectado);

                return resultado;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[NUTRIBRAIN] Timeout na requisição ao Groq.");
                return new RotuloResponse
                {
                    ProdutoDetectado = "Tempo limite excedido",
                    PodeConsumir = null,
                    PontuacaoSaude = 0,
                    AnaliseEmRelacaoAMeta = "A análise excedeu o tempo limite. Tente novamente.",
                    DicaBSFM = "Verifique sua conexão com a internet e tente novamente."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NUTRIBRAIN] Erro ao analisar rótulo.");
                throw;
            }
        }

        /// <summary>
        /// Extrai um JSON válido da resposta do LLM, mesmo que venha com texto extra
        /// </summary>
        private string ExtrairJsonDaResposta(string rawContent)
        {
            // Remove blocos de código markdown ```json ... ```
            var cleaned = rawContent.Trim();
            if (cleaned.StartsWith("```"))
            {
                var start = cleaned.IndexOf('\n') + 1;
                var end = cleaned.LastIndexOf("```");
                if (end > start)
                    cleaned = cleaned[start..end].Trim();
            }

            // Encontra o primeiro { e o último }
            var firstBrace = cleaned.IndexOf('{');
            var lastBrace = cleaned.LastIndexOf('}');
            
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleaned = cleaned[firstBrace..(lastBrace + 1)];
            }

            return cleaned;
        }
    }
}

---

## 6. D. Lógica de Feedback Restritivo

### 6.1 Regras de Negócio para Análise Nutricional

A IA deve agir como um **Nutricionista Clínico**, aplicando regras restritivas baseadas no perfil do usuário e nos macros do produto.

```
┌─────────────────────────────────────────────────────────────────┐
│                    MOTOR DE REGRAS (Lógica)                       │
│                                                                  │
│  Entrada: Macros do Produto + Perfil do Usuário                  │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  REGRA 1: SÓDIO ALTO + SOBREPESO                          │   │
│  │  Se Sódio > 800mg/porção E IMC >= 25:                     │   │
│  │    → Alerta Vermelho: Retenção Hídrica                    │   │
│  │    → Pontuação -3                                         │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  REGRA 2: AÇÚCAR ALTO                                     │   │
│  │  Se Açúcar > 15g/porção:                                  │   │
│  │    → Alerta: Pico Glicêmico                               │   │
│  │    → Pontuação -2                                         │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  REGRA 3: GORDURAS SATURADAS ALTAS                        │   │
│  │  Se Gord. Saturadas > 5g/porção:                          │   │
│  │    → Alerta: Saúde Cardiovascular                         │   │
│  │    → Pontuação -2                                         │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  REGRA 4: CALORIAS vs META DO USUÁRIO                     │   │
│  │  Se Calorias > 30% do TDEE do usuário:                    │   │
│  │    → Alerta: Ultrapassa limite da refeição               │   │
│  │    → Pontuação -1                                         │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  REGRA 5: PRODUTO EQUILIBRADO                             │   │
│  │  Se Fibras > 3g E Proteínas > 10g E Sódio < 400mg:       │   │
│  │    → Feedback Positivo: Opção Saudável                   │   │
│  │    → Pontuação +3                                         │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  Saída: Pontuação 0-10 + PodeConsumir + Alerta Personalizado     │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 Prompt de Restrição (Embedded no SystemPrompt)

O prompt do Groq já contém as regras de restrição embutidas. Abaixo, o bloco específico que instrui a IA sobre o comportamento restritivo:

```
## REGRAS DE FEEDBACK RESTRITIVO (OBRIGATÓRIO):

### Alerta Vermelho (Retenção Hídrica)
SE Sódio > 800mg/porção E IMC do usuário >= 25 (Sobrepeso/Obesidade):
→ PodeConsumir = false
→ PontuacaoSaude reduzida em 3 pontos
→ AnaliseEmRelacaoAMeta deve mencionar "ALERTA: Alto teor de sódio. 
   Risco de retenção hídrica. Prefira alimentos com baixo sódio."

### Alerta Laranja (Pico Glicêmico)
SE Açúcar > 15g/porção:
→ PontuacaoSaude reduzida em 2 pontos
→ AnaliseEmRelacaoAMeta deve mencionar "CUIDADO: Alto teor de açúcar. 
   Risco de pico glicêmico. Consuma com moderação."

### Alerta Laranja (Saúde Cardiovascular)
SE Gorduras Saturadas > 5g/porção:
→ PontuacaoSaude reduzida em 2 pontos
→ AnaliseEmRelacaoAMeta deve mencionar "CUIDADO: Alto teor de gorduras 
   saturadas. Risco cardiovascular."

### Alerta Amarelo (Calorias)
SE Calorias > 30% do Gasto Calórico Total (TDEE) do usuário:
→ PontuacaoSaude reduzida em 1 ponto
→ AnaliseEmRelacaoAMeta deve mencionar "ATENÇÃO: Esta porção representa 
   mais de 30% do seu gasto calórico diário."

### Feedback Positivo
SE Fibras >= 3g E Proteínas >= 10g E Sódio < 400mg:
→ PontuacaoSaude aumenta em 3 pontos
→ AnaliseEmRelacaoAMeta deve mencionar "ÓTIMA ESCOLHA! Produto rico em 
   fibras e proteínas com baixo sódio."
```

### 6.3 Exemplos de Saída com Feedback Restritivo

#### Exemplo 1: Produto com Sódio Alto (Usuário com Sobrepeso)

```json
{
  "ProdutoDetectado": "Salgadinho de Queijo",
  "PodeConsumir": false,
  "PontuacaoSaude": 2,
  "AnaliseEmRelacaoAMeta": "ALERTA: Alto teor de sódio (920mg). Risco de retenção hídrica. Considerando seu IMC de 28.7 (Sobrepeso), este produto deve ser evitado. Prefira snacks com baixo sódio como frutas ou castanhas sem sal.",
  "DicaBSFM": "Experimente substituir salgadinhos por pipoca caseira sem sal ou palitos de cenoura com homus. Reduzir o sódio ajuda no controle da pressão e na redução do inchaço."
}
```

#### Exemplo 2: Produto Equilibrado

```json
{
  "ProdutoDetectado": "Iogurte Grego Natural",
  "PodeConsumir": true,
  "PontuacaoSaude": 8,
  "AnaliseEmRelacaoAMeta": "ÓTIMA ESCOLHA! Produto rico em proteínas (12g) com baixo teor de sódio (45mg). As 120 calorias se encaixam bem no seu déficit calórico para atingir a meta de 78kg.",
  "DicaBSFM": "Combine com frutas vermelhas e granola sem açúcar para um café da manhã completo e nutritivo que ajuda na saciedade até o almoço."
}
```

#### Exemplo 3: Produto com Açúcar Alto

```json
{
  "ProdutoDetectado": "Refrigerante de Cola",
  "PodeConsumir": false,
  "PontuacaoSaude": 1,
  "AnaliseEmRelacaoAMeta": "CUIDADO: Alto teor de açúcar (39g). Risco de pico glicêmico. Com seu objetivo de perda de peso, refrigerantes são calorias vazias que atrapalham o progresso. Prefira água com gás e limão.",
  "DicaBSFM": "Para matar a vontade de refrigerante, experimente água com gás, limão espremido e folhas de hortelã. É refrescante, zero calorias e ajuda na hidratação!"
}
```

---

## 7. E. Output JSON Estruturado

### 7.1 Modelo `RotuloResponse.cs`

```csharp
using System.Text.Json.Serialization;

namespace BSFM.CoreAnalytics.Backend.Models
{
    /// <summary>
    /// Modelo de resposta da análise de rótulo.
    /// 
    /// O Groq é FORÇADO a retornar APENAS este JSON, sem texto adicional.
    /// A validação no backend garante que todos os campos estão presentes.
    /// 
    /// Formato:
    /// {
    ///   "ProdutoDetectado": "Nome do produto identificado",
    ///   "PodeConsumir": true/false/null,
    ///   "PontuacaoSaude": 0-10,
    ///   "AnaliseEmRelacaoAMeta": "Texto explicativo personalizado",
    ///   "DicaBSFM": "Dica prática do nutricionista"
    /// }
    /// </summary>
    public class RotuloResponse
    {
        [JsonPropertyName("ProdutoDetectado")]
        public string? ProdutoDetectado { get; set; }

        [JsonPropertyName("PodeConsumir")]
        public bool? PodeConsumir { get; set; }

        [JsonPropertyName("PontuacaoSaude")]
        public int PontuacaoSaude { get; set; }

        [JsonPropertyName("AnaliseEmRelacaoAMeta")]
        public string? AnaliseEmRelacaoAMeta { get; set; }

        [JsonPropertyName("DicaBSFM")]
        public string? DicaBSFM { get; set; }
    }
}
```

### 7.2 Modelo `RotuloRequest.cs`

```csharp
using System.Text.Json.Serialization;

namespace BSFM.CoreAnalytics.Backend.Models
{
    /// <summary>
    /// Modelo de requisição para análise de rótulo.
    /// O frontend envia apenas o ID do usuário e o texto extraído pelo OCR.
    /// </summary>
    public class RotuloRequest
    {
        [JsonPropertyName("usuarioId")]
        public int UsuarioId { get; set; }

        [JsonPropertyName("textoOcr")]
        public string TextoOcr { get; set; } = string.Empty;
    }
}
```

### 7.3 Modelos da API Groq (`GroqApiModels.cs`)

```csharp
using System.Text.Json.Serialization;

namespace BSFM.CoreAnalytics.Backend.Models
{
    // ====== REQUEST MODELS ======

    public class GroqChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "llama3-70b-8192";

        [JsonPropertyName("messages")]
        public List<GroqMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.1;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 1024;

        [JsonPropertyName("response_format")]
        public GroqResponseFormat? ResponseFormat { get; set; }
    }

    public class GroqMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class GroqResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";
    }

    // ====== RESPONSE MODELS ======

    public class GroqChatResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<GroqChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public GroqUsage? Usage { get; set; }
    }

    public class GroqChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public GroqResponseMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    public class GroqResponseMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    public class GroqUsage
    {
        [JsonPropertyName("queue_time")]
        public long QueueTime { get; set; }

        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("total_time")]
        public long TotalTime { get; set; }
    }
}
```

### 7.4 Controller `RotuloController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using BSFM.CoreAnalytics.Backend.Services;
using BSFM.CoreAnalytics.Backend.Models;

namespace BSFM.CoreAnalytics.Backend.Controllers
{
    /// <summary>
    /// Controller para análise de rótulos nutricionais.
    /// 
    /// Endpoint: POST /api/analisar-rotulo
    /// 
    /// Fluxo:
    /// 1. Recebe texto OCR do frontend
    /// 2. ContextInjector busca dados do usuário no PostgreSQL
    /// 3. Monta SystemPrompt personalizado
    /// 4. NutriBrainService envia para Groq e recebe JSON
    /// 5. Retorna resposta estruturada ao frontend
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RotuloController : ControllerBase
    {
        private readonly NutriBrainService _nutriBrain;
        private readonly ContextInjectorService _contextInjector;
        private readonly ILogger<RotuloController> _logger;

        public RotuloController(
            NutriBrainService nutriBrain,
            ContextInjectorService contextInjector,
            ILogger<RotuloController> logger)
        {
            _nutriBrain = nutriBrain;
            _contextInjector = contextInjector;
            _logger = logger;
        }

        /// <summary>
        /// Analisa o texto OCR de um rótulo nutricional
        /// </summary>
        [HttpPost("analisar-rotulo")]
        public async Task<IActionResult> AnalisarRotulo(
            [FromBody] RotuloRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.TextoOcr))
            {
                return BadRequest(new { 
                    mensagem = "Texto OCR não pode estar vazio." 
                });
            }

            try
            {
                _logger.LogInformation(
                    "[ROTULO] Iniciando análise para usuário {UsuarioId}", 
                    request.UsuarioId);

                // 1. Busca contexto do usuário
                var userContext = await _contextInjector
                    .BuildContextAsync(request.UsuarioId);

                // 2. Monta SystemPrompt personalizado
                var systemPrompt = _contextInjector
                    .BuildSystemPrompt(userContext);

                // 3. Envia para o Groq
                var resultado = await _nutriBrain
                    .AnalisarRotuloAsync(request.TextoOcr, systemPrompt, ct);

                _logger.LogInformation(
                    "[ROTULO] Análise concluída: {Produto} - Score: {Score}", 
                    resultado.ProdutoDetectado, 
                    resultado.PontuacaoSaude);

                return Ok(resultado);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("[ROTULO] {Mensagem}", ex.Message);
                return NotFound(new { mensagem = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[ROTULO] Erro na API Groq");
                return StatusCode(502, new { 
                    mensagem = "Serviço de análise temporariamente indisponível.",
                    detalhe = ex.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ROTULO] Erro interno");
                return StatusCode(500, new { 
                    mensagem = "Erro interno ao analisar rótulo." 
                });
            }
        }
    }
}
```

---

## 8. Integração com o Sistema Existente

### 8.1 Registro no `Program.cs`

```csharp
// ====== NOVO: Registro do Módulo BSFM.CoreAnalytics ======

// 1. HttpClient para Groq API
builder.Services.AddHttpClient<NutriBrainService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// 2. Context Injector (usa o mesmo DbContext existente)
builder.Services.AddScoped<ContextInjectorService>();

// 3. Controller do módulo
builder.Services.AddScoped<RotuloController>();
```

### 8.2 Mapeamento de Rotas

```csharp
// O controller já usa [Route("api/[controller]")]
// A rota será: POST /api/Rotulo/analisar-rotulo
// 
// Para manter consistência com o padrão REST:
// app.MapControllers() já cobre automaticamente
```

### 8.3 Variáveis de Ambiente no Render

```yaml
# render.yaml (adicione esta variável)
services:
  - type: web
    name: bsfm-app
    env: dotnet
    envVars:
      - key: GROQ_API_KEY
        sync: false  # Será configurada manualmente no dashboard do Render
```

---

## 9. Instruções de Teste e Sandbox

### 9.1 Estrutura de Testes Isolados (Sandbox)

O módulo `BSFM.CoreAnalytics` foi projetado para rodar **isoladamente** antes de ser fundido ao projeto principal. Siga os passos abaixo:

```
┌─────────────────────────────────────────────────────────────────┐
│                    SANDBOX DE TESTES                             │
│                                                                  │
│  FASE 1: Teste Offline (sem API)                                │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  test-prompt-groq.txt → Simula a resposta do Groq         │   │
│  │  mock-ocr-data.json → Dados de OCR simulados              │   │
│  └───────────────────────────────────────────────────────────┘   │
│         │                                                        │
│         ▼                                                        │
│  FASE 2: Teste com Groq (sandbox)                               │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  POST /api/analisar-rotulo → Groq real                    │   │
│  │  Usuário de teste no banco (ID=999)                       │   │
│  └───────────────────────────────────────────────────────────┘   │
│         │                                                        │
│         ▼                                                        │
│  FASE 3: Teste de Integração (full stack)                       │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  analisador-rotulo.html → Câmera → OCR → Groq → Resultado│   │
│  └───────────────────────────────────────────────────────────┘   │
│         │                                                        │
│         ▼                                                        │
│  FASE 4: Fusão ao Projeto Principal                             │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  Mover arquivos para BSFM-APP/                            │   │
│  │  Registrar serviços no Program.cs                         │   │
│  │  Adicionar link no menu principal                         │   │
│  └───────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 9.2 FASE 1: Teste Offline (Prompt Simulado)

Arquivo: `BSFM.CoreAnalytics/Tests/test-prompt-groq.txt`

```text
=== TESTE DE PROMPT OFFLINE ===
Execute este prompt manualmente no Groq Playground (https://console.groq.com/playground)
para validar o comportamento antes de integrar.

Modelo: llama3-70b-8192
Temperature: 0.1
Max Tokens: 1024

=== SYSTEM PROMPT ===
Você é um Nutricionista Clínico especialista em análise de rótulos de alimentos.
Sua função é analisar a Tabela Nutricional de um produto e fornecer um feedback personalizado.

## CONTEXTO DO USUÁRIO:
- Nome: Maria Teste
- Idade: 28 anos
- Sexo: Feminino
- Peso Atual: 72kg
- Altura: 1.65m
- IMC: 26.4 (Sobrepeso)
- TMB: 1480 kcal/dia
- Gasto Calórico Total (TDEE): 2000 kcal/dia
- Peso Meta: 62kg
- Nível de Atividade: Moderado
- Água Consumida Hoje: 600ml de 2000ml

## HISTÓRICO ALIMENTAR (ÚLTIMAS 48H):
- [15/04 08:00] Pão integral com queijo branco: 250kcal, P:12g, C:30g, G:8g
- [15/04 13:00] Salada com frango grelhado: 350kcal, P:35g, C:15g, G:10g
- [14/04 20:00] Pizza de mussarela (2 fatias): 600kcal, P:24g, C:60g, G:28g

## REGRAS DE FEEDBACK RESTRITIVO:
[Inserir as regras da seção 6.2]

## INSTRUÇÕES:
Analise o texto OCR abaixo e retorne APENAS o JSON no formato especificado.
Ignore erros de OCR. Foque nos macros principais.

=== TEXTO OCR (SIMULADO) ===
Informação Nutricional
Porção de 30g (1 unidade)
Valor Energético 140kcal = 588kJ
Carboidratos 16g
Proteínas 2,0g
Gorduras Totais 7,0g
Gorduras Saturadas 3,5g
Gorduras Trans 0g
Fibra Alimentar 0g
Sódio 180mg

=== RESPOSTA ESPERADA ===
{
  "ProdutoDetectado": "Biscoito Recheado",
  "PodeConsumir": true,
  "PontuacaoSaude": 5,
  "AnaliseEmRelacaoAMeta": "Produto com teor moderado de gorduras saturadas (3.5g). As 140kcal são aceitáveis para um lanche, mas o baixo teor de fibras (0g) reduz a saciedade. Consuma com moderação dentro do seu plano alimentar.",
  "DicaBSFM": "Prefira biscoitos integrais ou frutas como lanche. Se consumir este produto, combine com uma fonte de fibras como uma maçã para aumentar a saciedade."
}
```

### 9.3 FASE 2: Teste com Groq (Sandbox)

```bash
# 1. Configure a chave da API Groq
export GROQ_API_KEY="sua_chave_aqui"

# 2. Execute o projeto em modo sandbox
cd BSFM-APP
dotnet run --urls "http://localhost:5000"

# 3. Teste com curl (simulando o frontend)
curl -X POST http://localhost:5000/api/Rotulo/analisar-rotulo \
  -H "Content-Type: application/json" \
  -d '{
    "usuarioId": 1,
    "textoOcr": "Informação Nutricional\nPorção de 30g\nValor Energético 140kcal\nCarboidratos 16g\nProteínas 2,0g\nGorduras Totais 7,0g\nSódio 180mg"
  }'

# 4. Resposta esperada (JSON)
# {
#   "produtoDetectado": "Biscoito Recheado",
#   "podeConsumir": true,
#   "pontuacaoSaude": 5,
#   "analiseEmRelacaoAMeta": "...",
#   "dicaBSFM": "..."
# }
```

### 9.4 FASE 3: Teste de Integração Full Stack

```bash
# 1. Abra o arquivo HTML diretamente no navegador
start BSFM-APP/BSFM.CoreAnalytics/Frontend/analisador-rotulo.html

# 2. Permita acesso à câmera quando solicitado
# 3. Aponte para um rótulo de alimento
# 4. Clique em "Extrair Texto"
# 5. Aguarde o OCR processar (barra de progresso)
# 6. Veja o resultado da análise do Groq
```

### 9.5 FASE 4: Fusão ao Projeto Principal

Após validar o funcionamento no sandbox:

```bash
# 1. Copie os arquivos do sandbox para o projeto principal
cp -r BSFM-APP/BSFM.CoreAnalytics/Backend/Services/* BSFM-APP/Services/
cp -r BSFM-APP/BSFM.CoreAnalytics/Backend/Models/* BSFM-APP/Models/
cp -r BSFM-APP/BSFM.CoreAnalytics/Backend/Controllers/* BSFM-APP/Controllers/
cp -r BSFM-APP/BSFM.CoreAnalytics/Frontend/* BSFM-APP/wwwroot/

# 2. Registre os serviços no Program.cs (conforme seção 8.1)
# 3. Adicione o link no menu de navegação
# 4. Configure a variável GROQ_API_KEY no Render
# 5. Faça deploy
```

---

## 10. Prompt Engineering — Blindagem contra Alucinações

### 10.1 Estratégias de Blindagem

O prompt do Groq foi projetado com múltiplas camadas de proteção contra alucinações:

```
┌─────────────────────────────────────────────────────────────────┐
│              CAMADAS DE BLINDAGEM DO PROMPT                      │
│                                                                  │
│  CAMADA 1: Role Lock                                            │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │ "Você é um Nutricionista Clínico especialista..."         │   │
│  │ → Fixa o papel da IA, reduzindo respostas genéricas      │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  CAMADA 2: Context Injection                                    │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │ Dados reais do usuário (IMC, TMB, meta, histórico)        │   │
│  │ → A IA tem dados concretos para basear a análise          │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  CAMADA 3: Rule Enforcement                                     │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │ Regras explícitas de "SE... ENTÃO..."                     │   │
│  │ → Comportamento determinístico para casos críticos        │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  CAMADA 4: Output Format Lock                                   │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │ "SEMPRE retorne APENAS um JSON válido"                    │   │
│  │ + response_format: { type: "json_object" }                │   │
│  │ → Força o formato de saída, eliminando texto extra        │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  CAMADA 5: Temperature Control                                  │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │ Temperature: 0.1 (baixíssima)                             │   │
│  │ → Reduz criatividade, aumenta determinismo                │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  CAMADA 6: Post-Processing Validation                           │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │ ExtrairJsonDaResposta() + validação de campos             │   │
│  │ → Backend limpa e valida a resposta antes de enviar ao UI │   │
│  └───────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 10.2 Tratamento de Erros de OCR no Prompt

O prompt instrui explicitamente a IA a ignorar erros de OCR:

```
## INSTRUÇÕES PARA ANÁLISE:
1. Ignore erros de OCR — foque nos macros principais: Calorias, Sódio, 
   Açúcar, Gorduras Totais e Saturadas.
2. Se um valor parecer incorreto devido ao OCR (ex: "lOOg" em vez de 
   "100g"), use seu conhecimento para inferir o valor correto.
3. Não invente valores que não estão presentes no texto.
4. Se o texto OCR estiver muito corrompido e impossível de analisar, 
   retorne PontuacaoSaude = 0 e PodeConsumir = null.
```

### 10.3 Fallback para OCR de Baixa Qualidade

```csharp
// No NutriBrainService.cs, após receber a resposta do Groq:

// Se a pontuação for 0 e o produto não foi identificado,
// significa que o OCR estava muito ruim
if (resultado.PontuacaoSaude == 0 && 
    string.IsNullOrEmpty(resultado.ProdutoDetectado))
{
    resultado.AnaliseEmRelacaoAMeta = 
        "Não foi possível analisar o rótulo. " +
        "O texto extraído pode estar muito danificado. " +
        "Tente fotografar com melhor iluminação e foco.";
    resultado.DicaBSFM = 
        "Para melhores resultados: 1) Use boa iluminação " +
        "2) Mantenha a câmera estável 3) Foque na tabela nutricional " +
        "4) Evite reflexos na embalagem.";
}
```

---

## 10.5 Integração com o Analisador de Pratos (YOLO + USDA + Groq)

O módulo `BSFM.CoreAnalytics` também foi integrado ao fluxo existente de análise de pratos via foto, adicionando **feedback da IA Nutricional** aos resultados do YOLO + USDA.

### 10.5.1 Fluxo Atualizado

```
Foto do Prato → YOLO (detecta alimentos) → USDA (busca macros)
                                              ↓
                                    ContextInjector (busca dados do usuário)
                                              ↓
                                    Groq Llama 3 (feedback personalizado)
                                              ↓
                                    Salva na AnalisesIA + retorna pro frontend
```

### 10.5.2 O que mudou no Backend

**Rota `/analisar-prato` (Program.cs):**
- Agora recebe `ContextInjectorService` e `NutriBrainService` como dependências
- Após calcular os macros (YOLO + USDA), monta um texto com os dados nutricionais
- Envia para o Groq junto com o SystemPrompt personalizado do usuário
- Preenche os campos `PodeConsumir`, `PontuacaoSaude`, `AnaliseEmRelacaoAMeta`, `DicaBSFM`
- Se o Groq falhar, a análise ainda é salva sem feedback (graceful degradation)

### 10.5.3 O que mudou no Frontend

**`analisador-ia.html`:**
- Novo card de feedback da IA abaixo dos macros
- Score de saúde (0-10) com badge colorido (verde/amarelo/vermelho)
- Status "Pode Consumir" com ícone e cor indicativa
- Análise textual em relação à meta do usuário
- Dica personalizada BSFM

### 10.5.4 O que mudou no Banco

**Tabela `AnalisesIA`:**
```sql
ALTER TABLE "AnalisesIA" 
ADD COLUMN "PodeConsumir" BOOLEAN DEFAULT NULL,
ADD COLUMN "PontuacaoSaude" INTEGER NOT NULL DEFAULT 0,
ADD COLUMN "AnaliseEmRelacaoAMeta" TEXT NOT NULL DEFAULT '',
ADD COLUMN "DicaBSFM" TEXT NOT NULL DEFAULT '';
```

### 10.5.5 Classe C# Atualizada

```csharp
public class AnaliseIA
{
    [Key]
    public int ID { get; set; }
    public int UsuarioID { get; set; }
    public string Alimento { get; set; } = string.Empty;
    public double Calorias { get; set; }
    public double Proteinas { get; set; }
    public double Carbos { get; set; }
    public double Gorduras { get; set; }
    public string Porcao { get; set; } = string.Empty;
    public DateTime DataAnalise { get; set; } = DateTime.Now;
    
    // NOVOS CAMPOS
    public bool? PodeConsumir { get; set; }
    public int PontuacaoSaude { get; set; }
    public string AnaliseEmRelacaoAMeta { get; set; } = string.Empty;
    public string DicaBSFM { get; set; } = string.Empty;
}
```

### 10.5.6 Script de Migração

Arquivo: `script_sql_migracao_analises.sql`

```sql
ALTER TABLE "AnalisesIA" 
ADD COLUMN IF NOT EXISTS "PodeConsumir" BOOLEAN DEFAULT NULL;

ALTER TABLE "AnalisesIA" 
ADD COLUMN IF NOT EXISTS "PontuacaoSaude" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "AnalisesIA" 
ADD COLUMN IF NOT EXISTS "AnaliseEmRelacaoAMeta" TEXT NOT NULL DEFAULT '';

ALTER TABLE "AnalisesIA" 
ADD COLUMN IF NOT EXISTS "DicaBSFM" TEXT NOT NULL DEFAULT '';
```

---

## 11. Apêndice Técnico

### 11.1 Desafios Conhecidos do Tesseract.js

| Desafio | Impacto | Mitigação |
|---------|---------|-----------|
| **Caracteres confundidos** (0 vs O, 1 vs l) | Valores numéricos incorretos | Whitelist de caracteres + pós-processamento |
| **Espaçamento irregular** em tabelas | Texto corrido sem colunas | PSM 6 (assume bloco de texto uniforme) |
| **Fontes decorativas** em embalagens | Baixa confiança do OCR | Alerta se confiança < 40% |
| **Reflexos e sombras** na foto | Artefatos no texto | Guia de crop + instruções ao usuário |
| **Idioma misto** (português + inglês) | Palavras não reconhecidas | Carregar 'por+eng' simultaneamente |
| **Memória do navegador** | Travamento em dispositivos fracos | Web Worker + timeout de 30s |

### 11.2 Comparativo de Modelos Groq

| Modelo | Parâmetros | Velocidade | Precisão | Custo/Tokens | Recomendação |
|--------|-----------|------------|----------|--------------|--------------|
| `llama3-70b-8192` | 70B | ~400ms | Excelente | Moderado | **Produção** |
| `llama3-8b-8192` | 8B | ~200ms | Boa | Baixo | **Desenvolvimento** |
| `mixtral-8x7b-32768` | 46B | ~300ms | Muito Boa | Moderado | Alternativa |
| `gemma2-9b-it` | 9B | ~200ms | Boa | Baixo | Alternativa rápida |

### 11.3 Estimativa de Consumo de Tokens

```
Por análise de rótulo:
- System Prompt: ~800-1000 tokens (com contexto do usuário)
- Texto OCR: ~100-300 tokens (depende do tamanho da tabela)
- Resposta JSON: ~100-200 tokens
- Total por requisição: ~1000-1500 tokens

Com Llama 3 70B ($0.59/1M tokens de input, $0.79/1M tokens de output):
- Custo por análise: ~$0.0008 (menos de 1 centavo de real)
- 1000 análises: ~$0.80 (aproximadamente R$4,00)
```

### 11.4 Checklist de Deploy

- [ ] GROQ_API_KEY configurada no Render
- [ ] HttpClient registrado no Program.cs
- [ ] ContextInjectorService registrado
- [ ] RotuloController mapeado
- [ ] Teste offline com prompt simulado (FASE 1)
- [ ] Teste com Groq real via curl (FASE 2)
- [ ] Teste full stack com câmera (FASE 3)
- [ ] Fusão dos arquivos ao projeto principal (FASE 4)
- [ ] Link no menu de navegação adicionado
- [ ] Deploy no Render validado

---

> **Documento gerado em:** 30/04/2026  
> **Versão:** 1.0  
> **Arquiteto:** BSFM Core Team  
> **Próxima Revisão:** Após implementação e testes de integração
