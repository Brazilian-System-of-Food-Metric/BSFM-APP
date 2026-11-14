import fs from 'node:fs'
const [apiUrl = 'http://127.0.0.1:5100', appUrl = 'http://localhost:4173'] = process.argv.slice(2)
const espera = (ms) => new Promise((r) => setTimeout(r, ms))
let falhas = 0
const conferir = (n, ok, d = '') => { console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${n}${ok || !d ? '' : ` -> ${d}`}`); if (!ok) falhas++ }

const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
const alvo = alvos.find((t) => t.type === 'page')
const ws = new WebSocket(alvo.webSocketDebuggerUrl)
await new Promise((r, j) => { ws.addEventListener('open', r); ws.addEventListener('error', j) })
let id = 1; const pend = new Map()
ws.addEventListener('message', (e) => { const m = JSON.parse(e.data); if (m.id && pend.has(m.id)) { const { ok, erro } = pend.get(m.id); pend.delete(m.id); m.error ? erro(new Error(m.error.message)) : ok(m.result) } })
const cdp = (m, p = {}) => new Promise((ok, erro) => { const i = id++; pend.set(i, { ok, erro }); ws.send(JSON.stringify({ id: i, method: m, params: p })) })
const avaliar = async (e) => { const r = await cdp('Runtime.evaluate', { expression: e, returnByValue: true, awaitPromise: true }); if (r.exceptionDetails) throw new Error(r.exceptionDetails.text); return r.result.value }

await cdp('Page.enable'); await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 950, deviceScaleFactor: 1, mobile: false })
await cdp('Page.navigate', { url: appUrl }); await espera(2500)

const s = await (await fetch(`${apiUrl}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'carla@bsfm.dev', senha: 'SenhaForte#2026' }) })).json()
await avaliar(`
  sessionStorage.setItem('bsfm.token', ${JSON.stringify(s.token)});
  sessionStorage.setItem('bsfm.refresh', ${JSON.stringify(s.refreshToken)});
  sessionStorage.setItem('bsfm.chave', ${JSON.stringify(s.chaveAssinatura)});
  location.href='/'; location.reload();
`)
await espera(3500)

const telas = ['Painel', 'Analisar', 'Diário', 'Planos', 'Metas', 'Perfil']
for (const tela of telas) {
  await avaliar(`[...document.querySelectorAll('.nav-item, .nav-link, nav button, aside button')].find(b => b.textContent.trim().startsWith(${JSON.stringify(tela)}))?.click()`)
  await espera(1400)
  const r = JSON.parse(await avaliar(`JSON.stringify({
    titulo: document.querySelector('h1')?.textContent ?? null,
    cartoes: document.querySelectorAll('.cartao, .card').length,
    erro: document.querySelector('.erro')?.textContent ?? null,
    vazio: document.body.innerText.length
  })`))
  console.log(`\n[${tela}] titulo="${r.titulo}" cartoes=${r.cartoes}`)
  conferir('renderizou sem erro', !r.erro && r.vazio > 100, r.erro ?? '')
  const c = await cdp('Page.captureScreenshot', { format: 'png' })
  fs.writeFileSync(`ref-${tela.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')}.png`, Buffer.from(c.data, 'base64'))
}

console.log(`\n================  ${falhas === 0 ? 'TODAS AS TELAS OK' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
