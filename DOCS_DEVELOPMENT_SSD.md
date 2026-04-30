Para elevar o **BSFM (Brazilian System of Food Metric)** de um protótipo para uma plataforma de nível industrial (SaaS Profissional), preparei este documento seguindo as normas **SSD (Software Specification Document)**. 

Este arquivo foi estruturado especificamente para o **Cline (IA de Desenvolvimento)** processar e agir. Ele contém o mapeamento de dependências, arquitetura de banco e o **"Manifesto de Design Profissional"** para transformar a UI atual.

---

# 📄 BSFM: Professional Specification & Architecture Guide (SSD Standard)

> **Projeto:** Brazilian System of Food Metric (BSFM)  
> **Status:** Upgrade de Protótipo para Produção v2.0  
> **Diretiva Principal:** Transição de UI/UX rudimentar para Design Systems Premium (Apple Health/LifeSum Style).

---

## 1. Mapeamento Tecnológico (Stack)
Para guiar o desenvolvimento do Cline, os limites técnicos atuais são:

*   **Backend:** ASP.NET Core 8.0 (Minimal APIs) - Focado em Performance e Escalabilidade.
*   **Database:** PostgreSQL (Cloud via Railway) com suporte a JSONB para históricos complexos.
*   **ORM:** Entity Framework Core (Code-First Pattern).
*   **Inteligência Artificial:** 
    *   Computer Vision: YOLOv10 (Formato ONNX) via `YoloDotNet`.
    *   Knowledge Base: USDA Nutrition API (Data sourcing nutricional).
*   **Serviços de Infra:** Brevo API (SMTP Transitário para Segurança).
*   **Frontend Profissional:**
    *   Framework: Tailwind CSS v3+.
    *   Animações: Interações fluidas e transições de página (Modern Smoothness).
    *   Icons: Lucide Icons ou FontAwesome 6 Pro (SVGs limpos).

---

## 2. Arquitetura de Dados (Data Modeling)
A IA (Cline) deve respeitar a estrutura das tabelas existentes, mas otimizar os tipos de dados:

### Tabelas Críticas:
1.  **`Usuarios`**: Registra dados biométricos centrais + `PesoMeta` + `SenhaHash` (BCrypt).
2.  **`Historicos`**: Série temporal de peso/altura/IMC (Usado para o motor de gráficos).
3.  **`analises_ia`**: Histórico efêmero (48h) de detecção de alimentos (Lógica de Auto-Cleanup via `BackgroundService`).

---

## 3. Diretivas de Interface Profissional (UI/UX Branding)
Para remover a cara de "protótipo", o Cline deve aplicar o seguinte **System Design**:

### 🟢 Estética "Organic Modern"
*   **Paleta de Cores Primária:** Emerald-600 (Ação), Slate-900 (Textos/Dark Elements), Sky-500 (Hidratação/Destaques).
*   **Tipografia:** `Inter` para leitura técnica e `Lexend` ou `Outfit` para cabeçalhos (estilo modernista).
*   **Formas:** Bordas altamente arredondadas (`rounded-3xl` / `2.5rem`), Sombras suaves de elevação (`shadow-xl` com baixíssima opacidade).

### ✨ Elementos Exigidos:
1.  **Dashboard Visual:** Abandonar listas de texto e usar **Cards de Métrica Glassmorphism** com indicadores de variação (+- 2% vs ontem).
2.  **Feedback da IA:** Não exibir apenas "Carbos: 30g". Exibir **Barras de Progresso Progressivas** e recomendações por cor (Verde = Meta Batida, Laranja = Cuidado com sódio).
3.  **Skeleton Screens:** Durante o carregamento da análise da câmera, usar estados de carregamento elegantes, nunca travar a UI.

---

## 4. Backlog de Implementação (Páginas Faltantes)

| Módulo | Objetivo Profissional | Nível de Prioridade |
| :--- | :--- | :--- |
| **SOS Saúde (UPA Map)** | Dashboard Geográfico usando Mapbox ou Leaflet para listar hospitais reais. | ALTA |
| **Evolução em Gráficos** | Integração da biblioteca `Chart.js` ou `ApexCharts` para linha de peso. | ALTA |
| **Central de LIBRAS** | Player de vídeo flutuante com suporte a PIP (Picture-in-Picture) e legendas inteligentes. | MÉDIA |
| **Configurações de Perfil** | Upload de foto (gravatar), troca de senha e exportação de PDF do plano alimentar. | MÉDIA |

---

## 5. Instruções Críticas para o CLINE (IA)
*Ao iniciar tarefas neste repositório, siga as seguintes diretivas de engenharia:*

1.  **Clean Code:** Sempre separe a lógica C# em `Services` e os modelos em `DTOs`. Não use "Hard Coded" nas URLs (sempre via `EnvironmentVariable`).
2.  **Cross-Platform UI:** Garanta que todos os arquivos `.html` usem `meta viewport` perfeito. O app deve ser um PWA-Ready (funcionar como se fosse um app no celular).
3.  **HTTPS Everywhere:** Nunca use URLs sem segurança; todos os caminhos do Railway devem ser HTTPS.
4.  **Error Handling:** Se uma análise da IA falhar, retorne um "Card de Erro Amigável" explicando por que não foi detectado (falta de iluminação, alimento desconhecido, etc), nunca exiba mensagens técnicas para o usuário final.

---

## 📋 Visão Geral
Este documento registra as decisões de desenvolvimento, padrões e mudanças realizadas no projeto BSFM (Body Science Food Manager).

---

## 🎨 Padrões de Design Frontend (SaaS Professional)

