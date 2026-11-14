// Inspeção de um site: estrutura, PWA e screenshot.
// Uso: node inspecionar-site.mjs <url> <prefixo-saida>

import fs from 'node:fs'

const [url, prefixo = 'site'] = process.argv.slice(2)
if (!url) throw new Error('informe a URL')

const espera = (ms) => new Promise((r) => setTimeout(r, ms))

const alvos = await (await fetch('http://127.0.0.1:9222/json/list')).json()
const paginas = alvos.filter((t) => t.type === 'page')
const alvo = paginas.find((t) => t.url.startsWith(new URL(url).origin)) ?? paginas[0]
if (!alvo) throw new Error('nenhuma aba no Chrome')

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

// Registra o evento de instalação antes da página carregar.
await cdp('Page.addScriptToEvaluateOnNewDocument', {
  source: `
    window.__pwa = { installFired: false, installed: false };
    addEventListener('beforeinstallprompt', () => { window.__pwa.installFired = true; });
    addEventListener('appinstalled', () => { window.__pwa.installed = true; });
  `,
})

await cdp('Emulation.setDeviceMetricsOverride', {
  width: 1280, height: 800, deviceScaleFactor: 1, mobile: false,
})
await cdp('Page.navigate', { url })
await espera(4000)

const dados = JSON.parse(
  await cdp('Runtime.evaluate', {
    expression: `(async () => {
      const linkManifest = document.querySelector('link[rel=manifest]')?.href ?? null;
      const registro = 'serviceWorker' in navigator
        ? await navigator.serviceWorker.getRegistrations()
        : [];
      return JSON.stringify({
        titulo: document.title,
        h1: [...document.querySelectorAll('h1')].map(e => e.textContent.trim()).slice(0, 3),
        secoes: [...document.querySelectorAll('h2')].map(e => e.textContent.trim()).slice(0, 12),
        links: [...new Set([...document.querySelectorAll('a[href]')].map(a => a.getAttribute('href')))].slice(0, 40),
        totalLinks: document.querySelectorAll('a[href]').length,
        manifest: linkManifest,
        tema: document.querySelector('meta[name=theme-color]')?.content ?? null,
        serviceWorkers: registro.length,
        controlado: Boolean(navigator.serviceWorker?.controller),
        standalone: matchMedia('(display-mode: standalone)').matches,
        pwa: window.__pwa ?? null,
        botoes: [...document.querySelectorAll('button, .btn, a.btn')].map(b => b.textContent.trim()).filter(Boolean).slice(0, 15)
      });
    })()`,
    returnByValue: true,
    awaitPromise: true,
  }).then((r) => r.result.value),
)

console.log(JSON.stringify(dados, null, 2))

const captura = await cdp('Page.captureScreenshot', { format: 'png' })
const arquivo = `${prefixo}.png`
fs.writeFileSync(arquivo, Buffer.from(captura.data, 'base64'))
console.log(`captura: ${arquivo}`)

ws.close()
process.exit(0)
