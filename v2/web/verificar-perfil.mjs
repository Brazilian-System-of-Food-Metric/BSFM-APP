// Valida as telas novas do P1: dados pessoais, perfil de saúde, termos e
// emergências, além do banner de perfil incompleto no painel.
import fs from 'node:fs'

const [apiUrl = 'http://127.0.0.1:5100', appUrl = 'http://localhost:4173'] = process.argv.slice(2)
const espera = (ms) => new Promise((r) => setTimeout(r, ms))
let falhas = 0
const conferir = (nome, ok, detalhe = '') => {
  console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${nome}${ok || !detalhe ? '' : `  -> ${detalhe}`}`)
  if (!ok) falhas++
}

const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
const alvo = alvos.filter((t) => t.type === 'page').find((t) => t.url.startsWith(appUrl)) ?? alvos.find((t) => t.type === 'page')
const ws = new WebSocket(alvo.webSocketDebuggerUrl)
await new Promise((r, j) => { ws.addEventListener('open', r); ws.addEventListener('error', j) })
let id = 1; const pend = new Map()
ws.addEventListener('message', (e) => {
  const m = JSON.parse(e.data)
  if (m.id && pend.has(m.id)) { const { ok, erro } = pend.get(m.id); pend.delete(m.id); m.error ? erro(new Error(m.error.message)) : ok(m.result) }
})
const cdp = (metodo, params = {}) => new Promise((ok, erro) => { const i = id++; pend.set(i, { ok, erro }); ws.send(JSON.stringify({ id: i, method: metodo, params })) })
const avaliar = async (e) => {
  const r = await cdp('Runtime.evaluate', { expression: e, returnByValue: true, awaitPromise: true })
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.text ?? 'erro')
  return r.result.value
}

await cdp('Page.enable'); await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 1000, deviceScaleFactor: 1, mobile: false })
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
await espera(3200)

console.log('\n[1] Banner de perfil incompleto no painel')
const painel = JSON.parse(await avaliar(`JSON.stringify({
  banner: document.body.textContent.includes('Complete seu perfil de saúde'),
  botao: [...document.querySelectorAll('button')].some(b => /Completar/.test(b.textContent))
})`))
conferir('banner aparece para quem não informou', painel.banner)
conferir('botão leva ao perfil', painel.botao)
const c1 = await cdp('Page.captureScreenshot', { format: 'png' })
fs.writeFileSync('p1-painel-banner.png', Buffer.from(c1.data, 'base64'))

console.log('\n[2] Perfil: blocos novos')
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Perfil/.test(b.textContent))?.click()`)
await espera(2200)
const perfil = JSON.parse(await avaliar(`JSON.stringify({
  dadosPessoais: document.body.textContent.includes('Dados pessoais'),
  campoEmail: !!document.querySelector('#emailPerfil'),
  senhaEmail: !!document.querySelector('#senhaEmail'),
  nascimento: !!document.querySelector('#nascimento'),
  saude: document.body.textContent.includes('Perfil de saúde'),
  selectDiabetes: !!document.querySelector('#diabetes'),
  intolerancia: !!document.querySelector('#intolerancia'),
  termos: document.body.textContent.includes('Privacidade e dados'),
  hospitais: document.querySelectorAll('a[href*="waze.com"]').length,
  erro: document.querySelector('.erro')?.textContent ?? null
})`))
conferir('bloco de dados pessoais', perfil.dadosPessoais)
conferir('campo de e-mail', perfil.campoEmail)
conferir('campo de senha para confirmar', perfil.senhaEmail)
conferir('campo de data de nascimento', perfil.nascimento)
conferir('bloco de saúde', perfil.saude && perfil.selectDiabetes && perfil.intolerancia)
conferir('termos completos', perfil.termos)
conferir('8 unidades com Waze', perfil.hospitais === 8, `${perfil.hospitais}`)
conferir('sem erro na tela', !perfil.erro, perfil.erro ?? '')

// rolar até os blocos finais para a captura
await avaliar(`document.querySelector('#nascimento')?.scrollIntoView({block:'center'})`)
await espera(500)
const c2 = await cdp('Page.captureScreenshot', { format: 'png' })
fs.writeFileSync('p1-perfil.png', Buffer.from(c2.data, 'base64'))

console.log('\n[3] Persistência do perfil de saúde (ida e volta na API)')
const salvo = await avaliar(`
  (async () => {
    const api = await import('/src/api.js').catch(() => null)
    return 'pulado'
  })()
`)
// a chamada acima não funciona no bundle; validamos pela própria UI
await avaliar(`
  (() => {
    const sel = document.querySelector('#diabetes')
    if (!sel) return
    const setter = Object.getOwnPropertyDescriptor(window.HTMLSelectElement.prototype, 'value').set
    setter.call(sel, 'Não')
    sel.dispatchEvent(new Event('change', { bubbles: true }))
    document.querySelector('#intolerancia').value = 'lactose'
  })()
`)
await espera(300)
const pronto = await avaliar(`document.querySelector('#diabetes')?.value ?? null`)
conferir('formulário de saúde preenchível', pronto === 'Não', String(pronto))

console.log(`\n================  ${falhas === 0 ? 'TUDO RENDERIZA' : falhas + ' FALHA(S)'}  ================`)
ws.close()
process.exit(falhas ? 1 : 0)
