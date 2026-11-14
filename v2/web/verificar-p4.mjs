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
await cdp('Page.navigate', { url: appUrl }); await espera(2200)
await avaliar(`sessionStorage.clear(); location.reload()`); await espera(2500)

console.log('\n[1] Landing: tabela de status')
const landing = JSON.parse(await avaliar(`JSON.stringify({
  tabela: document.querySelectorAll('.landing table.tabela tr').length,
  status: document.body.textContent.includes('em desenvolvimento')
})`))
conferir('tabela de status presente', landing.tabela >= 7, `${landing.tabela}`)
conferir('estado em desenvolvimento declarado', landing.status)
const c1 = await cdp('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync('p4-landing.png', Buffer.from(c1.data, 'base64'))

console.log('\n[2] Camera e salvar no plano')
const s = await (await fetch(`${apiUrl}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'carla@bsfm.dev', senha: 'SenhaForte#2026' }) })).json()
await avaliar(`
  sessionStorage.setItem('bsfm.token', ${JSON.stringify(s.token)});
  sessionStorage.setItem('bsfm.refresh', ${JSON.stringify(s.refreshToken)});
  sessionStorage.setItem('bsfm.chave', ${JSON.stringify(s.chaveAssinatura)});
  location.href='/'; location.reload();
`)
await espera(3200)
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Analisar/.test(b.textContent))?.click()`)
await espera(1000)
await avaliar(`[...document.querySelectorAll('.segmento')].find(b => /Foto/.test(b.textContent))?.click()`)
await espera(600)
const foto = JSON.parse(await avaliar(`JSON.stringify({
  botaoCamera: [...document.querySelectorAll('button')].some(b => /Abrir câmera/.test(b.textContent)),
  erro: document.querySelector('.erro')?.textContent ?? null
})`))
conferir('botão de câmera presente', foto.botaoCamera)
conferir('sem erro', !foto.erro, foto.erro ?? '')

await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Diário/.test(b.textContent))?.click()`)
await espera(1500)
const diario = JSON.parse(await avaliar(`JSON.stringify({
  botoesPlano: [...document.querySelectorAll('button')].filter(b => /Plano/.test(b.textContent)).length,
  vazio: document.body.textContent.includes('Nada registrado')
})`))
conferir('diário abre', diario.vazio || diario.botoesPlano > 0, JSON.stringify(diario))

console.log(`\n================  ${falhas === 0 ? 'PASSOU' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
