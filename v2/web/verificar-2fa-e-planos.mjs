// Verifica os fluxos novos: login com 2FA (TOTP), edição de refeição do plano
// e remoção. Usa o usuário bia@bsfm.dev, que tem 2FA habilitado no seed.
//
// Uso: node verificar-2fa-e-planos.mjs [url-app] [url-cdp]

import { createHmac } from 'node:crypto'
import { DatabaseSync } from 'node:sqlite'
import { fileURLToPath } from 'node:url'

const appUrl = process.argv[2] ?? 'http://localhost:4173'
const cdpUrl = process.argv[3] ?? 'http://127.0.0.1:9222'
const banco = process.env.BSFM_DB ?? fileURLToPath(new URL('../backend/BSFM.Api/UsuariosBSFM.db', import.meta.url))

const esperar = (ms) => new Promise((r) => setTimeout(r, ms))
let falhas = 0
const conferir = (nome, ok, detalhe = '') => {
  console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${nome}${ok || !detalhe ? '' : `  -> ${detalhe}`}`)
  if (!ok) falhas++
}

function base32(texto) {
  const alfabeto = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'
  let bits = 0
  let valor = 0
  const bytes = []
  for (const c of texto.replace(/=+$/, '').toUpperCase()) {
    const i = alfabeto.indexOf(c)
    if (i < 0) continue
    valor = (valor << 5) | i
    bits += 5
    if (bits >= 8) {
      bytes.push((valor >>> (bits - 8)) & 0xff)
      bits -= 8
    }
  }
  return Buffer.from(bytes)
}

function codigoTotp(segredo) {
  const contador = Math.floor(Date.now() / 1000 / 30)
  const buffer = Buffer.alloc(8)
  buffer.writeBigUInt64BE(BigInt(contador))
  const hmac = createHmac('sha1', base32(segredo)).update(buffer).digest()
  const deslocamento = hmac[hmac.length - 1] & 0x0f
  const binario =
    ((hmac[deslocamento] & 0x7f) << 24) |
    ((hmac[deslocamento + 1] & 0xff) << 16) |
    ((hmac[deslocamento + 2] & 0xff) << 8) |
    (hmac[deslocamento + 3] & 0xff)
  return String(binario % 1_000_000).padStart(6, '0')
}

const db = new DatabaseSync(banco)
const bia = db.prepare("select TotpSegredo from Usuarios where Email = 'bia@bsfm.dev'").get()
if (!bia?.TotpSegredo) throw new Error('bia sem segredo TOTP no banco')

const alvos = await (await fetch(`${cdpUrl}/json/list`)).json()
const alvo = alvos.find((t) => t.type === 'page' && t.url.startsWith(appUrl)) ?? alvos.find((t) => t.type === 'page')
if (!alvo) throw new Error('Nenhuma página no CDP')

const ws = new WebSocket(alvo.webSocketDebuggerUrl)
await new Promise((ok, erro) => {
  ws.addEventListener('open', ok)
  ws.addEventListener('error', erro)
})

let id = 1
const pendentes = new Map()
const problemas = []
ws.addEventListener('message', (evento) => {
  const m = JSON.parse(evento.data)
  if (m.id && pendentes.has(m.id)) {
    const { ok, erro } = pendentes.get(m.id)
    pendentes.delete(m.id)
    m.error ? erro(new Error(m.error.message)) : ok(m.result)
    return
  }
  if (m.method === 'Runtime.exceptionThrown') {
    problemas.push(m.params.exceptionDetails?.exception?.description ?? m.params.exceptionDetails?.text ?? 'exceção')
  }
  if (m.method === 'Runtime.consoleAPICalled' && m.params.type === 'error') {
    problemas.push(m.params.args.map((a) => a.value ?? a.description ?? '').join(' '))
  }
})

const cdp = (metodo, params = {}) =>
  new Promise((ok, erro) => {
    const i = id++
    pendentes.set(i, { ok, erro })
    ws.send(JSON.stringify({ id: i, method: metodo, params }))
  })

const avaliar = async (expressao) => {
  const r = await cdp('Runtime.evaluate', { expression: expressao, returnByValue: true, awaitPromise: true })
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.text ?? 'erro')
  return r.result.value
}

const tela = () => avaliar('document.body.innerText')

async function digitar(seletor, texto) {
  const ok = await avaliar(`(() => {
    const el = document.querySelector(${JSON.stringify(seletor)})
    if (!el) return false
    el.focus()
    if (el.select) el.select()
    return true
  })()`)
  if (!ok) return false
  await cdp('Input.insertText', { text: texto })
  return true
}

const clicar = (texto, seletor = 'button') =>
  avaliar(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(seletor)})].find((b) => (b.textContent || '').trim() === ${JSON.stringify(texto)})
    if (!el) return false
    el.click()
    return true
  })()`)

