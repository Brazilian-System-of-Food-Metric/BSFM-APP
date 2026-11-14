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
const s = await (await fetch(`${apiUrl}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'ana@bsfm.dev', senha: 'SenhaForte#2026' }) })).json()
if (!s.token) { console.log('login falhou (ana pode estar bloqueada) — usando carla'); }
const sessao = s.token ? s : await (await fetch(`${apiUrl}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'carla@bsfm.dev', senha: 'SenhaForte#2026' }) })).json()
await avaliar(`
  sessionStorage.setItem('bsfm.token', ${JSON.stringify(sessao.token)});
  sessionStorage.setItem('bsfm.refresh', ${JSON.stringify(sessao.refreshToken)});
  sessionStorage.setItem('bsfm.chave', ${JSON.stringify(sessao.chaveAssinatura)});
  location.href='/'; location.reload();
`)
await espera(3200)

console.log('\n[1] Criar refeição pela interface')
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Planos/.test(b.textContent))?.click()`)
await espera(1800)
const antes = await avaliar(`document.querySelectorAll('.pagina input[type=checkbox]').length`)
await avaliar(`
  (() => {
    const form = [...document.querySelectorAll('form')].find(f => f.querySelector('#prato'))
    if (!form) return 'sem form'
    const set = (sel, v) => {
      const el = form.querySelector(sel)
      const proto = el.tagName === 'SELECT' ? window.HTMLSelectElement.prototype : window.HTMLInputElement.prototype
      Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, v)
      el.dispatchEvent(new Event('input', { bubbles: true }))
      el.dispatchEvent(new Event('change', { bubbles: true }))
    }
    set('#prato', 'Frango com arroz')
    set('#kcalPrato', '520')
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    return 'ok'
  })()
`)
await espera(2500)
const depois = await avaliar(`document.querySelectorAll('.pagina input[type=checkbox]').length`)
conferir('refeição criada pela interface', depois > antes, `${antes} -> ${depois}`)

console.log('\n[2] Concluir refeição')
if (depois > 0) {
  const antesMarcado = await avaliar(`document.querySelector('.pagina input[type=checkbox]')?.checked ?? null`)
  await avaliar(`document.querySelector('.pagina input[type=checkbox]').click()`)
  await espera(2200)
  const marcado = await avaliar(`document.querySelector('.pagina input[type=checkbox]')?.checked ?? null`)
  const contador = await avaliar(`document.body.textContent.match(/(\\d+) cumpridas/)?.[1] ?? null`)
  conferir('checkbox alterna (estado muda)', marcado !== antesMarcado, ` -> ` + String(marcado))
  conferir('contador acompanha o estado', Number(contador) === (marcado ? 1 : 0), `contador=` + contador + ` marcado=` + marcado)
} else {
  conferir('checkbox alterna (estado muda)', marcado !== antesMarcado, ` -> ` + String(marcado))
}

console.log('\n[3] Endpoints públicos novos')
const fb = await fetch(`${apiUrl}/api/nutricao/feedback`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ kcal: 500, proteina: 30, carboidrato: 50, gordura: 12, fibra: 6, metaKcal: 2000, consumidoKcal: 800 }) })
conferir('feedback 200', fb.status === 200, String(fb.status))

const pr = await fetch(`${apiUrl}/api/nutricao/prescricao`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ texto: 'Cafe da manha\n- pao\nAlmoco\n- arroz' }) })
conferir('prescricao 200', pr.status === 200, String(pr.status))

const rt = await fetch(`${apiUrl}/api/nutricao/rotulo`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ texto: 'Porcao 30 g. Energia 120 kcal. Proteinas 3 g. Carboidratos 18 g. Gorduras 4 g.' }) })
conferir('rotulo 200', rt.status === 200, String(rt.status))

console.log(`\n================  ${falhas === 0 ? 'PASSOU' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
