import { useEffect, useRef, useState } from 'react'

/* Mapa de unidades de saúde com Leaflet, carregado sob demanda.
   Sem chave de API: usa os tiles públicos do OpenStreetMap. */

let carregando = null

function carregarLeaflet() {
  if (window.L) return Promise.resolve(window.L)
  if (carregando) return carregando

  carregando = new Promise((resolver, rejeitar) => {
    if (!document.querySelector('link[data-leaflet]')) {
      const css = document.createElement('link')
      css.rel = 'stylesheet'
      css.dataset.leaflet = '1'
      css.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'
      document.head.appendChild(css)
    }

    const js = document.createElement('script')
    js.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'
    js.onload = () => (window.L ? resolver(window.L) : rejeitar(new Error('Leaflet indisponível')))
    js.onerror = () => rejeitar(new Error('Falha ao carregar o mapa'))
    document.head.appendChild(js)
  })

  return carregando
}

export default function Mapa({ pontos = [], posicao = null, altura = 260 }) {
  const container = useRef(null)
  const mapa = useRef(null)
  const [pronto, setPronto] = useState(false)

  // Cria o mapa uma vez. Mudar de lista não recria o Leaflet.
  useEffect(() => {
    let cancelado = false

    ;(async () => {
      try {
        const L = await carregarLeaflet()
        if (cancelado || !container.current || mapa.current) return

        const centro = posicao
          ? [posicao.lat, posicao.lng]
          : pontos.length > 0
            ? [pontos[0].lat, pontos[0].lng]
            : [-23.5505, -46.6333]

        const instancia = L.map(container.current, { scrollWheelZoom: false }).setView(centro, 11)
        mapa.current = instancia

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
          attribution: '© OpenStreetMap',
          maxZoom: 18,
        }).addTo(instancia)

        if (!cancelado) setPronto(true)
      } catch {
        /* sem mapa: a lista continua funcionando */
      }
    })()

    return () => {
      cancelado = true
      const instancia = mapa.current
      mapa.current = null
      try {
        instancia?.stop()
        instancia?.remove()
      } catch {
        /* já desmontado */
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Camada de marcadores: recriada quando os pontos ou a posição mudam.
  useEffect(() => {
    const L = window.L
    const instancia = mapa.current
    if (!pronto || !L || !instancia) return

    let camada = null
    try {
      camada = L.layerGroup().addTo(instancia)

      for (const ponto of pontos) {
        L.marker([ponto.lat, ponto.lng])
          .addTo(camada)
          .bindPopup(`<strong>${ponto.nome}</strong><br>${ponto.endereco ?? ''}`)
      }

      if (posicao) {
        L.circleMarker([posicao.lat, posicao.lng], {
          radius: 7,
          color: '#ffffff',
          weight: 3,
          fillColor: '#2f9e6e',
          fillOpacity: 1,
        })
          .addTo(camada)
          .bindPopup('<strong>Sua localização</strong>')
      }

      const coordenadas = pontos.map((p) => [p.lat, p.lng])
      if (posicao) coordenadas.push([posicao.lat, posicao.lng])

      if (coordenadas.length > 0) {
        // Sem animação: trocar de aba no meio de um zoom animado faz o Leaflet
        // acessar um container já removido (erro _leaflet_pos).
        instancia.fitBounds(coordenadas, { padding: [24, 24], maxZoom: 13, animate: false })
      }
    } catch {
      /* bounds inválidos ou mapa desmontado não derrubam a tela */
    }

    return () => {
      try {
        camada?.remove()
      } catch {
        /* mapa já removido */
      }
    }
  }, [pontos, posicao, pronto])

  return (
    <div
      ref={container}
      className="mapa"
      style={{ height: altura }}
      aria-label="Mapa das unidades de saúde"
    />
  )
}
