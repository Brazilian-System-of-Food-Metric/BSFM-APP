import { useEffect, useRef, useState } from 'react'

/* ============================================================================
   Captura pela câmera, ao vivo.
   Usa getUserMedia; se o navegador negar ou não houver câmera, avisa e mantém
   o envio por arquivo como alternativa.
   ========================================================================== */

export default function Camera({ aoCapturar, avisar }) {
  const [ligada, setLigada] = useState(false)
  const [erro, setErro] = useState('')
  const video = useRef(null)
  const stream = useRef(null)

  useEffect(() => {
    return () => {
      stream.current?.getTracks().forEach((t) => t.stop())
    }
  }, [])

  async function ligar() {
    setErro('')
    try {
      const midia = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment', width: { ideal: 1280 } },
        audio: false,
      })
      stream.current = midia
      if (video.current) {
        video.current.srcObject = midia
        await video.current.play()
      }
      setLigada(true)
    } catch {
      setErro('Câmera indisponível ou permissão negada. Use o envio por arquivo.')
      avisar?.('Não foi possível abrir a câmera.', 'erro')
    }
  }

  function desligar() {
    stream.current?.getTracks().forEach((t) => t.stop())
    stream.current = null
    setLigada(false)
  }

  function capturar() {
    const elemento = video.current
    if (!elemento) return

    const tela = document.createElement('canvas')
    tela.width = elemento.videoWidth || 1280
    tela.height = elemento.videoHeight || 720
    tela.getContext('2d').drawImage(elemento, 0, 0, tela.width, tela.height)

    const base64 = tela.toDataURL('image/jpeg', 0.85).split(',')[1]
    desligar()
    aoCapturar(base64)
  }

  return (
    <div>
      {!ligada ? (
        <button className="btn btn-fantasma btn-mini" onClick={ligar} type="button">
          Abrir câmera
        </button>
      ) : (
        <>
          <video
            ref={video}
            playsInline
            muted
            style={{ width: '100%', borderRadius: 'var(--raio-md)', marginTop: 10, background: '#000' }}
          />
          <div className="acoes" style={{ marginTop: 10 }}>
            <button className="btn btn-mini" onClick={capturar} type="button">Capturar</button>
            <button className="btn btn-fantasma btn-mini" onClick={desligar} type="button">Fechar câmera</button>
          </div>
        </>
      )}

      {erro && <div className="erro" style={{ marginTop: 10 }}>{erro}</div>}
    </div>
  )
}
