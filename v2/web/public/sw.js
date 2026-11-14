/* ============================================================================
   Service worker do BSFM.

   Estratégia deliberadamente conservadora:
   - /api/*  NUNCA é cacheado (dado de saúde não pode ficar velho nem vazar
             entre sessões);
   - navegação: rede primeiro, cache como reserva quando offline;
   - arquivos estáticos com hash do Vite: cache primeiro (nunca mudam).
   ========================================================================== */

const VERSAO = 'bsfm-v1'
const ESSENCIAIS = ['/', '/index.html', '/manifest.webmanifest', '/icons/icon-192.png']

self.addEventListener('install', (evento) => {
  evento.waitUntil(
    caches
      .open(VERSAO)
      .then((cache) => cache.addAll(ESSENCIAIS))
      .then(() => self.skipWaiting()),
  )
})

self.addEventListener('activate', (evento) => {
  evento.waitUntil(
    caches
      .keys()
      .then((chaves) => Promise.all(chaves.filter((k) => k !== VERSAO).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (evento) => {
  const requisicao = evento.request
  const url = new URL(requisicao.url)

  // Só lida com GET da própria origem.
  if (requisicao.method !== 'GET' || url.origin !== self.location.origin) return
  if (url.pathname.startsWith('/api/')) return

  // Navegação: rede primeiro, cache quando offline.
  if (requisicao.mode === 'navigate') {
    evento.respondWith(
      fetch(requisicao)
        .then((resposta) => {
          const copia = resposta.clone()
          caches.open(VERSAO).then((cache) => cache.put('/index.html', copia))
          return resposta
        })
        .catch(() => caches.match('/index.html').then((r) => r ?? Response.error())),
    )
    return
  }

  // Estáticos: cache primeiro.
  evento.respondWith(
    caches.match(requisicao).then((cacheado) => {
      if (cacheado) return cacheado

      return fetch(requisicao).then((resposta) => {
        if (!resposta || resposta.status !== 200 || resposta.type !== 'basic') return resposta

        const copia = resposta.clone()
        caches.open(VERSAO).then((cache) => cache.put(requisicao, copia))
        return resposta
      })
    }),
  )
})
