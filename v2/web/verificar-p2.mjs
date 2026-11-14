import fs from 'node:fs'
const [apiUrl = 'http://127.0.0.1:5100', appUrl = 'http://localhost:4173'] = process.argv.slice(2)
const espera = (ms) => new Promise((r) => setTimeout(r, ms))
let falhas = 0
const conferir = (n, ok, d = '') => { console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${n}${ok || !d ? '' : `  -> ${d}`}`); if (!ok) falhas++ }

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
await espera(3000)

console.log('\n[1] Planos: importar prescricao')
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Planos/.test(b.textContent))?.click()`)
await espera(1500)
const planos = JSON.parse(await avaliar(`JSON.stringify({
  bloco: document.body.textContent.includes('Importar prescrição'),
  input: [...document.querySelectorAll('input[type=file]')].length,
  erro: document.querySelector('.erro')?.textContent ?? null
})`))
conferir('bloco de importacao presente', planos.bloco)
conferir('seletor de arquivo', planos.input >= 1, `${planos.input}`)
conferir('sem erro', !planos.erro, planos.erro ?? '')
const c1 = await cdp('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync('p2-planos.png', Buffer.from(c1.data, 'base64'))

console.log('\n[2] Diario: feedback por item')
await avaliar(`[...document.querySelectorAll('.nav-item')].find(b => /Diário/.test(b.textContent))?.click()`)
await espera(2000)
const diario = JSON.parse(await avaliar(`JSON.stringify({
  vazio: document.body.textContent.includes('Nada registrado ainda'),
  itens: document.querySelectorAll('.item').length,
  botoes: [...document.querySelectorAll('button')].filter(b => /Avaliar|Detalhes/.test(b.textContent)).length
})`))
conferir('tela do diario abre', diario.vazio || diario.itens > 0 || diario.botoes > 0, JSON.stringify(diario))

// registra um alimento pela API para o feedback ter o que avaliar não é possível (HMAC), então só confere o endpoint
console.log('\n[3] Endpoints')
const fb = await fetch(`${apiUrl}/api/nutricao/feedback`, {
  method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ kcal: 700, proteina: 20, carboidrato: 30, gordura: 40, fibra: 1, metaKcal: 2000, consumidoKcal: 500, diabetes: 'Tipo 2', intolerancia: 'lactose' }),
})
const fbCorpo = await fb.json()
console.log('  feedback:', fbCorpo.classificacao, fbCorpo.pontuacao, '|', fbCorpo.dicas?.[0])
conferir('feedback responde e classifica', fb.status === 200 && typeof fbCorpo.pontuacao === 'number')

const pr = await fetch(`${apiUrl}/api/nutricao/prescricao`, {
  method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ texto: 'Café da manhã\n- pão integral\n- ovo mexido\nAlmoço\n- arroz\n- feijão 120 kcal\nJantar\n- sopa de legumes' }),
})
const prCorpo = await pr.json()
console.log('  prescricao:', JSON.stringify(prCorpo.refeicoes?.map((r) => `${r.nome}:${r.itens.length}`)))
conferir('prescricao separa refeicoes', pr.status === 200 && prCorpo.refeicoes?.length === 3, `${prCorpo.refeicoes?.length}`)

console.log(`\n================  ${falhas === 0 ? 'TUDO PASSOU' : falhas + ' FALHA(S)'}  ================`)
ws.close(); process.exit(falhas ? 1 : 0)
