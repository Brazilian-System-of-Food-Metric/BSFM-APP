// Verifica os itens P0 aplicados: recuperação de senha, abas de análise,
// análise de rótulo e hidratação com gráfico.
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

let id = 1
const pend = new Map()
ws.addEventListener('message', (e) => {
  const m = JSON.parse(e.data)
  if (m.id && pend.has(m.id)) { const { ok, erro } = pend.get(m.id); pend.delete(m.id); m.error ? erro(new Error(m.error.message)) : ok(m.result) }
})
const cdp = (metodo, params = {}) => new Promise((ok, erro) => { const i = id++; pend.set(i, { ok, erro }); ws.send(JSON.stringify({ id: i, method: metodo, params })) })
const avaliar = async (expr) => {
  const r = await cdp('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true })
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.text ?? 'erro')
  return r.result.value
}
const capturar = async (nome) => {
  const c = await cdp('Page.captureScreenshot', { format: 'png' })
  fs.writeFileSync(`p0-${nome}.png`, Buffer.from(c.data, 'base64'))
}

await cdp('Page.enable'); await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false })
await cdp('Page.navigate', { url: appUrl }); await espera(2200)
await avaliar(`sessionStorage.clear(); localStorage.clear(); location.reload()`); await espera(2500)

console.log('\n[1] Recuperação de senha')
await avaliar(`[...document.querySelectorAll('button')].find(b => /entrar/i.test(b.textContent))?.click()`)
await espera(800)
const temLink = await avaliar(`[...document.querySelectorAll('button')].some(b => /esqueci minha senha/i.test(b.textContent))`)
conferir('link "Esqueci minha senha" no login', temLink)
await avaliar(`[...document.querySelectorAll('button')].find(b => /esqueci minha senha/i.test(b.textContent))?.click()`)
await espera(700)
const rec = JSON.parse(await avaliar(`JSON.stringify({
  h1: document.querySelector('h1')?.textContent ?? null,
  campoEmail: !!document.querySelector('#rec-email'),
  botao: document.body.textContent.includes('Enviar código')
})`))
conferir('tela de recuperação abre', rec.h1 === 'Esqueci minha senha', rec.h1 ?? '')
conferir('campo de e-mail presente', rec.campoEmail)
conferir('botão enviar código', rec.botao)
await capturar('recuperar')

console.log('\n[2] Abas de análise')
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
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Analisar/.test(b.textContent))?.click()`)
await espera(1000)
const abas = JSON.parse(await avaliar(`JSON.stringify([...document.querySelectorAll('.segmento')].map(b => b.textContent.trim()))`))
console.log('  abas:', abas.join(' | '))
conferir('três modos de análise', abas.length === 3, `${abas.length}`)
conferir('modo Rótulo presente', abas.some((a) => /Rótulo/.test(a)))

await avaliar(`[...document.querySelectorAll('.segmento')].find(b => /Rótulo/.test(b.textContent))?.click()`)
await espera(700)
const rotulo = JSON.parse(await avaliar(`JSON.stringify({
  texto: document.body.textContent.includes('OCR local'),
  inputFile: document.querySelectorAll('input[type=file]').length
})`))
conferir('aba rótulo mostra upload', rotulo.inputFile === 1, `${rotulo.inputFile}`)
conferir('explica que OCR é local', rotulo.texto)
await capturar('rotulo')

console.log('\n[3] Hidratação com valor livre e gráfico')
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Painel/.test(b.textContent))?.click()`)
await espera(2500)
const agua = JSON.parse(await avaliar(`JSON.stringify({
  campo: !!document.querySelector('.campo-agua input'),
  barras: document.querySelectorAll('.barra-dia').length,
  grafico: document.querySelectorAll('.grafico svg').length
})`))
conferir('campo de valor livre', agua.campo)
conferir('gráfico semanal de água', agua.barras === 7, `${agua.barras}`)
conferir('gráfico de evolução', agua.grafico >= 1, `${agua.grafico}`)
await capturar('painel')

console.log('\n[4] Endpoint do rótulo')
const r = await fetch(`${apiUrl}/api/nutricao/rotulo`, {
  method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ texto: 'Porcao de 30 g. Valor energetico 120 kcal. Carboidratos 18 g. Proteinas 3 g. Gorduras totais 4 g.' }),
})
const corpo = await r.json()
console.log('  resposta:', JSON.stringify(corpo.por100g), 'conf=', corpo.confianca)
conferir('endpoint interpreta o rótulo', r.status === 200 && Math.round(corpo.por100g.kcal) === 400, `${r.status}`)

console.log(`\n================  ${falhas === 0 ? 'TODAS PASSARAM' : falhas + ' FALHA(S)'}  ================`)
ws.close()
process.exit(falhas ? 1 : 0)
