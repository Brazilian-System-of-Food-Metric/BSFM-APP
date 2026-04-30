// BSFM - Service Worker
const CACHE_NAME = 'bsfm-cache-v1';
const urlsToCache = [
  '/',
  '/index.html',
  '/login.html',
  '/dashboard.html',
  '/diario.html',
  '/analisador-ia.html',
  '/metas.html',
  '/planos.html',
  '/hospitais.html',
  '/libras.html',
  '/manifest.json',
  '/icons/Iconebsfm.png'
];

// Instalação - cache inicial
self.addEventListener('install', event => {
  self.skipWaiting();
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache => {
      return cache.addAll(urlsToCache);
    })
  );
});

// Ativação - limpa caches antigos
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(name => {
          if (name !== CACHE_NAME) {
            return caches.delete(name);
          }
        })
      );
    })
  );
});

// Interceptação de fetch - network first, fallback to cache
self.addEventListener('fetch', event => {
  event.respondWith(
    fetch(event.request)
      .then(response => {
        // Se deu certo, atualiza o cache
        if (response.status === 200) {
          const responseClone = response.clone();

// Notificações Push
self.addEventListener('push', event => {
  const options = {
    body: event.data?.text() || 'Nova notificação do BSFM',
    icon: 'icons/Iconebsfm.png',
    badge: 'icons/Iconebsfm.png',
    vibrate: [200, 100, 200],
    tag: 'bsfm-notification'
  };
  
  event.waitUntil(
    self.registration.showNotification('BSFM - Nutrição Inteligente', options)
  );
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  event.waitUntil(
    clients.openWindow('/')
  );
});
          caches.open(CACHE_NAME).then(cache => {
            cache.put(event.request, responseClone);
          });
        }
        return response;
      })
      .catch(() => {
        // Se falhou, tenta do cache
        return caches.match(event.request);
      })
  );
});
