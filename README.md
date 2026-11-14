# BSFM — Brazilian System of Food Metric

Plataforma de acompanhamento nutricional: registro de alimentos, cálculo de macros,
hidratação, metas e evolução corporal.

O repositório contém **duas aplicações**:

| | v1 (legado) | v2 (nova) |
|---|---|---|
| Onde | raiz do repositório | `v2/` |
| Stack | .NET 8, Minimal API, HTML/JS | .NET 10, Controllers + EF Core, React |
| Autenticação | sem token (só `localStorage`) | JWT + assinatura HMAC por sessão |
| Nutrição | YOLO próprio + USDA | catálogo TACO (582 alimentos) + visão multimodal |
| Deploy | serviço `bsfm-api` (Render, .NET) | serviço `bsfm-v2` (Render, Docker) |

As duas convivem no mesmo Blueprint. A v1 segue funcionando; a v2 sobe em paralelo
para migração gradual.

---

## Aplicativo instalável (PWA)

A v2 é instalável no celular e no desktop, com o mesmo código que roda no navegador:

- **desktop** — sidebar fixa, atalhos e tema claro/escuro;
- **celular** — barra inferior de navegação, alvos de toque de 44 px e área segura (notch);
- **instalado** — abre em tela cheia, ícone próprio, sem barra do navegador, e o shell
  continua disponível offline.

O convite de instalação aparece na barra lateral (desktop) e em **Perfil → Aplicativo**
(celular). Em iOS, use Compartilhar → "Adicionar à Tela de Início".

Arquivos: `v2/web/public/manifest.webmanifest`, `v2/web/public/sw.js` e os ícones em
`v2/web/public/icons` (os mesmos da identidade da v1).

O service worker **nunca** cacheia `/api/*`: dado de saúde não pode ficar velho nem
vazar entre sessões.

---
> **Nota de build:** `MeusApp.csproj` declara `<Compile Remove="v2/**/*.cs" />`.
> Sem isso, o SDK globa todos os `.cs` a partir da raiz e o build da v1 tenta compilar
> os arquivos da v2, falhando com `CS8802` (mais de um arquivo com top-level statements).
> O Dockerfile da v1 continua disponível como `Dockerfile.v1`.
## Arquitetura da v2

```
v2/
├── backend/
│   ├── BSFM.Seguranca/     criptografia pura — token, HMAC, PBKDF2, TOTP
│   ├── BSFM.Dados/         entidades, DbContext, migrations (Postgres)
│   ├── BSFM.Nutricao/      catálogo TACO, busca tolerante, visão multimodal
│   ├── BSFM.Api/           controllers, filtros, serviços de aplicação
│   └── BSFM.*.Verificacao/ harness de verificação (console)
├── web/                    React 18 + Vite
└── Dockerfile              front + API em um único container
```

Dependência sempre aponta para baixo: `Api → Dados → Seguranca`. Nenhuma camada
inferior conhece a superior.

### Modelo de segurança

O ponto central é: **a identidade vem do token, nunca da URL.**

1. **Login** verifica senha (PBKDF2, com promoção automática de hash BCrypt legado),
   aplica bloqueio progressivo e, se habilitado, exige TOTP.
2. **Sessão** é persistida em `Sessoes` com `jti`, permitindo revogar antes de expirar.
3. **Cada requisição** a `/api/eu/*` envia:

```
Authorization: Bearer <token>
X-BSFM-Timestamp: 1763078400
X-BSFM-Nonce: 9f2c1a7b8e4d0563
X-BSFM-Signature: <hex>
```

A assinatura é `HMAC-SHA256` sobre `MÉTODO\nROTA\nTIMESTAMP\nNONCE\nSHA256(corpo)`,
usando uma **chave derivada por sessão**:

```
chaveSessao = HMAC-SHA256(segredoMestre, "bsfm:sessao:v1:" + jti)
```

O login devolve a chave uma única vez; o navegador guarda em `sessionStorage` e
nunca vê o segredo mestre. Revogar a sessão revoga a chave.

---

## Como rodar a v2 localmente

Pré-requisitos: **.NET SDK 10** e **Node 20+**.

```bash
# API (SQLite local, seed automático)
cd v2/backend/BSFM.Api
dotnet run
# http://localhost:5100

# Front (proxy para a API)
cd v2/web
npm install
npm run dev
# http://localhost:5173
```

Credenciais de desenvolvimento (semeadas em SQLite):

| Usuário | Senha | Observação |
|---|---|---|
| `ana@bsfm.dev` | `SenhaForte#2026` | login simples |
| `bia@bsfm.dev` | `SenhaForte#2026` | exige 2FA (TOTP) |
| `carla@bsfm.dev` | `SenhaForte#2026` | hash BCrypt legado |

---

## Verificação

Seis suítes, **266 asserções**, todas passando:

```bash
cd v2/backend/BSFM.Seguranca.Verificacao && dotnet run   # 47
cd v2/backend/BSFM.Dados.Verificacao     && dotnet run   # 53
cd v2/backend/BSFM.Nutricao.Verificacao  && dotnet run   # 66

# com a API rodando em :5100
powershell -File v2/backend/smoke-s2.ps1                 # 32
powershell -File v2/backend/smoke-s3.ps1                 # 38
powershell -File v2/backend/smoke-s4.ps1                 # 31
```

