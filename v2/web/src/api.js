// Cliente da API do BSFM.
//
// Responsabilidade única: montar a requisição com sessão + assinatura HMAC e
// devolver JSON ou erro tipado. Nenhuma tela monta cabeçalho na mão.
//
// Segurança preservada: o token e a chave de assinatura continuam em
// sessionStorage (somem ao fechar a aba) e o refresh rotaciona no servidor.

const CHAVE_TOKEN = 'bsfm.token'
const CHAVE_REFRESH = 'bsfm.refresh'
const CHAVE_ASSINATURA = 'bsfm.chave'
const CHAVE_EXPIRA = 'bsfm.expira'

// Persistência: a sessão vive em localStorage para o usuário não precisar entrar
// de novo a cada vez que abre o app. O token de acesso é curto; quem mantém a
// sessão viva é o refresh, que rotaciona a cada uso no servidor.
//
// Contrapartida conhecida: um script malicioso na mesma origem conseguiria ler
// essas chaves. Por isso a chave HMAC é de sessão (não serve para outro
// dispositivo) e o refresh é de uso único — se for usado duas vezes, a sessão é
// revogada no servidor.
const armazem = window.localStorage

// Migração silenciosa de quem já usava sessionStorage.
try {
  const antigo = window.sessionStorage.getItem(CHAVE_TOKEN)
  if (antigo && !armazem.getItem(CHAVE_TOKEN)) {
    for (const k of [CHAVE_TOKEN, CHAVE_REFRESH, CHAVE_ASSINATURA, CHAVE_EXPIRA]) {
      const valor = window.sessionStorage.getItem(k)
      if (valor) armazem.setItem(k, valor)
    }
    window.sessionStorage.clear()
  }
} catch {
  /* modo restrito: segue sem migrar */
}

export const sessao = {
  get token() {
    return armazem.getItem(CHAVE_TOKEN)
  },
  get refreshToken() {
    return armazem.getItem(CHAVE_REFRESH)
  },
  get chave() {
    return armazem.getItem(CHAVE_ASSINATURA)
  },
  get expiraEm() {
    return Number(armazem.getItem(CHAVE_EXPIRA) ?? 0)
  },
  definir({ token, refreshToken, chaveAssinatura, expiraEmSegundos }) {
    if (token) armazem.setItem(CHAVE_TOKEN, token)
    if (refreshToken) armazem.setItem(CHAVE_REFRESH, refreshToken)
    if (chaveAssinatura) armazem.setItem(CHAVE_ASSINATURA, chaveAssinatura)
    if (expiraEmSegundos) {
      armazem.setItem(CHAVE_EXPIRA, String(Date.now() + expiraEmSegundos * 1000))
    }
  },
  limpar() {
    for (const k of [CHAVE_TOKEN, CHAVE_REFRESH, CHAVE_ASSINATURA, CHAVE_EXPIRA]) {
      armazem.removeItem(k)
      try {
        window.sessionStorage.removeItem(k)
      } catch {
        /* ignora */
      }
    }
  },
  /** Há sessão recuperável? Basta o refresh — o acesso é renovado sozinho. */
  get ativa() {
    return Boolean(this.chave && (this.token || this.refreshToken))
  },
}
function base64UrlParaBytes(texto) {
  const normalizado = texto.replace(/-/g, '+').replace(/_/g, '/')
  const comPadding = normalizado + '='.repeat((4 - (normalizado.length % 4)) % 4)
  const binario = atob(comPadding)
  const bytes = new Uint8Array(binario.length)
  for (let i = 0; i < binario.length; i += 1) bytes[i] = binario.charCodeAt(i)
  return bytes
}

function hex(buffer) {
  return Array.from(new Uint8Array(buffer))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
}

async function sha256Hex(texto) {
  const dados = new TextEncoder().encode(texto)
  return hex(await crypto.subtle.digest('SHA-256', dados))
}

async function assinar(metodo, caminho, corpo, chave) {
  const timestamp = Math.floor(Date.now() / 1000).toString()
  const nonce = crypto.randomUUID().replace(/-/g, '')

  // Mesma string canônica do servidor: METODO\nCAMINHO(com query)\nTS\nNONCE\nSHA256(corpo)
  const canonica = [metodo.toUpperCase(), caminho, timestamp, nonce, await sha256Hex(corpo)].join('\n')

  const chaveCripto = await crypto.subtle.importKey(
    'raw',
    base64UrlParaBytes(chave),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign'],
  )

  const assinatura = hex(await crypto.subtle.sign('HMAC', chaveCripto, new TextEncoder().encode(canonica)))

  return { timestamp, nonce, assinatura }
}

