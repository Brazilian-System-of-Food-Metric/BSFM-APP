import { useRef, useState } from 'react'
import { ErroApi, api } from './api.js'
import { lerTexto } from './ocr.js'

/* ============================================================================
   Importação de prescrição nutricional.
   Foto → OCR local → o servidor recorta as refeições → o usuário escolhe o dia
   e confirma. Nada é salvo sem revisão.
   ========================================================================== */

const DIAS = ['Segunda', 'Terca', 'Quarta', 'Quinta', 'Sexta', 'Sabado', 'Domingo']

export default function Prescricao({ avisar, aoImportar }) {
  const [etapa, setEtapa] = useState('inicial')
  const [progresso, setProgresso] = useState(0)
  const [status, setStatus] = useState('')
  const [refeicoes, setRefeicoes] = useState([])
  const [dia, setDia] = useState('Segunda')
  const [selecionadas, setSelecionadas] = useState({})
  const [salvando, setSalvando] = useState(false)
  const arquivoRef = useRef(null)

  async function processar(evento) {
    const arquivo = evento.target.files?.[0]
    if (!arquivo) return

    setEtapa('ocr')
    setProgresso(0)
    setStatus('Carregando motor de OCR…')

    try {
      const texto = await lerTexto(arquivo, (p, s) => {
        setProgresso(p)
        setStatus(s)
      })

      if (texto.length < 10) {
        avisar('Não foi possível ler texto na imagem.', 'erro')
        setEtapa('inicial')
        return
      }

      const resposta = await api.lerPrescricao(texto)
      const lista = resposta.refeicoes ?? []
      setRefeicoes(lista)
      setSelecionadas(Object.fromEntries(lista.map((_, i) => [i, true])))
      setEtapa('revisao')
    } catch (falha) {
      avisar(
        falha instanceof ErroApi
          ? falha.message
          : 'Não identificamos refeições na imagem. Tente uma foto mais nítida.',
        'erro',
      )
      setEtapa('inicial')
    } finally {
      if (arquivoRef.current) arquivoRef.current.value = ''
    }
  }

  async function importar() {
    setSalvando(true)
    try {
      let total = 0
      for (const [indice, ativo] of Object.entries(selecionadas)) {
        if (!ativo) continue
        const refeicao = refeicoes[Number(indice)]
        const calorias = refeicao.itens.reduce((soma, i) => soma + (i.kcal ?? 0), 0)

        await api.criarRefeicao({
          diaSemana: dia,
          tipoRefeicao: refeicao.nome,
          nomePrato: refeicao.itens.map((i) => i.descricao).join(', ').slice(0, 180),
          calorias,
        })
        total++
      }

      avisar(`${total} refeição(ões) adicionadas ao plano de ${dia}`)
      setEtapa('inicial')
      setRefeicoes([])
      aoImportar?.()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao importar.', 'erro')
    } finally {
      setSalvando(false)
    }
  }

  if (etapa === 'inicial') {
    return (
      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Importar prescrição</div>
        <p className="rodape-cartao" style={{ marginTop: 0 }}>
          Foto do plano alimentar impresso. O texto é lido no seu aparelho e o servidor
          separa as refeições — você revisa antes de salvar.
        </p>
        <input
          ref={arquivoRef}
          type="file"
          accept="image/png,image/jpeg,image/webp"
          onChange={processar}
          style={{ marginTop: 10 }}
          aria-label="Foto da prescrição nutricional"
        />
      </div>
    )
  }

  if (etapa === 'ocr') {
    return (
      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Lendo a prescrição</div>
        <div className="barra"><span style={{ width: `${progresso}%` }} /></div>
        <div className="rodape-cartao" style={{ marginTop: 10 }}>{status} {progresso}%</div>
      </div>
    )
  }

  return (
    <div className="cartao" style={{ marginBottom: 14 }}>
      <div className="cartao-titulo">Revisar importação</div>

      <div className="campo">
        <label htmlFor="diaImport">Adicionar ao dia</label>
        <select id="diaImport" value={dia} onChange={(e) => setDia(e.target.value)}>
          {DIAS.map((d) => <option key={d} value={d}>{d}</option>)}
        </select>
      </div>

      <ul className="lista" style={{ marginBottom: 14 }}>
        {refeicoes.map((refeicao, indice) => (
          <li className="item" key={indice}>
            <label style={{ display: 'flex', gap: 10, alignItems: 'flex-start', cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={Boolean(selecionadas[indice])}
                onChange={(e) => setSelecionadas((s) => ({ ...s, [indice]: e.target.checked }))}
                style={{ marginTop: 3 }}
              />
              <span>
                <span className="item-titulo">{refeicao.nome}</span>
                <span className="item-meta" style={{ display: 'block' }}>
                  {refeicao.itens.map((i) => i.descricao).join(' · ')}
                </span>
              </span>
            </label>
          </li>
        ))}
      </ul>

      <div className="acoes">
        <button className="btn" onClick={importar} disabled={salvando}>
          {salvando ? 'Importando…' : 'Adicionar ao plano'}
        </button>
        <button className="btn btn-fantasma" onClick={() => { setEtapa('inicial'); setRefeicoes([]) }}>
          Cancelar
        </button>
      </div>
    </div>
  )
}