async function capturar(nome) {
  const c = await cdp('Page.captureScreenshot', { format: 'png', fromSurface: true })
  const fs = await import('node:fs')
  fs.writeFileSync(`redesign-${nome}.png`, Buffer.from(c.data, 'base64'))
}

await cdp('Page.enable')
await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false })

await cdp('Page.navigate', { url: appUrl })
await esperar(1800)
await avaliar('sessionStorage.clear(); localStorage.setItem("bsfm.tema", "claro");')
await cdp('Page.navigate', { url: appUrl })
await esperar(1800)
problemas.length = 0

console.log('\n[1] Login com 2FA obrigatório')
await clicar('Entrar')
await esperar(700)
await digitar('#email', 'bia@bsfm.dev')
await digitar('#senha', 'SenhaForte#2026')
await avaliar('document.querySelector("form").requestSubmit()')
await esperar(1400)

const texto2fa = await tela()
conferir('servidor pediu o código', texto2fa.includes('duas etapas') || texto2fa.includes('código'))
conferir('campo de 2FA apareceu', Boolean(await avaliar('Boolean(document.querySelector("#codigo2fa"))')))

const codigo = codigoTotp(bia.TotpSegredo)
console.log(`  código TOTP gerado: ${codigo}`)
await digitar('#codigo2fa', codigo)
await avaliar('document.querySelector("form").requestSubmit()')
await esperar(2600)

const textoPainel = await tela()
conferir('login 2FA concluído', textoPainel.includes('Bia') && textoPainel.includes('Painel'))
await capturar('login-2fa')

console.log('\n[2] Plano: criar, editar e remover')
await clicar('Planos', '.nav-item')
await esperar(1200)
conferir('aba Planos', (await tela()).includes('Nova refeição'))

await digitar('#prato', 'Teste automático')
await digitar('#kcalPrato', '420')
await avaliar('document.querySelector("#form-plano").requestSubmit()')
await esperar(1400)
conferir('refeição criada', (await tela()).includes('Teste automático'))

const abriuEdicao = await avaliar(`(() => {
  const b = document.querySelector('[aria-label="Editar Teste automático"]')
  if (!b) return false
  b.click()
  return true
})()`)
conferir('botão de edição existe', abriuEdicao)
await esperar(600)
await digitar('#prato', 'Teste editado')
await avaliar('document.querySelector("#form-plano").requestSubmit()')
await esperar(1400)
const textoEditado = await tela()
conferir('refeição editada', textoEditado.includes('Teste editado'))
conferir('nome antigo saiu', !textoEditado.includes('Teste automático'))

const removeu = await avaliar(`(() => {
  const b = document.querySelector('[aria-label="Remover Teste editado"]')
  if (!b) return false
  b.click()
  return true
})()`)
await esperar(1200)
conferir('refeição removida', removeu && !(await tela()).includes('Teste editado'))

console.log('\n[3] Console')
const relevantes = problemas.filter((p) => !p.includes('401') && !p.includes('favicon'))
conferir('sem erro de runtime', relevantes.length === 0, relevantes.slice(0, 2).join(' | '))
if (relevantes.length) console.log(relevantes.join('\n'))

console.log('\n==============================================')
console.log(falhas === 0 ? 'TODAS AS VERIFICAÇÕES PASSARAM' : `${falhas} VERIFICAÇÃO(ÕES) FALHARAM`)
process.exit(falhas === 0 ? 0 : 1)
