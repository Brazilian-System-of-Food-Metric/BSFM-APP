import fs from 'node:fs'
const [appUrl = 'http://localhost:4173'] = process.argv.slice(2)
const espera = (ms) => new Promise((r) => setTimeout(r, ms))
let falhas = 0
const conferir = (n, ok, d = '') => { console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${n}${ok || !d ? '' : ` -> ${d}`}`); if (!ok) falhas++ }

const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
const alvo = alvos.filter((t) => t.type === 'page').find((t) => t.url.startsWith(appUrl)) ?? alvos.find((t) => t.type === 'page')
const ws = new WebSocket(alvo.webSocketDebuggerUrl)
await new Promise((r, j) => { ws.addEventListener('open', r); ws.addEventListener('error', j) })
let id = 1; const pend = new Map()
ws.addEventListener('message', (e) => { const m = JSON.parse(e.data); if (m.id && pend.has(m.id)) { const { ok, erro } = pend.get(m.id); pend.delete(m.id); m.error ? erro(new Error(m.error.message)) : ok(m.result) } })
const cdp = (m, p = {}) => new Promise((ok, erro) => { const i = id++; pend.set(i, { ok, erro }); ws.send(JSON.stringify({ id: i, method: m, params: p })) })
const avaliar = async (e) => { const r = await cdp('Runtime.evaluate', { expression: e, returnByValue: true, awaitPromise: true }); if (r.exceptionDetails) throw new Error(r.exceptionDetails.text); return r.result.value }

await cdp('Page.enable'); await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 1000, deviceScaleFactor: 1, mobile: false })
await cdp('Page.navigate', { url: appUrl }); await espera(2500)
await avaliar(`sessionStorage.clear(); location.reload()`); await espera(2500)

const r = JSON.parse(await avaliar(`JSON.stringify({
  secao: !!document.querySelector('#telas'),
  link: [...document.querySelectorAll('.landing-nav a')].some(a => a.getAttribute('href') === '#telas'),
  capturas: document.querySelectorAll('.tela-demo img').length,
  quebradas: [...document.querySelectorAll('.tela-demo img')].filter(i => i.complete && i.naturalWidth === 0).length,
  legenda: document.body.textContent.includes('Capturas reais do aplicativo em produção')
})`))
conferir('seção "Veja o app" presente', r.secao)
conferir('link Telas no menu', r.link)
conferir('4 capturas reais', r.capturas === 4, `${r.capturas}`)
conferir('nenhuma imagem quebrada', r.quebradas === 0, `${r.quebradas}`)
conferir('declara que são telas reais', r.legenda)

await avaliar(`document.querySelector('#telas')?.scrollIntoView({ block: 'start' })`)
await espera(700)
const c = await cdp('Page.captureScreenshot', { format: 'png' })
fs.writeFileSync('p6-galeria.png', Buffer.from(c.data, 'base64'))

console.log(`\n================  ${falhas === 0 ? 'PASSOU' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
