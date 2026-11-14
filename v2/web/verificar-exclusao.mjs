// Verifica a exclusão de conta (LGPD): cria um usuário descartável no SQLite
// de desenvolvimento, entra pela UI, exclui e confirma que a linha sumiu.
//
// Uso: node verificar-exclusao.mjs [url-app] [url-cdp]

import { pbkdf2Sync, randomBytes } from 'node:crypto'
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

const senha = 'SenhaForte#2026'
const email = `exclusao.${Date.now()}@bsfm.dev`
const salt = randomBytes(16)
const hash = pbkdf2Sync(senha, salt, 210_000, 32, 'sha256')
const senhaHash = `pbkdf2-sha256$210000$${salt.toString('base64')}$${hash.toString('base64')}`

const db = new DatabaseSync(banco)
db.prepare(`
  insert into Usuarios
    (Nome, Idade, Email, EmailVerificado, SenhaHash, AceitouTermos, DataAceite, VersaoTermos,
     Sexo, Peso, Altura, PesoMeta, DataNascimento, TipoPessoa, Intolerancia, Diabetes,
     IMC, TMB, GastoTotal, TotpSegredo, TotpHabilitado, Papel, TentativasFalhas, CriadoEm)
  values (?, ?, ?, 1, ?, 1, ?, '1.0', 'Feminino', 60, 1.65, 58, ?, 'Sedentário', '', 'Não informado',
          22.04, 1350, 1620, ?, 0, 'usuario', 0, ?)
`).run(
  'Conta Descartável',
  31,
  email,
  senhaHash,
  '2026-11-14 00:00:00',
  '1995-05-20 00:00:00',
  'GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ',
  '2026-11-14 00:00:00',
)

const existe = () => Boolean(db.prepare('select 1 as x from Usuarios where Email = ?').get(email))
conferir('usuário descartável criado', existe(), email)

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
    problemas.push(m.params.exceptionDetails?.exception?.description ?? 'exceção')
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

await cdp('Page.enable')
await cdp('Runtime.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false })

await cdp('Page.navigate', { url: appUrl })
await esperar(1600)
await avaliar('sessionStorage.clear();')
await cdp('Page.navigate', { url: appUrl })
await esperar(1600)

console.log('\n[1] Entrar com a conta descartável')
await clicar('Entrar')
await esperar(600)
await digitar('#email', email)
await digitar('#senha', senha)
await avaliar('document.querySelector("form").requestSubmit()')
await esperar(2200)
conferir('login da conta descartável', (await avaliar('document.body.innerText')).includes('Conta'))

console.log('\n[2] Excluir pela tela de Privacidade')
await clicar('Perfil', '.nav-item')
await esperar(1200)
conferir('botão de exclusão presente', await clicar('Excluir conta'))
await esperar(400)

conferir('campo de senha presente', Boolean(await avaliar('Boolean(document.querySelector("#senhaExclusao"))')))
await digitar('#senhaExclusao', senha)
await digitar('#confirmacaoExclusao', 'EXCLUIR')

const confirmou = await clicar('Confirmar exclusão')
conferir('confirmação disparada', confirmou)
await esperar(2400)

conferir('voltou para a landing', (await avaliar('document.body.innerText')).includes('Comer bem começa'))
conferir('linha removida do banco', !existe())

console.log('\n[3] Console')
const relevantes = problemas.filter((p) => !p.includes('401'))
conferir('sem exceção de runtime', relevantes.length === 0, relevantes.slice(0, 2).join(' | '))
if (relevantes.length) console.log(relevantes.join('\n'))

console.log('\n==============================================')
console.log(falhas === 0 ? 'TODAS AS VERIFICAÇÕES PASSARAM' : `${falhas} VERIFICAÇÃO(ÕES) FALHARAM`)
process.exit(falhas === 0 ? 0 : 1)