Verificação visual (exige Chrome com `--remote-debugging-port=9222`):

```bash
cd v2/web
node verificar-tela.mjs http://127.0.0.1:5100 http://localhost:4173 tela
```

---

## Variáveis de ambiente

### v2 (`bsfm-v2`)

| Variável | Obrigatória | Padrão | Para que serve |
|---|---|---|---|
| `BSFM_JWT_SECRET` | recomendada | derivado de `DATABASE_URL` | assina o token de sessão |
| `BSFM_HMAC_SECRET` | recomendada | derivado de `DATABASE_URL` | deriva a chave por sessão; é o segredo mestre |
| `DATABASE_URL` | **sim em produção** | SQLite local | Postgres (Neon/Railway) |
| `PORT` | sim (Render injeta) | `5100` | porta HTTP |
| `BREVO_API_KEY` | **sim em produção** | vazio | envio do código de verificação por e-mail (sem ela, o código só aparece no log) |
| `BSFM_EMAIL_REMETENTE` | não | `brazilian.sfm@gmail.com` | remetente verificado no Brevo |
| `BSFM_EMAIL_NOME` | não | `Portal BSFM` | nome do remetente |
| `BSFM_GEMINI_API_KEY` | não | vazio | reconhecimento de alimento por imagem |
| `BSFM_GEMINI_MODEL` | não | `gemini-2.5-flash` | modelo de visão |
| `BSFM_ORIGENS_PERMITIDAS` | não | `localhost:5173/4173` | CORS explícito |
| `BSFM_CATALOGO` | não | `dados/taco.csv` | caminho do catálogo |
| `BSFM_BD_PROVIDER` | não | `npgsql` | `sqlite` para desenvolvimento |

> No `render.yaml`, `BSFM_JWT_SECRET` e `BSFM_HMAC_SECRET` usam `generateValue: true`:
> o Render gera e mantém. Não é preciso criá-las à mão.

> **Resolução dos segredos.** O serviço não cai se `BSFM_JWT_SECRET`/`BSFM_HMAC_SECRET`
> não estiverem configurados: ele deriva os dois de `DATABASE_URL` com HKDF-SHA256 e
> registra um aviso no log. A derivação é determinística, então as sessões sobrevivem a
> reinícios. Sem `DATABASE_URL`, os segredos são aleatórios por processo e cada restart
> encerra as sessões. Não existe valor fixo no código — seria público e permitiria forjar
> tokens.
>
> Definir as variáveis explicitamente é o caminho recomendado: permite rotação planejada
> sem depender da senha do banco.

### v1 (legado)

`DATABASE_URL`, `BREVO_API_KEY`, `USDA_API_KEY`, `PORT`, `ASPNETCORE_ENVIRONMENT`.

---

## Deploy no Render

O Blueprint (`render.yaml`) sobe dois serviços. Para a v2:

1. O repositório precisa estar conectado ao Render.
2. Configure `DATABASE_URL` (o mesmo Postgres da v1).
3. Opcional: `BSFM_GEMINI_API_KEY`.
4. Deploy. O healthcheck é `/saude`.

As migrations da v2 são **aditivas**: criam `Sessoes`, `RefreshTokens`, `Auditoria`,
`ConsentimentosLGPD`, `Metas` e `Alimentos`. Nenhuma tabela existente é removida.

> Se a migration falhar por e-mail duplicado no índice único, o serviço continua
> no ar e registra a remediação no log. Rode o SQL de deduplicação descrito na
> documentação (`DOC-03`, seção 1.6) e reinicie.

---

## Endpoints principais da v2

| Método | Rota | Autenticação |
|---|---|---|
| `POST` | `/api/auth/cadastrar` | pública |
| `POST` | `/api/auth/verificar-email` | código TOTP |
| `POST` | `/api/auth/login` | pública |
| `POST` | `/api/auth/refresh` | refresh token |
| `POST` | `/api/auth/logout` | refresh token |
| `POST` | `/api/auth/esqueci-senha` | pública (202 sempre) |
| `POST` | `/api/auth/redefinir-senha` | código TOTP |
| `GET` | `/api/eu/perfil` | sessão + assinatura |
| `GET/PUT` | `/api/eu/saude` | sessão + assinatura |
| `GET` | `/api/eu/evolucao` | sessão + assinatura |
| `GET/POST/DELETE` | `/api/eu/agua` | sessão + assinatura |
| `GET/POST/DELETE` | `/api/eu/refeicoes` | sessão + assinatura |
| `GET/PUT` | `/api/eu/metas` | sessão + assinatura |
| `GET/POST/DELETE` | `/api/eu/analises` | sessão + assinatura |
| `GET` | `/api/nutricao/alimentos?q=` | pública |
| `POST` | `/api/nutricao/por-nome` | pública |
| `POST` | `/api/nutricao/por-imagem` | pública |
| `GET` | `/saude` | pública |

---

## Documentação

Em repositório separado:
[Brazilian-System-of-Food-Metric/software-documentation](https://github.com/Brazilian-System-of-Food-Metric/software-documentation)

Inclui a engenharia reversa do site de referência, a análise do app original, o
schema do banco, o relatório de migração das rotas e a camada nutricional.

---

## Licença

Ver `LICENSE`.