export class ErroApi extends Error {
  constructor(mensagem, status, corpo) {
    super(mensagem)
    this.name = 'ErroApi'
    this.status = status
    this.corpo = corpo
  }
}

// ---------------------------------------------------------------------------
// Renovação de sessão. O token de acesso dura 2 h; o refresh dura 30 dias e é
// rotacionado a cada uso. Uma única renovação por vez, mesmo com chamadas
// concorrentes — o refresh é de uso único no servidor.
// ---------------------------------------------------------------------------

let renovando = null

async function renovarSessao() {
  if (!sessao.refreshToken) return false
  if (renovando) return renovando

  renovando = (async () => {
    try {
      const resposta = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json; charset=utf-8' },
        body: JSON.stringify({ refreshToken: sessao.refreshToken }),
      })
      if (!resposta.ok) return false

      const dados = await resposta.json().catch(() => null)
      if (!dados?.token || !dados?.chaveAssinatura) return false

      sessao.definir(dados)
      return true
    } catch {
      return false
    } finally {
      renovando = null
    }
  })()

  return renovando
}

async function requisitar(metodo, caminho, corpo = null, { autenticado = true, tentarRenovar = true } = {}) {
  const texto = corpo === null ? '' : JSON.stringify(corpo)
  const cabecalhos = { 'Content-Type': 'application/json; charset=utf-8' }

  if (autenticado) {
    if (!sessao.ativa) throw new ErroApi('Sessão expirada. Entre novamente.', 401, null)

    const { timestamp, nonce, assinatura } = await assinar(metodo, caminho, texto, sessao.chave)
    cabecalhos.Authorization = `Bearer ${sessao.token}`
    cabecalhos['X-BSFM-Timestamp'] = timestamp
    cabecalhos['X-BSFM-Nonce'] = nonce
    cabecalhos['X-BSFM-Signature'] = assinatura
  }

  const resposta = await fetch(caminho, { method: metodo, headers: cabecalhos, body: texto || undefined })

  // 401 em rota autenticada: tenta renovar uma vez. Se o refresh falhar, a
  // sessão acabou de verdade e a tela volta para o login.
  if (resposta.status === 401 && autenticado && tentarRenovar) {
    if (await renovarSessao()) {
      return requisitar(metodo, caminho, corpo, { autenticado, tentarRenovar: false })
    }
    sessao.limpar()
    throw new ErroApi('Sessão encerrada. Entre novamente.', 401, null)
  }

  const conteudo = resposta.headers.get('content-type')?.includes('json')
    ? await resposta.json().catch(() => null)
    : await resposta.text().catch(() => null)

  if (!resposta.ok) {
    const mensagem =
      conteudo?.mensagem ?? conteudo?.erro ?? conteudo?.title ?? `Falha na requisição (${resposta.status})`
    // Em 400/5xx a rota é a informação que falta para diagnosticar. Só nesses
    // casos o texto ganha o sufixo, para não poluir erros de formulário.
    const rota = caminho.split('?')[0]
    const texto =
      resposta.status >= 500 || mensagem === 'Requisição inválida'
        ? `${mensagem} (${resposta.status} em ${rota})`
        : mensagem
    throw new ErroApi(texto, resposta.status, conteudo)
  }

  return conteudo
}

