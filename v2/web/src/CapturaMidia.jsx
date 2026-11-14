import { useEffect, useRef, useState } from 'react'

/* ============================================================================
   Zona de captura — câmera ou arquivo, com a mesma cara nas duas abas.
   Estados: convite (zona tracejada) → câmera ligada (prévia + disparador).
   ========================================================================== */

const Icone = ({ d, tamanho = 26 }) => (
  <svg width={tamanho} height={tamanho} viewBox="0 0 24 24" fill="none" stroke="currentColor"
    strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d={d} />
  </svg>
)

const ICONE_CAMERA = 'M3 8a2 2 0 0 1 2-2h2l1.5-2h7L17 6h2a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8Zm9 9a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z'

export default function CapturaMidia({
  titulo = 'Enviar imagem',
  descricao = 'Use a câmera ou escolha uma foto da galeria.',
  rotuloArquivo = 'Escolher foto',
  aoArquivo,
  aoBase64,
  avisar,
  processando = false,
  textoProcessando = 'Processando…',
}) {
  const [ligada, setLigada] = useState(false)
  const [erro, setErro] = useState('')
  const video = useRef(null)
  const stream = useRef(null)
  const entrada = useRef(null)

  useEffect(() => () => stream.current?.getTracks().forEach((t) => t.stop()), [])

  async function ligar() {
    setErro('')

    if (!navigator.mediaDevices?.getUserMedia) {
      setErro('Este navegador não oferece captura pela câmera. Use o envio por arquivo.')
      return
    }

    try {
      const midia = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment', width: { ideal: 1600 } },
        audio: false,
      })
      stream.current = midia
      setLigada(true)

      // O <video> só existe depois do render do estado ligado.
      requestAnimationFrame(() => {
        if (video.current) {
          video.current.srcObject = midia
          video.current.play().catch(() => {})
        }
      })
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

  function disparar() {
    const elemento = video.current
    if (!elemento) return

    const tela = document.createElement('canvas')
    tela.width = elemento.videoWidth || 1280
    tela.height = elemento.videoHeight || 720
    tela.getContext('2d').drawImage(elemento, 0, 0, tela.width, tela.height)

    const base64 = tela.toDataURL('image/jpeg', 0.88).split(',')[1]
    desligar()
    aoBase64?.(base64)
  }

  function escolher(evento) {
    const arquivo = evento.target.files?.[0]
    evento.target.value = ''
    if (arquivo) aoArquivo?.(arquivo)
  }

  return (
    <div className="captura">
      <input
        ref={entrada}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        onChange={escolher}
        hidden
        aria-label={rotuloArquivo}
      />

      {!ligada ? (
        <div className={`captura-zona${processando ? ' ocupada' : ''}`}>
          <span className="captura-icone"><Icone d={ICONE_CAMERA} /></span>
          <strong>{processando ? textoProcessando : titulo}</strong>
          <span className="captura-descricao">{processando ? 'Aguarde um instante.' : descricao}</span>

          {!processando && (
            <div className="captura-acoes">
              <button type="button" className="btn" onClick={ligar}>
                <Icone d={ICONE_CAMERA} tamanho={17} />
                Abrir câmera
              </button>
              <button type="button" className="btn btn-fantasma" onClick={() => entrada.current?.click()}>
                {rotuloArquivo}
              </button>
            </div>
          )}

          {erro && <span className="captura-erro">{erro}</span>}
        </div>
      ) : (
        <div className="camera-painel">
          <video ref={video} playsInline muted className="camera-video" />
          <div className="camera-controles">
            <button type="button" className="btn btn-fantasma btn-mini" onClick={desligar}>
              Fechar
            </button>
            <button type="button" className="camera-disparador" onClick={disparar} aria-label="Capturar foto">
              <span />
            </button>
            <button type="button" className="btn btn-fantasma btn-mini" onClick={() => { desligar(); entrada.current?.click() }}>
              Galeria
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
