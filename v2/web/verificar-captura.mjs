import fs from 'node:fs'
const [apiUrl = 'http://127.0.0.1:5100', appUrl = 'http://localhost:4173'] = process.argv.slice(2)
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
await cdp('Page.navigate', { url: appUrl }); await espera(2000)
const s = await (await fetch(`${apiUrl}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'carla@bsfm.dev', senha: 'SenhaForte#2026' }) })).json()
await avaliar(`
  sessionStorage.setItem('bsfm.token', ${JSON.stringify(s.token)});
  sessionStorage.setItem('bsfm.refresh', ${JSON.stringify(s.refreshToken)});
  sessionStorage.setItem('bsfm.chave', ${JSON.stringify(s.chaveAssinatura)});
  location.href='/'; location.reload();
`)
await espera(3200)

await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Analisar/.test(b.textContent))?.click()`)
await espera(1200)

for (const aba of ['Foto', 'Rótulo']) {
  await avaliar(`[...document.querySelectorAll('.segmento')].find(b => b.textContent.includes(${JSON.stringify(aba)}))?.click()`)
  await espera(700)
  const r = JSON.parse(await avaliar(`JSON.stringify({
    zona: document.querySelectorAll('.captura-zona').length,
    icone: document.querySelectorAll('.captura-icone svg').length,
    camera: [...document.querySelectorAll('.captura-acoes button')].some(b => /Abrir câmera/.test(b.textContent)),
    arquivo: [...document.querySelectorAll('.captura-acoes button')].some(b => /Escolher/.test(b.textContent)),
    inputOculto: document.querySelectorAll('.captura input[type=file]').length,
    borda: getComputedStyle(document.querySelector('.captura-zona')).borderStyle,
    raio: getComputedStyle(document.querySelector('.captura-zona')).borderRadius
  })`))
  console.log(`\n[${aba}]`)
  conferir('zona de captura', r.zona === 1, `${r.zona}`)
  conferir('ícone da câmera', r.icone === 1, `${r.icone}`)
  conferir('botão "Abrir câmera"', r.camera)
  conferir('botão de arquivo', r.arquivo)
  conferir('input de arquivo oculto', r.inputOculto === 1, `${r.inputOculto}`)
  conferir('zona tracejada e arredondada', r.borda === 'dashed' && parseInt(r.raio) >= 14, `${r.borda} ${r.raio}`)
  const c = await cdp('Page.captureScreenshot', { format: 'png' })
  fs.writeFileSync(`p7-captura-${aba.toLowerCase().replace('ó','o')}.png`, Buffer.from(c.data, 'base64'))
}

console.log(`\n================  ${falhas === 0 ? 'PASSOU' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
