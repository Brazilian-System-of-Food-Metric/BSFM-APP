// Captura a tela de cadastro (sem login): abre a aba, clica em "Criar conta" e
// salva o PNG. Serve para conferir o visual e a presença dos campos.
import fs from 'node:fs'

const [appUrl = 'http://localhost:5101', saida = 'tela-cadastro.png'] = process.argv.slice(2)
const espera = (ms) => new Promise((r) => setTimeout(r, ms))

const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
const paginas = alvos.filter((t) => t.type === 'page')
// Prefere a aba do app; se não houver, usa qualquer aba e navega até ele.
const alvo = paginas.find((t) => t.url.startsWith(appUrl)) ?? paginas[0]
if (!alvo) throw new Error('nenhuma aba disponível no Chrome')

const ws = new WebSocket(alvo.webSocketDebuggerUrl)
await new Promise((r, j) => {
  ws.addEventListener('open', r)
  ws.addEventListener('error', j)
})

let id = 1
const pendentes = new Map()
ws.addEventListener('message', (e) => {
  const m = JSON.parse(e.data)
  if (m.id && pendentes.has(m.id)) {
    const { ok, erro } = pendentes.get(m.id)
    pendentes.delete(m.id)
    m.error ? erro(new Error(m.error.message)) : ok(m.result)
  }
})
const cdp = (metodo, params = {}) =>
  new Promise((ok, erro) => {
    const i = id++
    pendentes.set(i, { ok, erro })
    ws.send(JSON.stringify({ id: i, method: metodo, params }))
  })

await cdp('Page.enable')
await cdp('Runtime.enable')
await cdp('Page.navigate', { url: appUrl })
await espera(2500)
await cdp('Runtime.evaluate', { expression: 'sessionStorage.clear()' })
await cdp('Page.reload')
await espera(2200)

await cdp('Runtime.evaluate', {
  expression: `[...document.querySelectorAll('button')].find(b => b.textContent.includes('Criar conta'))?.click()`,
})
await espera(1200)

const estado = await cdp('Runtime.evaluate', {
  expression: `JSON.stringify({
    titulo: document.querySelector('h1')?.textContent,
    campos: [...document.querySelectorAll('form input, form select')].map(e => e.id || e.type),
    aceite: !!document.querySelector('input[type=checkbox]'),
    botao: [...document.querySelectorAll('button')].map(b => b.textContent.trim())
  })`,
  returnByValue: true,
})

console.log('tela de cadastro:', JSON.parse(estado.result.value))

const captura = await cdp('Page.captureScreenshot', { format: 'png' })
fs.writeFileSync(saida, Buffer.from(captura.data, 'base64'))
console.log(`salvo: ${saida} (${fs.statSync(saida).size} bytes)`)
ws.close()
process.exit(0)
