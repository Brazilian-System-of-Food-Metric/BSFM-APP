// Verificação visual ponta a ponta do front BSFM.
//
// Faz login na API, injeta a sessão na aba aberta (sessionStorage), recarrega e
// captura Painel, Analisar (com busca real) e Diário. Prova que a chave por
// sessão, o HMAC no navegador e o layout funcionam juntos — sem clique manual.
//
// Uso: node verificar-tela.mjs [url-api] [url-app] [prefixo-saida]

import fs from 'node:fs'

const [apiUrl = 'http://127.0.0.1:5100', appUrl = 'http://localhost:4173', prefixo = 'tela'] =
  process.argv.slice(2)

const email = process.env.BSFM_EMAIL ?? 'ana@bsfm.dev'
const senha = process.env.BSFM_SENHA ?? 'SenhaForte#2026'

const espera = (ms) => new Promise((r) => setTimeout(r, ms))

async function login() {
  const resposta = await fetch(`${apiUrl}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, senha }),
  })
  if (!resposta.ok) throw new Error(`login falhou: ${resposta.status} ${await resposta.text()}`)
  return resposta.json()
}

function conectar(url) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(url)
    ws.addEventListener('open', () => resolve(ws))
    ws.addEventListener('error', () => reject(new Error('falha ao conectar no CDP')))
  })
}

function clienteCdp(ws) {
  let proximo = 1
  const pendentes = new Map()

  ws.addEventListener('message', (evento) => {
    const msg = JSON.parse(evento.data)
    if (msg.id && pendentes.has(msg.id)) {
      const { resolver, rejeitar } = pendentes.get(msg.id)
      pendentes.delete(msg.id)
      msg.error ? rejeitar(new Error(msg.error.message)) : resolver(msg.result)
    }
  })

  return (metodo, params = {}) =>
    new Promise((resolver, rejeitar) => {
      const id = proximo++
      pendentes.set(id, { resolver, rejeitar })
      ws.send(JSON.stringify({ id, method: metodo, params }))
    })
}

try {
  const sessao = await login()
  console.log(`login ok: ${sessao.usuario?.nome}`)

  const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
  const alvo = alvos.find((t) => t.type === 'page' && t.url.startsWith(appUrl))
  if (!alvo) throw new Error(`aba ${appUrl} não encontrada; abra a página antes`)

  const ws = await conectar(alvo.webSocketDebuggerUrl)
  const cdp = clienteCdp(ws)

  await cdp('Page.enable')
  await cdp('Runtime.enable')

  const avaliar = async (expressao) => {
    const r = await cdp('Runtime.evaluate', { expression: expressao, returnByValue: true, awaitPromise: true })
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.text ?? 'erro de avaliação')
    return r.result.value
  }

  await avaliar(`
    sessionStorage.setItem('bsfm.token', ${JSON.stringify(sessao.token)});
    sessionStorage.setItem('bsfm.refresh', ${JSON.stringify(sessao.refreshToken)});
    sessionStorage.setItem('bsfm.chave', ${JSON.stringify(sessao.chaveAssinatura)});
    location.reload();
  `)

  await espera(2600)

  async function capturar(nome) {
    const captura = await cdp('Page.captureScreenshot', { format: 'png' })
    const arquivo = `${prefixo}-${nome}.png`
    fs.writeFileSync(arquivo, Buffer.from(captura.data, 'base64'))
    console.log(`  ${arquivo} (${fs.statSync(arquivo).size} bytes)`)
    return arquivo
  }

  // ---- Painel -------------------------------------------------------------
  await avaliar(`document.querySelectorAll('.nav-item')[0].click()`)
  await espera(2200)

  const painel = JSON.parse(
    await avaliar(`JSON.stringify({
      aba: document.querySelector('.topo h1')?.textContent,
      usuario: document.querySelector('.chip-usuario .nome')?.textContent,
      cards: document.querySelectorAll('.cartao').length,
      valores: [...document.querySelectorAll('.valor-simples')].map(e => e.textContent.trim()),
      erro: document.querySelector('.erro')?.textContent ?? null
    })`),
  )
  console.log('painel:', painel)
  if (painel.erro) throw new Error(`erro na interface: ${painel.erro}`)
  if (!painel.usuario) throw new Error('usuário não apareceu no topo')
  if (painel.cards < 4) throw new Error(`esperava ao menos 4 cards, veio ${painel.cards}`)
  await capturar('painel')

  // ---- Analisar: busca real ----------------------------------------------
  await avaliar(`document.querySelectorAll('.nav-item')[1].click()`)
  await espera(600)

  await avaliar(`
    const campo = document.querySelector('.busca input');
    const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
    setter.call(campo, 'arroz');
    campo.dispatchEvent(new Event('input', { bubbles: true }));
  `)

  await espera(1500)

  const busca = JSON.parse(
    await avaliar(`JSON.stringify({
      sugestoes: [...document.querySelectorAll('.sugestao')].map(b => b.textContent.trim()).slice(0, 3)
    })`),
  )
  console.log('busca:', busca)
  if (busca.sugestoes.length === 0) throw new Error('a busca não retornou sugestões')
  await capturar('analisar')

  // ---- Diário -------------------------------------------------------------
  await avaliar(`document.querySelectorAll('.nav-item')[2].click()`)
  await espera(1600)
  const diario = JSON.parse(await avaliar(`JSON.stringify({ itens: document.querySelectorAll('.item').length })`))
  console.log('diario:', diario)
  await capturar('diario')

  ws.close()
  console.log('\nOK: login, painel, busca e diário verificados.')
  process.exit(0)
} catch (erro) {
  console.error('FALHA:', erro.message)
  process.exit(1)
}
