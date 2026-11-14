const [appUrl = 'http://localhost:4173'] = process.argv.slice(2)
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

async function cenario(url, limpar) {
  await cdp('Page.navigate', { url })
  await espera(1500)
  if (limpar) await avaliar(`localStorage.clear(); sessionStorage.clear()`)
  await cdp('Page.navigate', { url })
  await espera(3000)
  return JSON.parse(await avaliar(`JSON.stringify({
    h1: document.querySelector('h1')?.textContent ?? null,
    landing: Boolean(document.querySelector('.heroi h1')),
    app: Boolean(document.querySelector('.nav-item, aside nav button')),
    logo: document.querySelectorAll('img.marca-logo').length,
    logoOk: [...document.querySelectorAll('img.marca-logo')].every(i => i.complete && i.naturalWidth > 0)
  })`))
}

console.log('\n[1] Navegador, sem sessão -> landing')
const a = await cenario(appUrl + '/', true)
conferir('landing visível', a.landing, JSON.stringify(a).slice(0, 120))
conferir('logo PNG carregou', a.logo >= 1 && a.logoOk, `${a.logo}`)

console.log('\n[2] Modo aplicativo (?app=1), sem sessão -> login')
const b = await cenario(appUrl + '/?app=1', true)
conferir('sem landing', b.landing === false)
conferir('login exibido', /entrar/i.test(b.h1 ?? ''), b.h1 ?? '')
conferir('logo PNG carregou', b.logo >= 1 && b.logoOk, `${b.logo}`)

console.log(`\n================  ${falhas === 0 ? 'PASSOU' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
