// Verificação visual do redesign em desktop e celular, mais checagem do PWA.
//
// Uso: node verificar-layout.mjs <url-api> <url-app> <prefixo>

import fs from 'node:fs'

const [apiUrl = 'http://127.0.0.1:5100', appUrl = 'http://localhost:4173', prefixo = 'layout'] =
  process.argv.slice(2)

const email = process.env.BSFM_EMAIL ?? 'ana@bsfm.dev'
const senha = process.env.BSFM_SENHA ?? 'SenhaForte#2026'
const espera = (ms) => new Promise((r) => setTimeout(r, ms))

const resposta = await fetch(`${apiUrl}/api/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, senha }),
})
if (!resposta.ok) throw new Error(`login falhou: ${resposta.status}`)
const sessao = await resposta.json()

const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
const paginas = alvos.filter((t) => t.type === 'page')
const alvo = paginas.find((t) => t.url.startsWith(appUrl)) ?? paginas[0]
if (!alvo) throw new Error('nenhuma aba no Chrome')

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

async function viewport(width, height, mobile) {
  await cdp('Emulation.setDeviceMetricsOverride', {
    width, height, deviceScaleFactor: mobile ? 2 : 1, mobile, screenWidth: width, screenHeight: height,
  })
}

async function capturar(nome) {
  const c = await cdp('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false })
  const arquivo = `${prefixo}-${nome}.png`
  fs.writeFileSync(arquivo, Buffer.from(c.data, 'base64'))
  console.log(`  ${arquivo} (${Math.round(fs.statSync(arquivo).size / 1024)} KB)`)
}

await cdp('Page.enable')
await cdp('Runtime.enable')

await cdp('Page.navigate', { url: appUrl })
await espera(2500)
await avaliar(`
  sessionStorage.setItem('bsfm.token', ${JSON.stringify(sessao.token)});
  sessionStorage.setItem('bsfm.refresh', ${JSON.stringify(sessao.refreshToken)});
  sessionStorage.setItem('bsfm.chave', ${JSON.stringify(sessao.chaveAssinatura)});
  location.reload();
`)
await espera(3000)

// ---- desktop -------------------------------------------------------------
await viewport(1280, 800, false)
await avaliar(`document.querySelectorAll('.nav-item')[0].click()`)
await espera(2200)

const desktop = JSON.parse(await avaliar(`JSON.stringify({
  titulo: document.querySelector('.topo h1')?.textContent,
  sidebarVisivel: getComputedStyle(document.querySelector('.sidebar')).display,
  barraMobile: getComputedStyle(document.querySelector('.barra-mobile')).display,
  cartoes: document.querySelectorAll('.cartao').length,
  donut: !!document.querySelector('.donut'),
  tema: document.documentElement.dataset.tema,
  erro: document.querySelector('.erro')?.textContent ?? null
})`))
console.log('desktop:', desktop)
if (desktop.sidebarVisivel === 'none') throw new Error('sidebar não apareceu no desktop')
if (desktop.barraMobile !== 'none') throw new Error('barra mobile apareceu no desktop')
if (!desktop.donut) throw new Error('donut de calorias ausente')
if (desktop.erro) throw new Error(`erro na tela: ${desktop.erro}`)
await capturar('desktop-painel')

// ---- celular -------------------------------------------------------------
await viewport(390, 844, true)
await espera(700)

const celular = JSON.parse(await avaliar(`JSON.stringify({
  sidebarVisivel: getComputedStyle(document.querySelector('.sidebar')).display,
  barraMobile: getComputedStyle(document.querySelector('.barra-mobile')).display,
  largura: window.innerWidth
})`))
console.log('celular:', celular)
if (celular.sidebarVisivel !== 'none') throw new Error('sidebar apareceu no celular')
if (celular.barraMobile === 'none') throw new Error('barra mobile não apareceu')
await capturar('celular-painel')

// analisar no celular
await avaliar(`document.querySelectorAll('.aba')[1].click()`)
await espera(500)
await avaliar(`
  const campo = document.querySelector('.busca input');
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
  setter.call(campo, 'arroz');
  campo.dispatchEvent(new Event('input', { bubbles: true }));
`)
await espera(1600)
const sugestoes = await avaliar(`document.querySelectorAll('.sugestao').length`)
console.log('sugestões no celular:', sugestoes)
if (sugestoes === 0) throw new Error('busca não retornou sugestões')
await capturar('celular-analisar')

// ---- PWA -----------------------------------------------------------------
await avaliar(`document.querySelectorAll('.aba')[3].click()`)
await espera(900)
const pwa = JSON.parse(await avaliar(`JSON.stringify({
  manifest: document.querySelector('link[rel=manifest]')?.href ?? null,
  themeColor: document.querySelector('meta[name=theme-color]')?.content ?? null,
  serviceWorker: !!navigator.serviceWorker,
  controle: !!navigator.serviceWorker?.controller
})`))
console.log('pwa:', pwa)
if (!pwa.manifest) throw new Error('manifest não linkado')
if (!pwa.themeColor) throw new Error('theme-color ausente')
await capturar('celular-perfil')

// confirma que o manifest é servido e válido
const manifesto = await (await fetch(new URL(pwa.manifest, appUrl))).json()
console.log('manifest:', { name: manifesto.name, display: manifesto.display, icones: manifesto.icons.length })
if (manifesto.display !== 'standalone') throw new Error('display do manifest não é standalone')
if (!manifesto.icons?.length) throw new Error('manifest sem ícones')

// service worker servido
const sw = await fetch(new URL('/sw.js', appUrl))
if (!sw.ok) throw new Error(`sw.js respondeu ${sw.status}`)
console.log('sw.js:', sw.status, sw.headers.get('content-type'))

ws.close()
console.log('\nOK: desktop, celular e PWA verificados.')
process.exit(0)
