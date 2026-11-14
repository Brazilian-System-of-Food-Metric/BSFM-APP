// Verificação visual e funcional do redesenho, via CDP.
//
// Uso: node verificar-redesign.mjs [url-app] [url-cdp]
// Requer um Chrome com --remote-debugging-port=9222 e os servidores no ar.
//
// Gera redesign-*.png e falha (exit 1) se houver exceção de runtime, erro de
// console ou se uma tela esperada não aparecer.

import fs from 'node:fs'

const appUrl = process.argv[2] ?? 'http://localhost:4173'
const cdpUrl = process.argv[3] ?? 'http://127.0.0.1:9222'

const esperar = (ms) => new Promise((r) => setTimeout(r, ms))
let falhas = 0

const conferir = (nome, ok, detalhe = '') => {
  console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${nome}${ok || !detalhe ? '' : `  -> ${detalhe}`}`)
  if (!ok) falhas++
}

const alvos = await (await fetch(`${cdpUrl}/json/list`)).json()
const alvo = alvos.find((t) => t.type === 'page' && t.url.startsWith(appUrl)) ?? alvos.find((t) => t.type === 'page')
if (!alvo) throw new Error('Nenhuma página disponível no CDP')

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
    problemas.push(`exceção: ${m.params.exceptionDetails?.exception?.description ?? m.params.exceptionDetails?.text}`)
  }
  if (m.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(m.params.type)) {
    problemas.push(`console.${m.params.type}: ${m.params.args.map((a) => a.value ?? a.description ?? '').join(' ')}`)
  }
  if (m.method === 'Log.entryAdded' && m.params.entry.level === 'error') {
    problemas.push(`log: ${m.params.entry.text}`)
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
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.text ?? 'erro de avaliação')
  return r.result.value
}

async function capturar(nome) {
  const c = await cdp('Page.captureScreenshot', { format: 'png', fromSurface: true })
  fs.writeFileSync(`redesign-${nome}.png`, Buffer.from(c.data, 'base64'))
}

async function tela() {
  return avaliar('document.body.innerText')
}

async function digitar(seletor, texto) {
  // Digitação real via CDP: dispara os eventos que o React escuta.
  const focado = await avaliar(`(() => {
    const el = document.querySelector(${JSON.stringify(seletor)})
    if (!el) return false
    el.focus()
    if (el.select) el.select()
    return true
  })()`)
  if (!focado) return false
  await cdp('Input.insertText', { text: texto })
  return true
}

async function clicarTexto(texto, seletor = 'button') {
  return avaliar(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(seletor)})].find((b) => (b.textContent || '').trim() === ${JSON.stringify(texto)})
    if (!el) return false
    el.click()
    return true
  })()`)
}

await cdp('Page.enable')
await cdp('Runtime.enable')
await cdp('Log.enable')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false })

console.log('\n[1] Landing pública')
await cdp('Page.navigate', { url: appUrl })
await esperar(2200)
await avaliar('sessionStorage.clear(); localStorage.removeItem("bsfm.tema");')
await cdp('Page.navigate', { url: appUrl })
await esperar(2200)
problemas.length = 0

const textoLanding = await tela()
conferir('landing renderizou', textoLanding.includes('Comer bem começa'))
conferir('seção "Por que o BSFM existe"', textoLanding.includes('Por que o BSFM existe'))
conferir('seção de inclusão/Libras', textoLanding.includes('acessibilidade'))
conferir('roadmap presente', textoLanding.includes('próximos passos') || textoLanding.includes('Próximos passos'))
conferir('CTA de instalação', textoLanding.includes('Instalar'))
await capturar('landing-desktop')

console.log('\n[2] Login')
conferir('botão Entrar clicado', await clicarTexto('Entrar'))
await esperar(700)
const textoLogin = await tela()
conferir('tela de login', textoLogin.includes('Entrar na sua conta'))
conferir('link de recuperação', textoLogin.includes('Esqueci minha senha'))
conferir('alternância de tema', (await avaliar('document.documentElement.dataset.tema')) === 'claro')
await capturar('login-desktop')

console.log('\n[3] Sessão com usuário de desenvolvimento')
await digitar('#email', 'ana@bsfm.dev')
await digitar('#senha', 'SenhaForte#2026')
await avaliar('document.querySelector("form").requestSubmit()')
await esperar(2600)

let textoApp = await tela()
conferir('painel abriu', textoApp.includes('Painel') && (textoApp.includes('Bom dia') || textoApp.includes('Boa tarde') || textoApp.includes('Boa noite')))
conferir('macros no painel', textoApp.includes('Macronutrientes'))
conferir('hidratação no painel', textoApp.toLowerCase().includes('água de hoje'))
await capturar('painel-desktop')

console.log('\n[4] Navegação pelas abas')
for (const aba of ['Analisar', 'Diário', 'Planos', 'Metas', 'Perfil']) {
  const ok = await clicarTexto(aba, '.nav-item')
  await esperar(900)
  const conteudo = await tela()
  conferir(`aba ${aba}`, ok && conteudo.length > 100)
  await capturar(`aba-${aba.toLowerCase()}`)
}

console.log('\n[5] Diário (assinatura com query)')
await clicarTexto('Diário', '.nav-item')
await esperar(1400)
const textoDiario = await tela()
conferir('diário sem erro de assinatura', !textoDiario.includes('Sessão encerrada') && !textoDiario.includes('Falha ao carregar o diário'))
conferir('diário carregou os números', textoDiario.toLowerCase().includes('registros') && textoDiario.toLowerCase().includes('média por dia'))

console.log('\n[6] Perfil e 2FA')
await clicarTexto('Perfil', '.nav-item')
await esperar(1200)
const textoPerfil = await tela()
conferir('perfil com segurança', textoPerfil.includes('Segurança'))
conferir('perfil com privacidade', textoPerfil.includes('Privacidade'))
conferir('perfil com emergências', textoPerfil.includes('Emergências'))
conferir('perfil com aplicativo', textoPerfil.includes('Aplicativo'))
await capturar('perfil-desktop')

console.log('\n[7] Mobile')
await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 2, mobile: true })
await esperar(600)
await capturar('painel-mobile')
await clicarTexto('Perfil', '.aba')
await esperar(800)
await capturar('perfil-mobile')
await clicarTexto('Painel', '.aba')
await esperar(600)
conferir('barra mobile visível', await avaliar('getComputedStyle(document.querySelector(".barra-mobile")).display !== "none"'))

console.log('\n[8] Tema escuro')
await cdp('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false })
await avaliar('document.querySelector(".topo-acoes .botao-icone").click()')
await esperar(500)
conferir('tema alternou para escuro', (await avaliar('document.documentElement.dataset.tema')) === 'escuro')
await capturar('painel-escuro')

console.log('\n[9] Console e exceções')
const relevantes = problemas.filter((p) => !p.includes('favicon') && !p.includes('Download the React DevTools'))
conferir('sem erro de runtime', relevantes.length === 0, relevantes.slice(0, 3).join(' | '))
if (relevantes.length > 0) console.log(relevantes.join('\n'))

console.log('\n==============================================')
console.log(falhas === 0 ? 'TODAS AS VERIFICAÇÕES PASSARAM' : `${falhas} VERIFICAÇÃO(ÕES) FALHARAM`)
process.exit(falhas === 0 ? 0 : 1)
