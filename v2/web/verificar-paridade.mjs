// Verifica a paridade com o site original: landing pública, instalação e as
// telas que existiam antes (planos, metas) mais análise por foto.
// Uso: node verificar-paridade.mjs <url-api> <url-app>

import fs from 'node:fs'

const [apiUrl = 'http://127.0.0.1:5100', appUrl = 'http://localhost:4173'] = process.argv.slice(2)
const espera = (ms) => new Promise((r) => setTimeout(r, ms))

let falhas = 0
const conferir = (nome, ok, detalhe = '') => {
  console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${nome}${ok || !detalhe ? '' : `  -> ${detalhe}`}`)
  if (!ok) falhas++
}

const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
const paginas = alvos.filter((t) => t.type === 'page')
const alvo = paginas.find((t) => t.url.startsWith(appUrl)) ?? paginas[0]

const ws = new WebSocket(alvo.webSocketDebuggerUrl)
await new Promise((r, j) => {
  ws.addEventListener('open', r)
  ws.addEventListener('error', j)
})

let id = 1
const pendentes = new Map()
ws.addEventListener('message', (e) => {
  const m = JSON.parse(e.data)
  if (m.id && pendentes.has(m.id)) {
    const { ok, erro } = pendentes.get(m.id)
    pendentes.delete(m.id)
    m.error ? erro(new Error(m.error.message)) : ok(m.result)
  }
})
const cdp = (metodo, params = {}) =>
  new Promise((ok, erro) => {
    const i = id++
    pendentes.set(i, { ok, erro })
    ws.send(JSON.stringify({ id: i, method: metodo, params }))
  })

const avaliar = async (expressao) => {
  const r = await cdp('Runtime.evaluate', { expression: expressao, returnByValue: true, awaitPromise: true })
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.text ?? 'erro')
  return r.result.value
}

async function capturar(nome) {
  const c = await cdp('Page.captureScreenshot', { format: 'png' })
  fs.writeFileSync(`paridade-${nome}.png`, Buffer.from(c.data, 'base64'))
}

await cdp('Page.enable')
await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false })

await cdp('Page.navigate', { url: appUrl })
await espera(2500)
await avaliar(`sessionStorage.clear(); localStorage.clear();`)
await cdp('Page.reload')
await espera(2600)

// ---- landing -------------------------------------------------------------
const landing = JSON.parse(await avaliar(`JSON.stringify({
  h1: document.querySelector('.hero h1')?.textContent ?? null,
  secoes: [...document.querySelectorAll('h2')].map(e => e.textContent.trim()),
  links: document.querySelectorAll('.landing-nav a').length,
  docs: document.querySelectorAll('.doc').length,
  botaoInstalar: [...document.querySelectorAll('button')].some(b => /instalar/i.test(b.textContent)),
  botaoEntrar: [...document.querySelectorAll('button')].some(b => /entrar/i.test(b.textContent)),
  manifest: document.querySelector('link[rel=manifest]')?.href ?? null
})`))

console.log('\n[1] Landing pública')
conferir('h1 presente', Boolean(landing.h1), landing.h1 ?? '')
conferir('ao menos 6 seções', landing.secoes.length >= 6, `${landing.secoes.length}`)
conferir('navegação com 4 links', landing.links === 4, `${landing.links}`)
conferir('link para documentação', landing.docs >= 4, `${landing.docs}`)
conferir('botão instalar visível', landing.botaoInstalar)
conferir('botão entrar visível', landing.botaoEntrar)
conferir('manifest linkado', Boolean(landing.manifest))
await capturar('landing')

// ---- login a partir da landing ------------------------------------------
console.log('\n[2] Navegação para o login')
await avaliar(`[...document.querySelectorAll('button')].find(b => /entrar/i.test(b.textContent))?.click()`)
await espera(900)
const login = JSON.parse(await avaliar(`JSON.stringify({
  h1: document.querySelector('h1')?.textContent ?? null,
  campos: document.querySelectorAll('.cartao-acesso input').length,
  instalar: [...document.querySelectorAll('button')].some(b => /instalar/i.test(b.textContent)),
  voltar: [...document.querySelectorAll('button')].some(b => /voltar ao site/i.test(b.textContent))
})`))
conferir('formulário de login aberto', login.h1 === 'Entrar', login.h1 ?? '')
conferir('campos de e-mail e senha', login.campos >= 2, `${login.campos}`)
conferir('instalar disponível antes do login', login.instalar)
conferir('voltar ao site', login.voltar)
await capturar('login')

// ---- aplicativo ---------------------------------------------------------
console.log('\n[3] Aplicativo (abas restauradas)')
const sessao = await (await fetch(`${apiUrl}/api/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'carla@bsfm.dev', senha: 'SenhaForte#2026' }),
})).json()

await avaliar(`
  sessionStorage.setItem('bsfm.token', ${JSON.stringify(sessao.token)});
  sessionStorage.setItem('bsfm.refresh', ${JSON.stringify(sessao.refreshToken)});
  sessionStorage.setItem('bsfm.chave', ${JSON.stringify(sessao.chaveAssinatura)});
  location.href = '/'; location.reload();
`)
await espera(3200)

const abas = JSON.parse(await avaliar(`JSON.stringify({
  titulos: [...document.querySelectorAll('.nav-item')].map(b => b.textContent.trim()),
  barra: [...document.querySelectorAll('.aba')].map(b => b.textContent.trim()),
  erro: document.querySelector('.erro')?.textContent ?? null
})`))
console.log('  abas:', abas.titulos.join(' | '))
conferir('painel e perfil presentes', abas.titulos.includes('Painel') && abas.titulos.includes('Perfil'))
conferir('planos restaurado', abas.titulos.some((t) => /Planos/.test(t)))
conferir('metas restaurado', abas.titulos.some((t) => /Metas/.test(t)))
conferir('sem erro na tela', !abas.erro, abas.erro ?? '')

// metas
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Metas/.test(b.textContent))?.click()`)
await espera(1800)
const metas = JSON.parse(await avaliar(`JSON.stringify({
  titulo: document.querySelector('.topo h1')?.textContent ?? null,
  cartoes: document.querySelectorAll('.cartao').length,
  campos: document.querySelectorAll('.pagina input').length,
  erro: document.querySelector('.erro')?.textContent ?? null
})`))
conferir('tela de metas carrega', metas.cartoes >= 3 && metas.campos >= 4, JSON.stringify(metas))
await capturar('metas')

// planos
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Planos/.test(b.textContent))?.click()`)
await espera(1500)
const planos = JSON.parse(await avaliar(`JSON.stringify({
  titulo: document.querySelector('.topo h1')?.textContent ?? null,
  selects: document.querySelectorAll('.pagina select').length,
  erro: document.querySelector('.erro')?.textContent ?? null
})`))
conferir('tela de planos carrega', planos.selects >= 2, JSON.stringify(planos))
await capturar('planos')

// análise por foto
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Analisar/.test(b.textContent))?.click()`)
await espera(1200)
const foto = JSON.parse(await avaliar(`JSON.stringify({
  bloco: document.body.textContent.includes('Analisar por foto'),
  inputFile: document.querySelectorAll('input[type=file]').length
})`))
conferir('bloco de foto presente', foto.bloco)
conferir('seletor de arquivo presente', foto.inputFile === 1, `${foto.inputFile}`)
await capturar('analisar')

console.log(`\n================  ${falhas === 0 ? 'TODAS AS VERIFICAÇÕES PASSARAM' : falhas + ' FALHA(S)'}  ================`)
ws.close()
process.exit(falhas === 0 ? 0 : 1)
