import { useState } from 'react'
import { ErroApi, api } from './api.js'
import CapturaMidia from './CapturaMidia.jsx'
import { Icone } from './Icones.jsx'
import { base64ParaBlob, lerTexto } from './ocr.js'
import { Anel, Selo, numero } from './ui.jsx'

/* Análise de rótulo nutricional.
   A imagem é lida no próprio aparelho (Tesseract.js, sob demanda); só o texto
   extraído vai ao servidor, que normaliza a porção e devolve os campos. */

export default function Rotulo({ avisar, aoRegistrar }) {
  const [etapa, setEtapa] = useState('captura')
  const [progresso, setProgresso] = useState(0)
  const [status, setStatus] = useState('')
  const [texto, setTexto] = useState('')
  const [leitura, setLeitura] = useState(null)
  const [avaliacao, setAvaliacao] = useState(null)
  const [nome, setNome] = useState('')
  const [salvando, setSalvando] = useState(false)

  async function processarArquivo(arquivo) {
    if (!arquivo) return

    setEtapa('ocr')
    setProgresso(0)
    setStatus('Carregando motor de OCR…')

    try {
      const extraido = await lerTexto(arquivo, (p, s) => {
        setProgresso(p)
        setStatus(s)
      })

      if (extraido.length < 10) {
        avisar('Não foi possível ler texto na imagem. Tente uma foto mais nítida.', 'erro')
        setEtapa('captura')
        return
      }

      setTexto(extraido)
      const resposta = await api.lerRotulo(extraido)
      setLeitura(resposta)
      setEtapa('resultado')

      // Pontuação contextual: meta, consumo do dia e perfil de saúde vêm do servidor.
      try {
        const nota = await api.avaliar({
          kcal: resposta.por100g.kcal ?? 0,
          proteinas: resposta.por100g.proteina ?? 0,
          carbos: resposta.por100g.carboidrato ?? 0,
          gorduras: resposta.por100g.gordura ?? 0,
          fibra: resposta.por100g.fibra ?? null,
        })
        setAvaliacao(nota)
      } catch {
        setAvaliacao(null)
      }
    } catch (falha) {
      if (falha instanceof ErroApi) {
        avisar(falha.message, 'erro')
      } else {
        avisar('Falha no OCR. Verifique a conexão e tente novamente.', 'erro')
      }
      setEtapa('captura')
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
        fibra: leitura.por100g.fibra ?? null,
        porcao: '100g',
        origem: 'rotulo',
        confianca: leitura.confianca,
        modeloVersao: 'ocr-rotulo',
      })
      avisar('Rótulo registrado no diário')
      aoRegistrar?.()
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
          <div className="cartao-cabeca">
            <div>
              <span className="olho">Leitura local</span>
              <h3>Foto do rótulo</h3>
            </div>
            <Selo tom="ok" icone="escudo">imagem não sai do aparelho</Selo>
          </div>
          <p className="rodape-cartao" style={{ marginTop: 0 }}>
            Enquadre a tabela nutricional inteira, com boa luz. O OCR local roda no seu aparelho; só
            o texto extraído é enviado para o cálculo.
          </p>
          <CapturaMidia
            titulo="Fotografar o rótulo"
            descricao="Tabela nutricional inteira, sem reflexo."
            rotuloArquivo="Escolher imagem"
            aoArquivo={processarArquivo}
            aoBase64={(b) => processarArquivo(base64ParaBlob(b))}
            avisar={avisar}
          />
        </div>
      )}

      {etapa === 'ocr' && (
        <div className="cartao">
          <div className="cartao-cabeca">
            <div><span className="olho">OCR</span><h3>Lendo o rótulo</h3></div>
            <span className="tag">{progresso}%</span>
          </div>
          <div className="barra"><span style={{ width: `${progresso}%` }} /></div>
          <p className="rodape-cartao" style={{ marginTop: 10 }}>{status || 'Preparando o motor…'}</p>
        </div>
      )}

      {etapa === 'resultado' && leitura && (
        <div className="cartao">
          <div className="cartao-cabeca">
            <div><span className="olho">Revisão</span><h3>Valores encontrados</h3></div>
            <Selo tom={leitura.confianca >= 0.75 ? 'ok' : 'aviso'}>
              confiança {Math.round(leitura.confianca * 100)}%
            </Selo>
          </div>

          {leitura.aviso && (
            <div className="callout" style={{ marginBottom: 14 }}>
              <Icone nome="info" tamanho={16} />
              <span>Porção não identificada no rótulo — assumimos 100 g. Confira os valores.</span>
            </div>
          )}

          <div className="nutrientes">
            <div className="nutriente nutriente-kcal">
              <strong>{leitura.por100g.kcal ?? '—'}</strong>
              <span>kcal</span>
            </div>
            <div className="nutriente"><strong>{leitura.por100g.proteina ?? '—'}</strong><span>proteína (g)</span></div>
            <div className="nutriente"><strong>{leitura.por100g.carboidrato ?? '—'}</strong><span>carboidrato (g)</span></div>
            <div className="nutriente"><strong>{leitura.por100g.gordura ?? '—'}</strong><span>gordura (g)</span></div>
            <div className="nutriente"><strong>{leitura.por100g.fibra ?? '—'}</strong><span>fibra (g)</span></div>
            <div className="nutriente"><strong>{leitura.por100g.sodio ?? '—'}</strong><span>sódio (mg)</span></div>
          </div>

          {avaliacao && (
            <div className={`avaliacao avaliacao-${avaliacao.classificacao}`} style={{ marginTop: 4 }}>
              <Anel pontuacao={avaliacao.pontuacao} tamanho={50} />
              <div className="avaliacao-texto">
                <div className="avaliacao-topo">
                  <strong>{avaliacao.mensagem}</strong>
                  <Selo tom={avaliacao.podeConsumir ? 'ok' : 'perigo'}>
                    {avaliacao.podeConsumir ? 'pode consumir' : 'evitar'}
                  </Selo>
                </div>
                <p className="rodape-cartao" style={{ marginTop: 2 }}>
                  Valores por 100 g · meta de {numero(avaliacao.metaKcal)} kcal
                </p>
              </div>
            </div>
          )}

          <div className="campo" style={{ marginTop: 16 }}>
            <label htmlFor="nomeProduto">Nome do produto</label>
            <input
              id="nomeProduto"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              placeholder="Ex: Biscoito integral"
              maxLength={200}
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
            <button className="btn btn-fantasma" onClick={() => { setEtapa('captura'); setLeitura(null); setAvaliacao(null) }}>
              Nova foto
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