### Cores e Tema
- **Cor Primária**: Emerald (#10b981) - representa saúde e nutrição
- **Cores de Apoio**:
  - Blue (#3b82f6) - para elementos de informação/metas
  - Orange (#f97316) - para ações secundárias/diário
  - Red (#ef4444) - para emergências/SOS
  - Slate (#64748b) - para texto secundário

### Tipografia
- **Fonte Principal**: Inter (Google Fonts)
- **Pesos Utilizados**: 300, 400, 500, 600, 700, 800
- **Tamanhos**:
  - Títulos: text-2xl a text-4xl
  - Corpo: text-sm a text-base
  - Legendas: text-xs

### Componentes UI

#### Cards
- **Border Radius**: 1.5rem (24px) para cards principais
- **Padding**: 1.5rem a 1.75rem
- **Background**: rgba(255, 255, 255, 0.95) com backdrop-filter blur(10px)
- **Border**: 1px solid rgba(226, 232, 240, 0.8)
- **Hover Effect**: translateY(-4px) com box-shadow esverdeado
- **Transição**: cubic-bezier(0.4, 0, 0.2, 1) - 0.3s

#### Botões
- **Primário**: Gradiente emerald (#10b981 → #059669)
- **Secundário**: Gradiente slate (#f1f5f9 → #e2e8f0)
- **Dark**: Gradiente dark (#0f172a → #1e293b)
- **Danger**: Gradiente red (#ef4444 → #dc2626)
- **Border Radius**: 1rem (16px)
- **Padding**: 0.875rem a 1.25rem vertical, 1.5rem a 1.75rem horizontal
- **Hover**: translateY(-2px) com aumento de shadow

#### Barras de Progresso
- **Track**: Gradiente slate (#e2e8f0 → #cbd5e1)
- **Fill**: Gradiente emerald (#10b981 → #34d399)
- **Height**: 0.5rem a 0.625rem
- **Border Radius**: 9999px (full)
- **Animação**: width transition 1s-1.5s cubic-bezier

### Animações

#### Fade In Up
```css
@keyframes fadeInUp {
  0% { opacity: 0; transform: translateY(20px); }
  100% { opacity: 1; transform: translateY(0); }
}
```
- **Duração**: 0.6s
- **Easing**: ease-out
- **Preenchimento**: forwards

#### Delays Escalonados
- `.animate-delay-100`: 100ms
- `.animate-delay-200`: 200ms
- `.animate-delay-300`: 300ms
- `.animate-delay-400`: 400ms
- `.animate-delay-500`: 500ms
- `.animate-delay-600`: 600ms

---

## 📝 Changelog

### [2.0.0] - 2026-04-02 - Refatoração SaaS Professional

#### ✨ Novas Funcionalidades
- **Dashboard com animações de entrada**: Cards aparecem com fade-in-up escalonado
- **Métricas animadas**: Valores de IMC, TMB e TDEE com contagem animada
- **Gráficos em Metas.html**: Chart.js integrado com dados reais do backend
- **Analisador IA redesenhado**: Viewfinder profissional com efeitos de escaneamento

#### 🎨 Mudanças de Design
- **dashboard.html**:
  - Novo layout com glassmorphism sidebar
  - Cards de métricas com barras de progresso visuais
  - Botões com gradientes e efeitos hover aprimorados
  - Header sticky com backdrop blur
  - Data atual exibida no header

- **analisador-ia.html**:
  - Viewfinder com cantos decorativos e linha de escaneamento animada
  - Seletor de modo (Câmera/Galeria) com estados visuais
  - Seletor de porção com botões destacados
  - Card de resultados com header verde gradiente
  - Loader com spinner animado

- **metas.html**:
  - Gráfico de Peso com Chart.js (linha verde com área preenchida)
  - Gráfico de IMC com Chart.js (linha azul com área preenchida)
  - Empty states para quando não há dados
  - Contador de registros em cada gráfico
  - Atualização automática após nova pesagem
  - Barra de progresso da meta com porcentagem

#### 🔧 Melhorias Técnicas
- **API Integration**: Todas as chamadas usam endpoint correto `/evolucao/{usuarioId}`
- **Error Handling**: Mensagens de erro amigáveis e fallback states
- **Performance**: Chart.js com maintainAspectRatio: false para melhor responsividade
- **LocalStorage**: Sincronização automática após ações do usuário

#### 📱 Responsive Design
- Mobile-first approach com Tailwind CSS
- Sidebar colapsável em mobile com overlay
- Grid layouts adaptativos (1-2-3 colunas)
- Inputs e botões com tamanhos touch-friendly

---

## 🏗️ Arquitetura de Componentes

### Estrutura de Páginas
```
wwwroot/
├── index.html          # Página inicial/Landing
├── login.html          # Autenticação
├── dashboard.html      # Painel principal (REFATORADO)
├── diario.html         # Diário alimentar
├── analisador-ia.html  # Análise de pratos com IA (REFATORADO)
├── metas.html          # Metas e gráficos (REFATORADO + GRÁFICOS)
```

### Backend Endpoints Utilizados
- `POST /analisar-prato` - Envia foto para análise IA
- `GET /evolucao/{usuarioId}` - Busca histórico de peso/IMC
- `POST /atualizar-medicao` - Registra nova pesagem
- `POST /definir-meta` - Define peso meta do usuário

---

## 🎯 Próximas Melhorias Sugeridas
1. Adicionar modo escuro (dark mode)
2. Implementar notificações toast em vez de alerts
3. Adicionar exportação de dados (PDF/CSV)
4. Implementar PWA para instalação mobile
5. Adicionar gráficos de evolução de macros
6. Criar sistema de conquistas/badges

---

## 📚 Referências de Design
- Tailwind CSS Documentation
- Chart.js Documentation
- FontAwesome Icons
- Google Fonts - Inter

---

*Última atualização: 02/04/2026*