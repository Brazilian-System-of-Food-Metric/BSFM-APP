import { useRef, useState } from 'react'
import { ErroApi, api } from './api.js'
import { Icone } from './Icones.jsx'
import { lerTexto } from './ocr.js'
import { Selo, numero } from './ui.jsx'

/* Importação de prescrição nutricional.
   Foto → OCR local → o servidor recorta as refeições → revisão → salvar. */

const DIAS = [
  ['Segunda', 'Segunda'],
  ['Terca', 'Terça'],
  ['Quarta', 'Quarta'],
  ['Quinta', 'Quinta'],
  ['Sexta', 'Sexta'],
  ['Sabado', 'Sábado'],
  ['Domingo', 'Domingo'],
]

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
      <div className="cartao">
        <div className="cartao-cabeca">
          <div>
            <span className="olho">Atalho</span>
            <h3>Importar prescrição</h3>
          </div>
          <Selo tom="neutro" icone="foto">OCR local</Selo>
        </div>
        <p className="rodape-cartao" style={{ marginTop: 0 }}>
          Tem o plano alimentar impresso? Fotografe. O texto é lido no seu aparelho, o servidor separa
          as refeições e você revisa antes de salvar.
        </p>

        <input
          ref={arquivoRef}
          id="arquivoPrescricao"
          type="file"
          accept="image/png,image/jpeg,image/webp"
          onChange={processar}
          hidden
        />
        <button className="btn btn-fantasma" onClick={() => arquivoRef.current?.click()}>
          <Icone nome="foto" tamanho={17} /> Escolher foto da prescrição
        </button>
      </div>
    )
  }

  if (etapa === 'ocr') {
    return (
      <div className="cartao">
        <div className="cartao-cabeca">
          <div><span className="olho">OCR</span><h3>Lendo a prescrição</h3></div>
          <span className="tag">{progresso}%</span>
        </div>
        <div className="barra"><span style={{ width: `${progresso}%` }} /></div>
        <p className="rodape-cartao" style={{ marginTop: 10 }}>{status || 'Preparando…'}</p>
      </div>
    )
  }

  const totalSelecionadas = Object.values(selecionadas).filter(Boolean).length

  return (
    <div className="cartao">
      <div className="cartao-cabeca">
        <div><span className="olho">Revisão</span><h3>Refeições encontradas</h3></div>
        <span className="tag">{totalSelecionadas} de {refeicoes.length}</span>
      </div>

      <div className="campo">
        <label htmlFor="diaImport">Adicionar ao dia</label>
        <select id="diaImport" value={dia} onChange={(e) => setDia(e.target.value)}>
          {DIAS.map(([valor, rotulo]) => <option key={valor} value={valor}>{rotulo}</option>)}
        </select>
      </div>

      <ul className="lista" style={{ marginBottom: 14 }}>
        {refeicoes.map((refeicao, indice) => {
          const kcal = refeicao.itens.reduce((s, i) => s + (i.kcal ?? 0), 0)
          return (
            <li className="item" key={`${refeicao.nome}-${indice}`}>
              <label className="item-check">
                <input
                  type="checkbox"
                  checked={Boolean(selecionadas[indice])}
                  onChange={(e) => setSelecionadas((s) => ({ ...s, [indice]: e.target.checked }))}
                />
                <span>
                  <span className="item-titulo">{refeicao.nome}</span>
                  <span className="item-meta">{refeicao.itens.map((i) => i.descricao).join(' · ')}</span>
                </span>
              </label>
              <span className="etiqueta-kcal">{kcal > 0 ? `${numero(kcal)} kcal` : 'sem kcal'}</span>
            </li>
          )
        })}
      </ul>

      <div className="acoes">
        <button className="btn" onClick={importar} disabled={salvando || totalSelecionadas === 0}>
          {salvando ? 'Importando…' : `Adicionar ${totalSelecionadas} ao plano`}
        </button>
        <button className="btn btn-fantasma" onClick={() => { setEtapa('inicial'); setRefeicoes([]) }}>
          Cancelar
        </button>
      </div>
    </div>
  )
}
