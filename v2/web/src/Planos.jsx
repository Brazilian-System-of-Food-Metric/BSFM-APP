import { useCallback, useEffect, useMemo, useState } from 'react'
import { ErroApi, api } from './api.js'
import Prescricao from './Prescricao.jsx'
import { Icone } from './Icones.jsx'
import { Barra, Selo, numero } from './ui.jsx'

/* Planos: a semana alimentar em 7 abas, com conclusão de refeição e importação
   de prescrição por OCR. */

const DIAS = [
  ['Segunda', 'Seg'],
  ['Terca', 'Ter'],
  ['Quarta', 'Qua'],
  ['Quinta', 'Qui'],
  ['Sexta', 'Sex'],
  ['Sabado', 'Sáb'],
  ['Domingo', 'Dom'],
]

const TIPOS = [
  ['Cafe', 'Café da manhã'],
  ['Almoco', 'Almoço'],
  ['Jantar', 'Jantar'],
  ['Lanche', 'Lanche'],
]

const diaDeHoje = () => {
  const nomes = ['Domingo', 'Segunda', 'Terca', 'Quarta', 'Quinta', 'Sexta', 'Sabado']
  return nomes[new Date().getDay()]
}

export default function Planos({ avisar }) {
  const [itens, setItens] = useState([])
  const [concluidas, setConcluidas] = useState(0)
  const [calorias, setCalorias] = useState(0)
  const [diaAtivo, setDiaAtivo] = useState(diaDeHoje())
  const [form, setForm] = useState({
    tipoRefeicao: 'Almoco',
    nomePrato: '',
    calorias: '',
    ingredientes: '',
    modoPreparo: '',
  })
  const [salvando, setSalvando] = useState(false)
  const [detalhado, setDetalhado] = useState(false)
  const [editando, setEditando] = useState(null)

  const carregar = useCallback(async () => {
    try {
      const dados = await api.refeicoes()
      setItens(dados.itens ?? [])
      setConcluidas(dados.concluidas ?? 0)
      setCalorias(dados.caloriasSemana ?? 0)
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao carregar os planos.', 'erro')
    }
  }, [avisar])

  useEffect(() => {
    carregar()
  }, [carregar])

  async function adicionar(evento) {
    evento.preventDefault()
    setSalvando(true)
    try {
      const dados = {
        diaSemana: diaAtivo,
        tipoRefeicao: form.tipoRefeicao,
        nomePrato: form.nomePrato.trim(),
        calorias: Number(form.calorias) || 0,
        ingredientes: form.ingredientes.trim(),
        modoPreparo: form.modoPreparo.trim(),
      }

      if (editando) {
        await api.atualizarRefeicao(editando, dados)
        avisar('Refeição atualizada')
      } else {
        await api.criarRefeicao(dados)
        avisar(`Refeição adicionada em ${diaAtivo}`)
      }

      setForm((f) => ({ ...f, nomePrato: '', calorias: '', ingredientes: '', modoPreparo: '' }))
      setEditando(null)
      setDetalhado(false)
      carregar()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao salvar a refeição.', 'erro')
    } finally {
      setSalvando(false)
    }
  }

  function iniciarEdicao(item) {
    setEditando(item.id)
    setDiaAtivo(item.diaSemana)
    setDetalhado(Boolean(item.ingredientes || item.modoPreparo))
    setForm({
      tipoRefeicao: item.tipoRefeicao,
      nomePrato: item.nomePrato,
      calorias: item.calorias ? String(Math.round(item.calorias)) : '',
      ingredientes: item.ingredientes ?? '',
      modoPreparo: item.modoPreparo ?? '',
    })
    document.getElementById('form-plano')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  function cancelarEdicao() {
    setEditando(null)
    setForm({ tipoRefeicao: 'Almoco', nomePrato: '', calorias: '', ingredientes: '', modoPreparo: '' })
  }

  async function alternarConclusao(item) {
    try {
      await api.concluirRefeicao(item.id, !item.concluida)
      setItens((lista) =>
        lista.map((i) => (i.id === item.id ? { ...i, concluida: !i.concluida } : i)),
      )
      setConcluidas((n) => (item.concluida ? Math.max(0, n - 1) : n + 1))
    } catch {
      avisar('Não foi possível atualizar a refeição.', 'erro')
    }
  }

  async function remover(id) {
    try {
      await api.removerRefeicao(id)
      carregar()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao remover.', 'erro')
    }
  }

  const doDia = useMemo(() => itens.filter((i) => i.diaSemana === diaAtivo), [itens, diaAtivo])
  const kcalDia = doDia.reduce((s, i) => s + (i.calorias ?? 0), 0)
  const percConcluido = itens.length > 0 ? Math.round((concluidas / itens.length) * 100) : 0

  return (
    <div className="pagina pagina-estreita">
      <Prescricao avisar={avisar} aoImportar={carregar} />

      <section className="cartao cartao-plano">
        <div className="cartao-cabeca">
          <div>
            <span className="olho">Semana</span>
            <h3>{itens.length} refeição(ões) planejada(s)</h3>
          </div>
          <Selo tom={percConcluido === 100 && itens.length > 0 ? 'ok' : 'neutro'}>
            {concluidas} cumpridas
          </Selo>
        </div>

        <div className="plano-numeros">
          <div><strong>{numero(calorias)}</strong><span>kcal na semana</span></div>
          <div><strong>{numero(kcalDia)}</strong><span>kcal em {diaAtivo}</span></div>
          <div><strong>{numero(percConcluido, 0)}%</strong><span>concluído</span></div>
        </div>
        <Barra percentual={percConcluido} />
      </section>

      <div className="dias" role="tablist" aria-label="Dias da semana">
        {DIAS.map(([valor, rotulo]) => {
          const total = itens.filter((i) => i.diaSemana === valor).length
          return (
            <button
              key={valor}
              role="tab"
              aria-selected={diaAtivo === valor}
              className={`dia${diaAtivo === valor ? ' dia-ativo' : ''}`}
              onClick={() => setDiaAtivo(valor)}
            >
              <span>{rotulo}</span>
              <small>{total || '—'}</small>
            </button>
          )
        })}
      </div>

      <form className="cartao" id="form-plano" onSubmit={adicionar}>
        <div className="cartao-cabeca">
          <div>
            <span className="olho">{editando ? 'Editando' : `Adicionar em ${diaAtivo}`}</span>
            <h3>{editando ? 'Editar refeição' : 'Nova refeição'}</h3>
          </div>
          <button
            type="button"
            className="btn btn-fantasma btn-mini"
            onClick={() => setDetalhado((v) => !v)}
          >
            <Icone nome="editar" tamanho={14} /> {detalhado ? 'Ocultar detalhes' : 'Ingredientes e preparo'}
          </button>
        </div>

        <div className="linha-campos">
          <div className="campo">
            <label htmlFor="tipo">Refeição</label>
            <select
              id="tipo"
              value={form.tipoRefeicao}
              onChange={(e) => setForm({ ...form, tipoRefeicao: e.target.value })}
            >
              {TIPOS.map(([valor, rotulo]) => <option key={valor} value={valor}>{rotulo}</option>)}
            </select>
          </div>
          <div className="campo">
            <label htmlFor="prato">Prato</label>
            <input
              id="prato"
              value={form.nomePrato}
              onChange={(e) => setForm({ ...form, nomePrato: e.target.value })}
              required
              minLength={2}
              maxLength={200}
              placeholder="Frango com arroz e salada"
            />
          </div>
          <div className="campo campo-curto">
            <label htmlFor="kcalPrato">Kcal</label>
            <input
              id="kcalPrato"
              type="number"
              min="0"
              max="5000"
              value={form.calorias}
              onChange={(e) => setForm({ ...form, calorias: e.target.value })}
              placeholder="450"
            />
          </div>
        </div>

        {detalhado && (
          <>
            <div className="campo">
              <label htmlFor="ingredientes">Ingredientes</label>
              <input
                id="ingredientes"
                value={form.ingredientes}
                onChange={(e) => setForm({ ...form, ingredientes: e.target.value })}
                placeholder="Arroz, feijão, frango grelhado, salada"
                maxLength={500}
              />
            </div>
            <div className="campo">
              <label htmlFor="preparo">Modo de preparo</label>
              <input
                id="preparo"
                value={form.modoPreparo}
                onChange={(e) => setForm({ ...form, modoPreparo: e.target.value })}
                placeholder="Grelhar o frango, cozinhar o arroz…"
                maxLength={500}
              />
            </div>
          </>
        )}

        <div className="acoes">
          <button className="btn" type="submit" disabled={salvando}>
            {salvando ? 'Salvando…' : editando ? 'Salvar alterações' : 'Adicionar ao plano'}
          </button>
          {editando && (
            <button type="button" className="btn btn-fantasma" onClick={cancelarEdicao}>
              Cancelar edição
            </button>
          )}
        </div>
      </form>

      {doDia.length === 0 ? (
        <div className="vazio">
          <span className="vazio-icone"><Icone nome="planos" tamanho={22} /></span>
          <strong>Nenhuma refeição em {diaAtivo}</strong>
          <p>Adicione acima ou importe uma prescrição por foto.</p>
        </div>
      ) : (
        <ul className="lista">
          {doDia.map((item) => (
            <li className={`item item-plano${item.concluida ? ' item-concluido' : ''}`} key={item.id}>
              <label className="item-check">
                <input
                  type="checkbox"
                  checked={Boolean(item.concluida)}
                  onChange={() => alternarConclusao(item)}
                  aria-label={`Concluir ${item.nomePrato}`}
                />
                <span>
                  <span className="item-titulo">{item.nomePrato}</span>
                  <span className="item-meta">
                    {TIPOS.find(([valor]) => valor === item.tipoRefeicao)?.[1] ?? item.tipoRefeicao}
                    {' · '}{numero(item.calorias)} kcal
                    {item.ingredientes ? ` · ${item.ingredientes}` : ''}
                  </span>
                </span>
              </label>

              <div className="item-plano-acoes">
                {item.concluida ? <Selo tom="ok" icone="check">cumprida</Selo> : null}
                <button
                  className="btn btn-fantasma btn-mini"
                  onClick={() => iniciarEdicao(item)}
                  aria-label={`Editar ${item.nomePrato}`}
                >
                  <Icone nome="editar" tamanho={14} />
                </button>
                <button className="btn btn-fantasma btn-mini" onClick={() => remover(item.id)} aria-label={`Remover ${item.nomePrato}`}>
                  <Icone nome="lixeira" tamanho={14} />
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
