import { useState } from 'react'
import { ErroApi, api } from './api.js'
import CapturaMidia from './CapturaMidia.jsx'
import { base64ParaBlob } from './ocr.js'

/* ============================================================================
   Análise de rótulo nutricional.

   Fluxo: foto → OCR no próprio navegador (Tesseract.js, carregado sob demanda)
   → texto enviado ao servidor → o servidor normaliza a porção e devolve os
   campos lidos → usuário confirma → vira registro no diário.

   O OCR roda no cliente de propósito: a imagem não sai do aparelho.
   ========================================================================== */

let carregandoTesseract = null

function carregarTesseract() {
  if (window.Tesseract) return Promise.resolve(window.Tesseract)
  if (carregandoTesseract) return carregandoTesseract

  carregandoTesseract = new Promise((resolver, rejeitar) => {
    const script = document.createElement('script')
    script.src = 'https://cdn.jsdelivr.net/npm/tesseract.js@5/dist/tesseract.min.js'
    script.onload = () => (window.Tesseract ? resolver(window.Tesseract) : rejeitar(new Error('Tesseract indisponível')))
    script.onerror = () => rejeitar(new Error('Falha ao carregar o motor de OCR'))
    document.head.appendChild(script)
  })

  return carregandoTesseract
}

export default function Rotulo({ avisar, aoRegistrar }) {
  const [etapa, setEtapa] = useState('captura')
  const [progresso, setProgresso] = useState(0)
  const [status, setStatus] = useState('')
  const [texto, setTexto] = useState('')
  const [leitura, setLeitura] = useState(null)
  const [nome, setNome] = useState('')
  const [salvando, setSalvando] = useState(false)

  async function processarArquivo(arquivo) {
    if (!arquivo) return

    setEtapa('ocr')
    setProgresso(0)
    setStatus('Carregando motor de OCR…')

    try {
      const Tesseract = await carregarTesseract()

      const resultado = await Tesseract.recognize(arquivo, 'por', {
        logger: (m) => {
          if (m.status === 'recognizing text') {
            setProgresso(Math.round((m.progress ?? 0) * 100))
            setStatus('Lendo o rótulo…')
          } else if (m.status) {
            setStatus(m.status)
          }
        },
      })

      const extraido = (resultado?.data?.text ?? '').trim()
      if (extraido.length < 10) {
        avisar('Não foi possível ler texto na imagem. Tente uma foto mais nítida.', 'erro')
        setEtapa('captura')
        return
      }

      setTexto(extraido)
      const resposta = await api.lerRotulo(extraido)
      setLeitura(resposta)
      setEtapa('resultado')
    } catch (falha) {
      if (falha instanceof ErroApi) {
        avisar(falha.message, 'erro')
      } else {
        avisar('Falha no OCR. Verifique a conexão e tente novamente.', 'erro')
      }
      setEtapa('captura')
    } finally {
    }
  }

  async function registrar() {
    if (!leitura?.por100g?.kcal) return
    setSalvando(true)
    try {
      await api.registrarAnaliseManual({
        alimento: nome.trim() || 'Produto de rótulo',
        calorias: leitura.por100g.kcal ?? 0,
        proteinas: leitura.por100g.proteina ?? 0,
        carbos: leitura.por100g.carboidrato ?? 0,
        gorduras: leitura.por100g.gordura ?? 0,
        porcao: '100g',
        origem: 'rotulo',
        confianca: leitura.confianca,
        modeloVersao: 'ocr-rotulo',
      })
      avisar('Rótulo registrado no diário')
      aoRegistrar()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao registrar.', 'erro')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <div>
      {etapa === 'captura' && (
        <div className="cartao">
          <div className="cartao-titulo">Foto do rótulo</div>
          <p className="rodape-cartao" style={{ marginTop: 0 }}>
            O texto é lido no seu aparelho (OCR local). A imagem não é enviada ao servidor —
            só o texto extraído, para o cálculo.
          </p>
          <CapturaMidia
            titulo="Fotografar o rótulo"
            descricao="Enquadre a tabela nutricional inteira, com boa luz."
            rotuloArquivo="Escolher imagem"
            aoArquivo={processarArquivo}
            aoBase64={(b) => processarArquivo(base64ParaBlob(b))}
            avisar={avisar}
          />
        </div>
      )}
      {etapa === 'ocr' && (
        <div className="cartao">
          <div className="cartao-titulo">Lendo o rótulo</div>
          <div className="barra"><span style={{ width: `${progresso}%` }} /></div>
          <div className="rodape-cartao" style={{ marginTop: 10 }}>{status} {progresso}%</div>
        </div>
      )}

      {etapa === 'resultado' && leitura && (
        <div className="cartao">
          <div className="cartao-titulo">Valores encontrados</div>

          <div className="acoes" style={{ marginBottom: 14 }}>
            <span className={`tag ${leitura.confianca >= 0.75 ? 'tag-ok' : ''}`}>
              confiança {Math.round(leitura.confianca * 100)}%
            </span>
            <span className="tag">porção {leitura.porcaoBaseG} g</span>
          </div>

          {leitura.aviso && (
            <div className="erro" style={{ marginBottom: 14 }}>
              Porção não identificada no rótulo — assumimos 100 g. Confira os valores.
            </div>
          )}

          <table className="tabela" style={{ marginBottom: 16 }}>
            <thead>
              <tr><th>Nutriente</th><th style={{ textAlign: 'right' }}>Por 100 g</th></tr>
            </thead>
            <tbody>
              <tr><td>Calorias</td><td className="num">{leitura.por100g.kcal ?? '—'} kcal</td></tr>
              <tr><td>Proteína</td><td className="num">{leitura.por100g.proteina ?? '—'} g</td></tr>
              <tr><td>Carboidrato</td><td className="num">{leitura.por100g.carboidrato ?? '—'} g</td></tr>
              <tr><td>Gordura</td><td className="num">{leitura.por100g.gordura ?? '—'} g</td></tr>
              <tr><td>Fibra</td><td className="num">{leitura.por100g.fibra ?? '—'} g</td></tr>
              <tr><td>Sódio</td><td className="num">{leitura.por100g.sodio ?? '—'} mg</td></tr>
            </tbody>
          </table>

          <div className="campo">
            <label htmlFor="nomeProduto">Nome do produto</label>
            <input
              id="nomeProduto"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              placeholder="Ex: Biscoito integral"
            />
          </div>

          <details style={{ marginBottom: 16 }}>
            <summary className="rodape-cartao" style={{ cursor: 'pointer' }}>Ver texto lido pelo OCR</summary>
            <pre className="texto-ocr">{texto}</pre>
          </details>

          <div className="acoes">
            <button className="btn" onClick={registrar} disabled={salvando || !leitura.por100g.kcal}>
              {salvando ? 'Registrando…' : 'Registrar no diário'}
            </button>
            <button className="btn btn-fantasma" onClick={() => { setEtapa('captura'); setLeitura(null) }}>
              Nova foto
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
