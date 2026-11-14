import { useEffect, useMemo, useState } from 'react'
import { ErroApi, api } from './api.js'
import { Icone } from './Icones.jsx'
import { Anel, Selo, dataCurta, dataLonga, numero } from './ui.jsx'

/* Diário: tudo o que foi registrado, agrupado por dia, com a pontuação
   calculada no servidor e atalho para o plano semanal. */

const DIAS = ['Segunda', 'Terca', 'Quarta', 'Quinta', 'Sexta', 'Sabado', 'Domingo']
const TIPOS = [
  ['Cafe', 'Café da manhã'],
  ['Almoco', 'Almoço'],
  ['Jantar', 'Jantar'],
  ['Lanche', 'Lanche'],
]

const tomDaNota = (nota) => (nota >= 8 ? 'ok' : nota >= 5 ? 'aviso' : 'perigo')

// Data local do registro. O servidor grava em UTC; agrupar por UTC jogaria
// uma refeição das 21h para o dia seguinte no fuso de Brasília.
const chaveDoDia = (iso) => {
  const d = new Date(iso)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export default function Diario({ avisar, versao, irPara }) {
  const [itens, setItens] = useState([])
  const [carregando, setCarregando] = useState(true)
  const [expandido, setExpandido] = useState(null)
  const [avaliacoes, setAvaliacoes] = useState({})
  const [paraPlano, setParaPlano] = useState(null)
  const [removendo, setRemovendo] = useState(null)
  const [filtro, setFiltro] = useState('todos')

  useEffect(() => {
    let ativo = true
    ;(async () => {
      setCarregando(true)
      try {
        const lista = await api.analises(60)
        if (ativo) setItens(lista ?? [])
      } catch (falha) {
        avisar(falha instanceof ErroApi ? falha.message : 'Falha ao carregar o diário.', 'erro')
      } finally {
        if (ativo) setCarregando(false)
      }
    })()
    return () => {
      ativo = false
    }
  }, [avisar, versao])

  const filtrados = useMemo(() => {
    if (filtro === 'hoje') {
      const hoje = chaveDoDia(new Date().toISOString())
      return itens.filter((i) => chaveDoDia(i.dataAnalise) === hoje)
    }
    if (filtro === 'semana') {
      const limite = Date.now() - 7 * 24 * 60 * 60 * 1000
      return itens.filter((i) => new Date(i.dataAnalise).getTime() >= limite)
    }
    return itens
  }, [itens, filtro])

  const porDia = useMemo(() => {
    const mapa = new Map()
    for (const item of filtrados) {
      const chave = chaveDoDia(item.dataAnalise)
      if (!mapa.has(chave)) mapa.set(chave, [])
      mapa.get(chave).push(item)
    }
    return [...mapa.entries()].sort((a, b) => (a[0] < b[0] ? 1 : -1))
  }, [filtrados])

  const total = useMemo(() => filtrados.reduce((soma, i) => soma + (i.calorias ?? 0), 0), [filtrados])
  const media = porDia.length > 0 ? total / porDia.length : 0

  async function avaliar(item) {
    try {
      const nota = await api.avaliar({
        kcal: item.calorias ?? 0,
        proteinas: item.proteinas ?? 0,
        carbos: item.carbos ?? 0,
        gorduras: item.gorduras ?? 0,
        fibra: null,
      })
      setAvaliacoes((atual) => ({ ...atual, [item.id]: nota }))
      setExpandido(item.id)
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Não foi possível avaliar este item.', 'erro')
    }
  }

  async function remover(item) {
    try {
      await api.removerAnalise(item.id)
      setItens((lista) => lista.filter((i) => i.id !== item.id))
      setRemovendo(null)
      avisar(`${item.alimento} removido do diário`)
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Não foi possível remover.', 'erro')
    }
  }

  return (
    <div className="pagina pagina-estreita">
      <div className="diario-topo">
        <div className="diario-numeros">
          <div className="diario-numero">
            <span className="olho">Registros</span>
            <strong>{filtrados.length}</strong>
          </div>
          <div className="diario-numero">
            <span className="olho">Calorias</span>
            <strong>{numero(total)}<small>kcal</small></strong>
          </div>
          <div className="diario-numero">
            <span className="olho">Média por dia</span>
            <strong>{numero(media)}<small>kcal</small></strong>
          </div>
        </div>

        <div className="segmentos segmentos-mini" role="tablist" aria-label="Filtrar diário">
          {[['todos', 'Tudo'], ['semana', '7 dias'], ['hoje', 'Hoje']].map(([id, rotulo]) => (
            <button
              key={id}
              role="tab"
              aria-selected={filtro === id}
              className={`segmento${filtro === id ? ' ativo' : ''}`}
              onClick={() => setFiltro(id)}
            >
              {rotulo}
            </button>
          ))}
        </div>
      </div>

      {carregando ? (
        <div className="cartao">
          <div className="esqueleto" style={{ width: '60%' }} />
          <div className="esqueleto" style={{ width: '45%' }} />
          <div className="esqueleto" style={{ width: '52%' }} />
        </div>
      ) : filtrados.length === 0 ? (
        <div className="vazio">
          <span className="vazio-icone"><Icone nome="diario" tamanho={22} /></span>
          <strong>{filtro === 'todos' ? 'Nada registrado ainda' : 'Nenhum registro neste período'}</strong>
          <p>Registre um alimento para começar o seu histórico.</p>
          <button className="btn" onClick={() => irPara('analisar')}>Registrar alimento</button>
        </div>
      ) : (
        porDia.map(([dia, lista]) => {
          const kcalDia = lista.reduce((s, i) => s + (i.calorias ?? 0), 0)
          const protDia = lista.reduce((s, i) => s + (i.proteinas ?? 0), 0)

          return (
            <section key={dia} className="diario-dia">
              <header className="grupo-dia">
                <span>{dataLonga(`${dia}T12:00:00`)}</span>
                <span className="grupo-dia-numeros">
                  {numero(kcalDia)} kcal · {numero(protDia, 1)} g proteína
                </span>
              </header>

              <ul className="lista">
                {lista.map((item) => {
                  const nota = avaliacoes[item.id]?.pontuacao ?? item.pontuacaoSaude ?? 0
                  const dicas = avaliacoes[item.id]?.dicas ?? []
                  const aberto = expandido === item.id

                  return (
                    <li className={`item item-diario${aberto ? ' item-aberto' : ''}`} key={item.id}>
                      <div className="item-diario-principal">
                        <div className="item-diario-nome">
                          <div className="item-titulo">{item.alimento}</div>
                          <div className="item-meta">
                            {item.porcao} · {new Date(item.dataAnalise).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
                            {' · '}P {numero(item.proteinas, 0)} g · C {numero(item.carbos, 0)} g · G {numero(item.gorduras, 0)} g
                          </div>
                        </div>

                        <div className="item-diario-lado">
                          <span className="etiqueta-kcal"><strong>{numero(item.calorias)}</strong> kcal</span>
                          {nota > 0 ? (
                            <button
                              className="nota"
                              onClick={() => setExpandido(aberto ? null : item.id)}
                              aria-label={`Nota ${nota} de 10. Ver detalhes`}
                            >
                              <Anel pontuacao={nota} tamanho={34} />
                            </button>
                          ) : (
                            <button className="btn btn-fantasma btn-mini" onClick={() => avaliar(item)}>
                              Avaliar
                            </button>
                          )}
                        </div>
                      </div>

                      <div className="item-diario-acoes">
                        <button className="btn btn-fantasma btn-mini" onClick={() => setExpandido(aberto ? null : item.id)}>
                          <Icone nome="info" tamanho={14} /> {aberto ? 'Fechar' : 'Detalhes'}
                        </button>
                        <button
                          className="btn btn-fantasma btn-mini"
                          onClick={() => setParaPlano(paraPlano === item.id ? null : item.id)}
                        >
                          <Icone nome="planos" tamanho={14} /> Plano
                        </button>
                        {removendo === item.id ? (
                          <>
                            <button className="btn btn-perigo btn-mini" onClick={() => remover(item)}>Confirmar</button>
                            <button className="btn btn-fantasma btn-mini" onClick={() => setRemovendo(null)}>Cancelar</button>
                          </>
                        ) : (
                          <button className="btn btn-fantasma btn-mini" onClick={() => setRemovendo(item.id)}>
                            <Icone nome="lixeira" tamanho={14} /> Remover
                          </button>
                        )}
                      </div>

                      {aberto && (
                        <div className="item-detalhe">
                          {nota > 0 && (
                            <div className="item-detalhe-nota">
                              <Anel pontuacao={nota} tamanho={44} />
                              <div>
                                <strong>{item.analiseEmRelacaoAMeta || avaliacoes[item.id]?.mensagem || 'Avaliação nutricional'}</strong>
                                <p>{item.dicaBSFM || item.dicaBsfm || dicas[0] || 'Sem observações adicionais para este item.'}</p>
                              </div>
                            </div>
                          )}
                          {dicas.length > 1 && (
                            <ul className="avaliacao-dicas">
                              {dicas.slice(1).map((dica) => <li key={dica}>{dica}</li>)}
                            </ul>
                          )}
                          <p className="rodape-cartao">
                            Origem: {item.origem ?? 'manual'}{item.confianca != null ? ` · confiança ${Math.round(item.confianca * 100)}%` : ''}
                            {item.modeloVersao ? ` · ${item.modeloVersao}` : ''}
                          </p>
                        </div>
                      )}

                      {paraPlano === item.id && (
                        <form
                          className="item-plano"
                          onSubmit={async (e) => {
                            e.preventDefault()
                            try {
                              await api.criarRefeicao({
                                diaSemana: e.target.diaPlano.value,
                                tipoRefeicao: e.target.tipoPlano.value,
                                nomePrato: item.alimento,
                                calorias: item.calorias ?? 0,
                                proteinas: item.proteinas ?? 0,
                                carboidratos: item.carbos ?? 0,
                                gorduras: item.gorduras ?? 0,
                              })
                              avisar(`${item.alimento} salvo no plano de ${e.target.diaPlano.value}`)
                              setParaPlano(null)
                            } catch (falha) {
                              avisar(falha instanceof ErroApi ? falha.message : 'Não foi possível salvar no plano.', 'erro')
                            }
                          }}
                        >
                          <select name="diaPlano" defaultValue="Segunda" aria-label="Dia do plano">
                            {DIAS.map((d) => <option key={d} value={d}>{d}</option>)}
                          </select>
                          <select name="tipoPlano" defaultValue="Almoco" aria-label="Tipo de refeição">
                            {TIPOS.map(([valor, rotulo]) => <option key={valor} value={valor}>{rotulo}</option>)}
                          </select>
                          <button className="btn btn-mini" type="submit">Salvar</button>
                        </form>
                      )}
                    </li>
                  )
                })}
              </ul>
            </section>
          )
        })
      )}

      {!carregando && filtrados.length > 0 && (
        <p className="rodape-cartao diario-rodape">
          <Selo tom="neutro" icone="info">Dica</Selo> Toque na nota para ver a análise e em <strong>Plano</strong> para reaproveitar o prato na semana.
        </p>
      )}
    </div>
  )
}
