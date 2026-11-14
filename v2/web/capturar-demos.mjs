// Gera as capturas usadas na landing (/public/demo/*.png) a partir do app real.
//
// Uso: node capturar-demos.mjs [url-app] [url-cdp]
// Requer Chrome com --remote-debugging-port=9222 e os servidores no ar.

import fs from 'node:fs'

const appUrl = process.argv[2] ?? 'http://localhost:4173'
const cdpUrl = process.argv[3] ?? 'http://127.0.0.1:9222'
const esperar = (ms) => new Promise((r) => setTimeout(r, ms))

const alvos = await (await fetch(`${cdpUrl}/json/list`)).json()
const alvo = alvos.find((t) => t.type === 'page' && t.url.startsWith(appUrl)) ?? alvos.find((t) => t.type === 'page')
if (!alvo) throw new Error('Nenhuma página disponível no CDP')

const ws = new WebSocket(alvo.webSocketDebuggerUrl)
await new Promise((ok, erro) => {
  ws.addEventListener('open', ok)
  ws.addEventListener('error', erro)
})

let id = 1
const pendentes = new Map()
ws.addEventListener('message', (evento) => {
  const m = JSON.parse(evento.data)
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
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.text ?? 'erro de avaliação')
  return r.result.value
}

async function digitar(seletor, texto) {
  const ok = await avaliar(`(() => {
    const el = document.querySelector(${JSON.stringify(seletor)})
    if (!el) return false
    el.focus()
    if (el.select) el.select()
    return true
  })()`)
  if (!ok) return false
  return cdp('Input.insertText', { text: texto })
}

async function capturar(nome) {
  const c = await cdp('Page.captureScreenshot', { format: 'png', fromSurface: true })
  fs.writeFileSync(`public/demo/${nome}.png`, Buffer.from(c.data, 'base64'))
  console.log(`  salvo public/demo/${nome}.png`)
}

async function clicar(texto, seletor) {
  return avaliar(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(seletor)})].find((b) => (b.textContent || '').trim() === ${JSON.stringify(texto)})
    if (!el) return false
    el.click()
    return true
  })()`)
}

await cdp('Page.enable')
await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1120, height: 740, deviceScaleFactor: 1, mobile: false })

await cdp('Page.navigate', { url: appUrl })
await esperar(1800)
await avaliar('sessionStorage.clear(); localStorage.setItem("bsfm.tema", "claro");')
await cdp('Page.navigate', { url: appUrl })
await esperar(1800)

// Esconde o botão flutuante do VLibras para a captura ficar limpa.
await avaliar(`(() => { const v = document.querySelector('div[vw]'); if (v) v.style.display = 'none'; return true })()`)

if (!(await clicar('Entrar', 'button'))) throw new Error('Botão Entrar não encontrado')
await esperar(700)
await digitar('#email', 'ana@bsfm.dev')
await digitar('#senha', 'SenhaForte#2026')
await avaliar('document.querySelector("form").requestSubmit()')
await esperar(2600)

if ((await avaliar('document.body.innerText')).includes('Entrar na sua conta')) {
  throw new Error('Login não concluiu — confira a senha do seed')
}

await avaliar(`(() => { const v = document.querySelector('div[vw]'); if (v) v.style.display = 'none'; return true })()`)

console.log('Capturando telas…')
await capturar('painel')

await clicar('Analisar', '.nav-item')
await esperar(900)
await clicar('Rótulo', '.segmento')
await esperar(900)
await capturar('rotulo')

await clicar('Metas', '.nav-item')
await esperar(1200)
await capturar('metas')

await clicar('Perfil', '.nav-item')
await esperar(1200)
await capturar('perfil')

console.log('Concluído. Rode "npm run build" para publicar as capturas em dist/.')
ws.close()
process.exit(0)
