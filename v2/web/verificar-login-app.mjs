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
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 950, deviceScaleFactor: 1, mobile: false })
await cdp('Page.navigate', { url: appUrl }); await espera(2500)
await cdp('Page.navigate', { url: appUrl + '/?app=1' }); await espera(4000)

console.log('\n[1] Logo PNG no lugar do "BS"')
const logo = JSON.parse(await avaliar(`JSON.stringify({
  imgs: document.querySelectorAll('img.marca-logo').length,
  carregou: [...document.querySelectorAll('img.marca-logo')].every(i => i.complete && i.naturalWidth > 0),
  textoBS: [...document.querySelectorAll('.marca-simbolo')].length,
  favicon: document.querySelector('link[rel=icon]')?.href ?? null,
  svg: !!document.querySelector('link[rel=icon][type="image/svg+xml"]')
})`))
conferir('logo PNG presente', logo.imgs >= 1, `${logo.imgs}`)
conferir('imagem carregou', logo.carregou)
conferir('sem bloco "BS"', logo.textoBS === 0, `${logo.textoBS}`)
conferir('favicon PNG', (logo.favicon ?? '').endsWith('.png'), logo.favicon ?? '')
conferir('favicon SVG removido', logo.svg === false)

console.log('\n[2] Landing normal (navegador)')
const landing = await avaliar(`Boolean(document.querySelector('.heroi h1'))`)
conferir('landing aparece no navegador', landing)

console.log('\n[3] Sessão persistente')
const s = await (await fetch(`${apiUrl}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'carla@bsfm.dev', senha: 'SenhaForte#2026' }) })).json()
await avaliar(`
  localStorage.setItem('bsfm.token', ${JSON.stringify(s.token)});
  localStorage.setItem('bsfm.refresh', ${JSON.stringify(s.refreshToken)});
  localStorage.setItem('bsfm.chave', ${JSON.stringify(s.chaveAssinatura)});
  localStorage.setItem('bsfm.expira', String(Date.now() + 900000));
  location.reload();
`)
await espera(3200)
const depois = JSON.parse(await avaliar(`JSON.stringify({
  app: Boolean(document.querySelector('.nav-item, aside button')),
  landing: Boolean(document.querySelector('.heroi h1')),
  storage: Object.keys(localStorage).filter(k => k.startsWith('bsfm.')).length,
  sessaoStorage: Object.keys(sessionStorage).filter(k => k.startsWith('bsfm.')).length
})`))
conferir('entra direto no app após recarregar', depois.app && !depois.landing, JSON.stringify(depois))
conferir('credenciais em localStorage', depois.storage >= 3, `${depois.storage}`)
conferir('nada em sessionStorage', depois.sessaoStorage === 0, `${depois.sessaoStorage}`)

// sobrevive a "fechar e abrir": novo documento, mesmo storage
await cdp('Page.navigate', { url: appUrl }); await espera(3000)
const reabriu = await avaliar(`Boolean(document.querySelector('aside button, .nav-item'))`)
conferir('continua logado ao reabrir', reabriu)

console.log('\n[4] App instalado: sem landing')
await cdp('Page.navigate', { url: appUrl + '/?app=1' }); await espera(4000)
await espera(3000)
const app = JSON.parse(await avaliar(`JSON.stringify({
  standalone: true,
  landing: Boolean(document.querySelector('.heroi h1')),
  login: document.querySelector('h1')?.textContent ?? null
})`))
conferir('sem landing no app', app.landing === false)
conferir('vai direto ao login', /entrar/i.test(app.login ?? ''), app.login ?? '')

const c = await cdp('Page.captureScreenshot', { format: 'png' })
fs.writeFileSync('login-app.png', Buffer.from(c.data, 'base64'))

console.log(`\n================  ${falhas === 0 ? 'PASSOU' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
