// Cliente da API do BSFM.
//
// Responsabilidade única: montar a requisição com sessão + assinatura HMAC e
// devolver JSON ou erro tipado. Nenhuma tela monta cabeçalho na mão.

const CHAVE_TOKEN = 'bsfm.token'
const CHAVE_REFRESH = 'bsfm.refresh'
const CHAVE_ASSINATURA = 'bsfm.chave'

// A chave de assinatura fica em sessionStorage: sobrevive a recarregar a aba,
// desaparece ao fechá-la. Nunca vai para localStorage.
export const sessao = {
  get token() {
    return sessionStorage.getItem(CHAVE_TOKEN)
  },
  get refreshToken() {
    return sessionStorage.getItem(CHAVE_REFRESH)
  },
  get chave() {
    return sessionStorage.getItem(CHAVE_ASSINATURA)
  },
  definir({ token, refreshToken, chaveAssinatura, expiraEmSegundos }) {
    sessionStorage.setItem(CHAVE_TOKEN, token)
    sessionStorage.setItem(CHAVE_REFRESH, refreshToken)
    sessionStorage.setItem(CHAVE_ASSINATURA, chaveAssinatura)
    if (expiraEmSegundos) {
      sessionStorage.setItem('bsfm.expira', String(Date.now() + expiraEmSegundos * 1000))
    }
  },
  limpar() {
    for (const k of [CHAVE_TOKEN, CHAVE_REFRESH, CHAVE_ASSINATURA, 'bsfm.expira']) {
      sessionStorage.removeItem(k)
    }
  },
  get ativa() {
    return Boolean(this.token && this.chave)
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

  // Mesma string canônica do servidor: METODO\nCAMINHO\nTS\nNONCE\nSHA256(corpo)
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

async function requisitar(metodo, caminho, corpo = null, { autenticado = true } = {}) {
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

  if (resposta.status === 401 && autenticado) {
    sessao.limpar()
    throw new ErroApi('Sessão encerrada. Entre novamente.', 401, null)
  }

  const conteudo = resposta.headers.get('content-type')?.includes('json')
    ? await resposta.json().catch(() => null)
    : await resposta.text().catch(() => null)

  if (!resposta.ok) {
    const mensagem =
      conteudo?.mensagem ?? conteudo?.erro ?? conteudo?.title ?? `Falha na requisição (${resposta.status})`
    throw new ErroApi(mensagem, resposta.status, conteudo)
  }

  return conteudo
}

export const api = {
  login: (email, senha) =>
    requisitar('POST', '/api/auth/login', { email, senha }, { autenticado: false }),

  cadastrar: (dados) => requisitar('POST', '/api/auth/cadastrar', dados, { autenticado: false }),

  esqueciSenha: (email) =>
    requisitar('POST', '/api/auth/esqueci-senha', { email }, { autenticado: false }),

  redefinirSenha: (email, codigo, novaSenha) =>
    requisitar('POST', '/api/auth/redefinir-senha', { email, codigo, novaSenha }, { autenticado: false }),

  lerRotulo: (texto) =>
    requisitar('POST', '/api/nutricao/rotulo', { texto }, { autenticado: false }),

  registrarAnaliseManual: (dados) => requisitar('POST', '/api/eu/analises', dados),

  aguaSemanal: () => requisitar('GET', '/api/eu/agua/semanal'),

  verificarEmail: (email, codigo) =>
    requisitar('POST', '/api/auth/verificar-email', { email, codigo }, { autenticado: false }),

  logout: () =>
    requisitar('POST', '/api/auth/logout', { refreshToken: sessao.refreshToken }, { autenticado: false }),

  perfil: () => requisitar('GET', '/api/eu/perfil'),

  metas: () => requisitar('GET', '/api/eu/metas'),

  metasHistorico: () => requisitar('GET', '/api/eu/metas/historico'),

  definirMeta: (dados) => requisitar('PUT', '/api/eu/metas', dados),

  refeicoes: () => requisitar('GET', '/api/eu/refeicoes'),

  criarRefeicao: (dados) => requisitar('POST', '/api/eu/refeicoes', dados),

  removerRefeicao: (id) => requisitar('DELETE', `/api/eu/refeicoes/${id}`),

  resumo: () => requisitar('GET', '/api/eu/analises/resumo'),

  aguaHoje: () => requisitar('GET', '/api/eu/agua'),

  registrarAgua: (ml) => requisitar('POST', '/api/eu/agua', { ml }),

  buscarAlimentos: (consulta) =>
    requisitar('GET', `/api/nutricao/alimentos?q=${encodeURIComponent(consulta)}&limite=6`, null, {
      autenticado: false,
    }),

  previa: (nome, gramas) =>
    requisitar('POST', '/api/nutricao/por-nome', { nome, gramas }, { autenticado: false }),

  analisarImagem: (imagemBase64, gramas) =>
    requisitar('POST', '/api/nutricao/por-imagem', { imagemBase64, gramas }, { autenticado: false }),

  registrarAnalise: (nome, gramas) =>
    requisitar('POST', '/api/eu/analises/do-catalogo', { nome, gramas }),

  analises: (limite = 30) => requisitar('GET', `/api/eu/analises?limite=${limite}`),

  evolucao: () => requisitar('GET', '/api/eu/evolucao'),

  sessoes: () => requisitar('GET', '/api/eu/sessoes'),
}
