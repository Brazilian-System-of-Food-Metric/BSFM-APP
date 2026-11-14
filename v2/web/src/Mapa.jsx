import { useEffect, useRef } from 'react'

/* ============================================================================
   Mapa de unidades de saúde com Leaflet, carregado sob demanda.
   Sem chave de API: usa os tiles públicos do OpenStreetMap.
   ========================================================================== */

let carregando = null

function carregarLeaflet() {
  if (window.L) return Promise.resolve(window.L)
  if (carregando) return carregando

  carregando = new Promise((resolver, rejeitar) => {
    const css = document.createElement('link')
    css.rel = 'stylesheet'
    css.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'
    document.head.appendChild(css)

    const js = document.createElement('script')
    js.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'
    js.onload = () => (window.L ? resolver(window.L) : rejeitar(new Error('Leaflet indisponível')))
    js.onerror = () => rejeitar(new Error('Falha ao carregar o mapa'))
    document.head.appendChild(js)
  })

  return carregando
}

export default function Mapa({ pontos = [], altura = 240 }) {
  const container = useRef(null)
  const mapa = useRef(null)

  useEffect(() => {
    let cancelado = false

    ;(async () => {
      try {
        const L = await carregarLeaflet()
        if (cancelado || !container.current || mapa.current) return

        const centro = pontos.length > 0 ? [pontos[0].lat, pontos[0].lng] : [-23.5505, -46.6333]
        const instancia = L.map(container.current).setView(centro, 11)
        mapa.current = instancia

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
          attribution: '© OpenStreetMap',
          maxZoom: 18,
        }).addTo(instancia)

        for (const ponto of pontos) {
          L.marker([ponto.lat, ponto.lng])
            .addTo(instancia)
            .bindPopup(`<strong>${ponto.nome}</strong><br>${ponto.endereco ?? ''}`)
        }
      } catch {
        /* sem mapa: a lista abaixo continua funcionando */
      }
    })()

    return () => {
      cancelado = true
      mapa.current?.remove()
      mapa.current = null
    }
  }, [pontos])

  return (
    <div
      ref={container}
      style={{
        height: altura,
        width: '100%',
        borderRadius: 'var(--raio-md)',
        border: '1px solid var(--borda)',
        marginTop: 12,
        background: 'var(--surface-2)',
      }}
      aria-label="Mapa das unidades de saúde"
    />
  )
}