export const api = {
  // --- autenticação e conta ------------------------------------------------
  login: (email, senha, codigoTotp = null) =>
    requisitar('POST', '/api/auth/login', { email, senha, codigoTotp }, { autenticado: false }),

  cadastrar: (dados) => requisitar('POST', '/api/auth/cadastrar', dados, { autenticado: false }),

  esqueciSenha: (email) =>
    requisitar('POST', '/api/auth/esqueci-senha', { email }, { autenticado: false }),

  redefinirSenha: (email, codigo, novaSenha) =>
    requisitar('POST', '/api/auth/redefinir-senha', { email, codigo, novaSenha }, { autenticado: false }),

  verificarEmail: (email, codigo) =>
    requisitar('POST', '/api/auth/verificar-email', { email, codigo }, { autenticado: false }),

  reenviarVerificacao: (email) =>
    requisitar('POST', '/api/auth/reenviar-verificacao', { email }, { autenticado: false }),

  logout: () =>
    requisitar('POST', '/api/auth/logout', { refreshToken: sessao.refreshToken }, { autenticado: false }),

  perfil: () => requisitar('GET', '/api/eu/perfil'),

  alterarNome: (nome) => requisitar('PUT', '/api/eu/nome', { nome }),

  trocarSenha: (senhaAtual, novaSenha) =>
    requisitar('PUT', '/api/eu/senha', { senhaAtual, novaSenha }),

  trocarEmail: (email, senha) => requisitar('PUT', '/api/eu/email', { email, senha }),

  definirNascimento: (dataNascimento) => requisitar('PUT', '/api/eu/nascimento', { dataNascimento }),

  atualizarSaude: (diabetes, intolerancia) =>
    requisitar('PUT', '/api/eu/perfil/saude', { diabetes, intolerancia }),

  atualizarMedidas: (peso, altura) => requisitar('PUT', '/api/eu/saude', { peso, altura }),

  sessoes: () => requisitar('GET', '/api/eu/sessoes'),

  encerrarSessao: (id) => requisitar('DELETE', `/api/eu/sessoes/${id}`),

  encerrarOutrasSessoes: () => requisitar('DELETE', '/api/eu/sessoes'),

  // --- segundo fator -------------------------------------------------------
  iniciar2fa: (senha) => requisitar('POST', '/api/eu/2fa/iniciar', { senha }),

  ativar2fa: (codigo) => requisitar('POST', '/api/eu/2fa/ativar', { codigo }),

  desativar2fa: (senha, codigo) => requisitar('POST', '/api/eu/2fa/desativar', { senha, codigo }),

  // --- privacidade ---------------------------------------------------------
  exportar: () => requisitar('GET', '/api/eu/exportar'),

  excluirConta: (senha, confirmacao) =>
    requisitar('POST', '/api/eu/conta/excluir', { senha, confirmacao }),

  // --- nutrição ------------------------------------------------------------
  buscarAlimentos: (consulta) =>
    requisitar('GET', `/api/nutricao/alimentos?q=${encodeURIComponent(consulta)}&limite=6`, null, {
      autenticado: false,
    }),

  previa: (nome, gramas) =>
    requisitar('POST', '/api/nutricao/por-nome', { nome, gramas }, { autenticado: false }),

  analisarImagem: (imagemBase64, gramas) =>
    requisitar('POST', '/api/nutricao/por-imagem', { imagemBase64, gramas }, { autenticado: false }),

  provedor: () => requisitar('GET', '/api/nutricao/provedor', null, { autenticado: false }),

  lerRotulo: (texto) => requisitar('POST', '/api/nutricao/rotulo', { texto }, { autenticado: false }),

  lerPrescricao: (texto) =>
    requisitar('POST', '/api/nutricao/prescricao', { texto }, { autenticado: false }),

  /** Avalia a refeição com meta, consumo do dia e perfil de saúde lidos no servidor. */
  avaliar: ({ kcal, proteinas, carbos, gorduras, fibra = null }) =>
    requisitar('POST', '/api/eu/analises/avaliar', { kcal, proteinas, carbos, gorduras, fibra }),

  // --- diário --------------------------------------------------------------
  analises: (limite = 60) => requisitar('GET', `/api/eu/analises?limite=${limite}`),

  registrarAnalise: (nome, gramas) =>
    requisitar('POST', '/api/eu/analises/do-catalogo', { nome, gramas }),

  registrarAnaliseManual: (dados) => requisitar('POST', '/api/eu/analises', dados),

  removerAnalise: (id) => requisitar('DELETE', `/api/eu/analises/${id}`),

  resumo: () => requisitar('GET', '/api/eu/analises/resumo'),

  evolucao: () => requisitar('GET', '/api/eu/evolucao'),

  // --- água ----------------------------------------------------------------
  aguaHoje: () => requisitar('GET', '/api/eu/agua'),

  aguaSemanal: () => requisitar('GET', '/api/eu/agua/semanal'),

  registrarAgua: (ml) => requisitar('POST', '/api/eu/agua', { ml }),

  removerUltimaAgua: () => requisitar('DELETE', '/api/eu/agua/ultimo'),

  // --- metas ---------------------------------------------------------------
  metas: () => requisitar('GET', '/api/eu/metas'),

  metasHistorico: () => requisitar('GET', '/api/eu/metas/historico'),

  definirMeta: (dados) => requisitar('PUT', '/api/eu/metas', dados),

  // --- planos --------------------------------------------------------------
  refeicoes: () => requisitar('GET', '/api/eu/refeicoes'),

  criarRefeicao: (dados) => requisitar('POST', '/api/eu/refeicoes', dados),

  atualizarRefeicao: (id, dados) => requisitar('PUT', `/api/eu/refeicoes/${id}`, dados),

  concluirRefeicao: (id, concluida) =>
    requisitar('PUT', '/api/eu/refeicoes/' + id + '/concluir', { concluida }),

  removerRefeicao: (id) => requisitar('DELETE', `/api/eu/refeicoes/${id}`),
}

export { renovarSessao }
