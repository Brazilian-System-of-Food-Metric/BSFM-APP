// Verifica a ativação de 2FA pela tela de Perfil e o login seguinte.
// Cria um usuário descartável, ativa o TOTP, sai e entra com o código.
//
// Uso: node verificar-ativacao-2fa.mjs [url-app] [url-cdp]

import { createHmac, pbkdf2Sync, randomBytes } from 'node:crypto'
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

const senha = 'SenhaForte#2026'
const email = `totp.${Date.now()}@bsfm.dev`
const salt = randomBytes(16)
const hash = pbkdf2Sync(senha, salt, 210_000, 32, 'sha256')
const senhaHash = `pbkdf2-sha256$210000$${salt.toString('base64')}$${hash.toString('base64')}`

const db = new DatabaseSync(banco)
db.prepare(`
  insert into Usuarios
    (Nome, Idade, Email, EmailVerificado, SenhaHash, AceitouTermos, DataAceite, VersaoTermos,
     Sexo, Peso, Altura, PesoMeta, DataNascimento, TipoPessoa, Intolerancia, Diabetes,
     IMC, TMB, GastoTotal, TotpSegredo, TotpHabilitado, Papel, TentativasFalhas, CriadoEm)
  values (?, ?, ?, 1, ?, 1, ?, '1.0', 'Feminino', 62, 1.66, 60, ?, 'Sedentário', '', 'Não informado',
          22.5, 1360, 1632, NULL, 0, 'usuario', 0, ?)
`).run(
  'Teste Dois Fatores',
  30,
  email,
  senhaHash,
  '2026-11-14 00:00:00',
  '1996-03-10 00:00:00',
  '2026-11-14 00:00:00',
)

const limpar = () => {
  try {
    db.prepare('delete from Sessoes where UsuarioId = (select ID from Usuarios where Email = ?)').run(email)
    db.prepare('delete from Usuarios where Email = ?').run(email)
  } catch {
    /* melhor esforço */
  }
}

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
ws.addEventListener('message', (evento) => {
  const m = JSON.parse(evento.data)
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

const avaliar = async (expressao) => {
  const r = await cdp('Runtime.evaluate', { expression: expressao, returnByValue: true, awaitPromise: true })
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.text ?? 'erro')
  return r.result.value
}

async function digitar(seletor, texto) {
  const ok = await avaliar(`(() => {
    const el = document.querySelector(${JSON.stringify(seletor)})
    if (!el) return false
    el.focus()
    if (el.select) el.select()
    return true
  })()`)
  if (!ok) return false
  return cdp('Input.insertText', { text: texto })
}

const clicar = (texto, seletor = 'button') =>
  avaliar(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(seletor)})].find((b) => (b.textContent || '').trim() === ${JSON.stringify(texto)})
    if (!el) return false
    el.click()
    return true
  })()`)

const tela = () => avaliar('document.body.innerText')

try {
  await cdp('Page.enable')
  await cdp('Runtime.enable')
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false })

  await cdp('Page.navigate', { url: appUrl })
  await esperar(1600)
  await avaliar('sessionStorage.clear();')
  await cdp('Page.navigate', { url: appUrl })
  await esperar(1600)

  console.log('\n[1] Entrar e abrir o Perfil')
  await clicar('Entrar')
  await esperar(600)
  await digitar('#email', email)
  await digitar('#senha', senha)
  await avaliar('document.querySelector("form").requestSubmit()')
  await esperar(2200)
  conferir('login inicial', (await tela()).includes('Teste'))
  await clicar('Perfil', '.nav-item')
  await esperar(1200)

  console.log('\n[2] Ativar 2FA')
  conferir('botão de ativação presente', await clicar('Começar ativação'))
  await esperar(500)
  await digitar('#senha2fa', senha)
  await avaliar('document.querySelector("#senha2fa").closest("form").requestSubmit()')
  await esperar(1800)

  const segredo = await avaliar('document.querySelector(".segredo code")?.textContent?.trim() ?? ""')
  conferir('segredo exibido', segredo.length >= 16, segredo)
  const codigo = codigoTotp(segredo)

  await digitar('#codigoAtivar', codigo)
  await clicar('Ativar 2FA')
  await esperar(2200)
  const textoAtivo = await tela()
  conferir('2FA ativado no perfil', textoAtivo.includes('2FA ativo'))
  conferir('banco marcou TotpHabilitado', Boolean(db.prepare('select TotpHabilitado from Usuarios where Email = ?').get(email)?.TotpHabilitado))

  console.log('\n[3] Sair e entrar de novo com código')
  await clicar('Sair da conta')
  await esperar(1600)
  await clicar('Entrar')
  await esperar(700)
  await digitar('#email', email)
  await digitar('#senha', senha)
  await avaliar('document.querySelector("form").requestSubmit()')
  await esperar(1400)
  conferir('pediu o código', (await tela()).includes('duas etapas'))
  await digitar('#codigo2fa', codigoTotp(segredo))
  await avaliar('document.querySelector("form").requestSubmit()')
  await esperar(2400)
  conferir('login com 2FA concluído', (await tela()).includes('Teste'))

  const c = await cdp('Page.captureScreenshot', { format: 'png', fromSurface: true })
  const fs = await import('node:fs')
  fs.writeFileSync('redesign-perfil-2fa.png', Buffer.from(c.data, 'base64'))
} finally {
  limpar()
}

console.log('\n==============================================')
console.log(falhas === 0 ? 'TODAS AS VERIFICAÇÕES PASSARAM' : `${falhas} VERIFICAÇÃO(ÕES) FALHARAM`)
process.exit(falhas === 0 ? 0 : 1)
