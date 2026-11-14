import fs from 'node:fs'
const [apiUrl = 'http://127.0.0.1:5100', appUrl = 'http://localhost:4173'] = process.argv.slice(2)
const espera = (ms) => new Promise((r) => setTimeout(r, ms))
const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
const alvo = alvos.filter((t) => t.type === 'page').find((t) => t.url.startsWith(appUrl)) ?? alvos.find((t) => t.type === 'page')
const ws = new WebSocket(alvo.webSocketDebuggerUrl)
await new Promise((r, j) => { ws.addEventListener('open', r); ws.addEventListener('error', j) })
let id = 1; const pend = new Map()
ws.addEventListener('message', (e) => { const m = JSON.parse(e.data); if (m.id && pend.has(m.id)) { const { ok, erro } = pend.get(m.id); pend.delete(m.id); m.error ? erro(new Error(m.error.message)) : ok(m.result) } })
const cdp = (metodo, params = {}) => new Promise((ok, erro) => { const i = id++; pend.set(i, { ok, erro }); ws.send(JSON.stringify({ id: i, method: metodo, params })) })
const avaliar = async (e) => { const r = await cdp('Runtime.evaluate', { expression: e, returnByValue: true, awaitPromise: true }); return r.result.value }
await cdp('Page.enable'); await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false })
await cdp('Page.navigate', { url: appUrl }); await espera(2000)
const sessao = await (await fetch(`${apiUrl}/api/auth/login`, {
  method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'carla@bsfm.dev', senha: 'SenhaForte#2026' }),
})).json()
await avaliar(`
  sessionStorage.setItem('bsfm.token', ${JSON.stringify(sessao.token)});
  sessionStorage.setItem('bsfm.refresh', ${JSON.stringify(sessao.refreshToken)});
  sessionStorage.setItem('bsfm.chave', ${JSON.stringify(sessao.chaveAssinatura)});
  location.href='/'; location.reload();
`)
await espera(3000)
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Metas/.test(b.textContent))?.click()`)
await espera(2500)
const r = JSON.parse(await avaliar(`JSON.stringify({
  titulo: document.querySelector('.topo h1')?.textContent,
  svg: document.querySelectorAll('.grafico svg').length,
  ponto: document.querySelectorAll('.grafico-ponto').length,
  cartoes: document.querySelectorAll('.cartao').length,
  erro: document.querySelector('.erro')?.textContent ?? null
})`))
console.log(JSON.stringify(r))
const c = await cdp('Page.captureScreenshot', { format: 'png' })
fs.writeFileSync('metas-graficos.png', Buffer.from(c.data, 'base64'))
const ok = r.erro === null && r.svg + r.ponto >= 2
console.log(ok ? 'PASS: dois graficos de metas' : 'FAIL')
ws.close(); process.exit(ok ? 0 : 1)
