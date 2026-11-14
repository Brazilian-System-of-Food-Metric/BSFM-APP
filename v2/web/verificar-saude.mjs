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
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 1000, deviceScaleFactor: 1, mobile: false })
await cdp('Page.navigate', { url: appUrl }); await espera(2500)

const s = await (await fetch(`${apiUrl}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'carla@bsfm.dev', senha: 'SenhaForte#2026' }) })).json()
await avaliar(`
  localStorage.setItem('bsfm.token', ${JSON.stringify(s.token)});
  localStorage.setItem('bsfm.refresh', ${JSON.stringify(s.refreshToken)});
  localStorage.setItem('bsfm.chave', ${JSON.stringify(s.chaveAssinatura)});
  location.href='/'; location.reload();
`)
await espera(3500)
await avaliar(`[...document.querySelectorAll('aside button, .nav-item')].find(b => /Perfil/.test(b.textContent))?.click()`)
await espera(2500)

const inicio = JSON.parse(await avaliar(`JSON.stringify({
  secao: Boolean(document.querySelector('#perfil-emergencias')),
  contador: document.querySelector('#perfil-emergencias')?.innerText.match(/([\\d.]+) estabelecimentos/)?.[1] ?? null,
  chips: document.querySelectorAll('.chip').length,
  busca: Boolean(document.querySelector('.campo-busca')),
  itens: document.querySelectorAll('#perfil-emergencias li').length
})`))
conferir('seção de rede de saúde', inicio.secao)
conferir('contador do CNES', Number((inicio.contador ?? '0').replace('.', '')) > 500, inicio.contador ?? '')
conferir('filtros por tipo', inicio.chips >= 5, `${inicio.chips}`)
conferir('campo de busca', inicio.busca)
conferir('lista renderizada', inicio.itens >= 20, `${inicio.itens}`)

// busca real
await avaliar(`
  const campo = document.querySelector('.campo-busca')
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set
  setter.call(campo, 'clinicas')
  campo.dispatchEvent(new Event('input', { bubbles: true }))
`)
await espera(800)
const busca = await avaliar(`document.querySelector('#perfil-emergencias')?.innerText.match(/(\\d+) no filtro/)?.[1] ?? null`)
conferir('busca filtra resultados', Number(busca) > 0 && Number(busca) < 1009, String(busca))

// filtro SUS
await avaliar(`
  const campo = document.querySelector('.campo-busca')
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set
  const campo2 = document.querySelector('.campo-busca')
  const s2 = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set
  s2.call(campo2, ''); campo2.dispatchEvent(new Event('input', { bubbles: true }))
  document.querySelector('.chip-sus input')?.click()
`)
await espera(800)
const sus = await avaliar(`document.querySelector('#perfil-emergencias')?.innerText.match(/(\\d+) no filtro/)?.[1] ?? null`)
conferir('filtro "só SUS" funciona', Number(sus) > 20, String(sus))

const c = await cdp('Page.captureScreenshot', { format: 'png' })
fs.writeFileSync('saude-rede.png', Buffer.from(c.data, 'base64'))

console.log(`\n================  ${falhas === 0 ? 'PASSOU' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
