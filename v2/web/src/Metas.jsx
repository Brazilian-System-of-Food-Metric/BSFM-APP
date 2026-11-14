import { useCallback, useEffect, useState } from 'react'
import { ErroApi, api } from './api.js'
import { GraficoLinha } from './Graficos.jsx'
import { Icone } from './Icones.jsx'
import { Barra, Selo, dataCurta, numero } from './ui.jsx'

/* Metas: alvos de calorias, proteína, água e peso, com histórico e os dois
   gráficos que a v1 tinha (peso e IMC). */

export default function Metas({ avisar, usuario }) {
  const [meta, setMeta] = useState(null)
  const [historico, setHistorico] = useState([])
  const [evolucao, setEvolucao] = useState(null)
  const [form, setForm] = useState({ caloriasAlvo: '', proteinasAlvo: '', aguaAlvoMl: '', pesoAlvo: '' })
  const [salvando, setSalvando] = useState(false)

  const carregar = useCallback(async () => {
    try {
      const [atual, lista, evo] = await Promise.all([api.metas(), api.metasHistorico(), api.evolucao()])
      setMeta(atual)
      setHistorico(lista ?? [])
      setEvolucao(evo)
      setForm({
        caloriasAlvo: Math.round(atual.caloriasAlvo ?? 0),
        proteinasAlvo: Math.round(atual.proteinasAlvo ?? 0),
        aguaAlvoMl: Math.round(atual.aguaAlvoMl ?? 0),
        pesoAlvo: Number(atual.pesoAlvo ?? 0).toFixed(1),
      })
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao carregar metas.', 'erro')
    }
  }, [avisar])

  useEffect(() => {
    carregar()
  }, [carregar])

  async function salvar(evento) {
    evento.preventDefault()
    setSalvando(true)
    try {
      await api.definirMeta({
        caloriasAlvo: Number(form.caloriasAlvo),
        proteinasAlvo: Number(form.proteinasAlvo),
        aguaAlvoMl: Number(form.aguaAlvoMl),
        pesoAlvo: Number(form.pesoAlvo),
      })
      avisar('Meta atualizada e versão anterior guardada no histórico')
      await carregar()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao salvar a meta.', 'erro')
    } finally {
      setSalvando(false)
    }
  }

  const atualizar = (campo) => (e) => setForm((f) => ({ ...f, [campo]: e.target.value }))

  const percentual = meta?.percentualCalorico ?? 0
  const serie = evolucao?.serie ?? []

  return (
    <div className="pagina pagina-estreita">
      <section className="cartao cartao-meta">
        <div className="cartao-cabeca">
          <div>
            <span className="olho">Hoje</span>
            <h3>Meta calórica</h3>
          </div>
          <Selo tom={percentual > 100 ? 'aviso' : 'ok'}>
            {numero(percentual, 0)}% consumido
          </Selo>
        </div>

        <div className="meta-numeros">
          <div>
            <strong>{numero(meta?.caloriasAlvo)}</strong>
            <span>kcal alvo</span>
          </div>
          <div>
            <strong>{numero(meta?.caloriasConsumidasHoje)}</strong>
            <span>kcal hoje</span>
          </div>
          <div>
            <strong>{numero(Math.max(0, (meta?.caloriasAlvo ?? 0) - (meta?.caloriasConsumidasHoje ?? 0)))}</strong>
            <span>kcal restantes</span>
          </div>
        </div>

        <Barra percentual={percentual} alerta={percentual > 100} />

        <div className="meta-secundarias">
          <div><Icone nome="agua" tamanho={15} /> Água <strong>{numero(meta?.aguaAlvoMl)} ml</strong></div>
          <div><Icone nome="balanca" tamanho={15} /> Peso alvo <strong>{numero(meta?.pesoAlvo, 1)} kg</strong></div>
          <div><Icone nome="comida" tamanho={15} /> Proteína <strong>{numero(meta?.proteinasAlvo)} g</strong></div>
        </div>
      </section>

      <div className="grade grade-2">
        <section className="cartao">
          <div className="cartao-cabeca">
            <div>
              <span className="olho">Série histórica</span>
              <h3>Peso</h3>
            </div>
            <span className="tag">{numero(evolucao?.variacaoPeso, 1)} kg</span>
          </div>
          <GraficoLinha
            pontos={serie.map((p) => ({ data: p.data, valor: p.peso }))}
            unidade="peso"
            altura={165}
          />
        </section>

        <section className="cartao">
          <div className="cartao-cabeca">
            <div>
              <span className="olho">Série histórica</span>
              <h3>IMC</h3>
            </div>
            <span className="tag">{numero(evolucao?.variacaoImc, 2)}</span>
          </div>
          <GraficoLinha
            pontos={serie.map((p) => ({ data: p.data, valor: p.imc }))}
            unidade="imc"
            cor="var(--info)"
            altura={165}
          />
        </section>
      </div>

      <form className="cartao" onSubmit={salvar}>
        <div className="cartao-cabeca">
          <div>
            <span className="olho">Planejamento</span>
            <h3>Definir nova meta</h3>
          </div>
          <Selo tom="neutro">Cada alteração fica no histórico</Selo>
        </div>

        <div className="linha-campos">
          <div className="campo">
            <label htmlFor="kcal">Calorias (kcal)</label>
            <input id="kcal" type="number" min="1" max="10000" value={form.caloriasAlvo} onChange={atualizar('caloriasAlvo')} required />
            {usuario?.gastoTotal > 0 && (
              <button
                type="button"
                className="campo-dica campo-dica-acao"
                onClick={() => setForm((f) => ({ ...f, caloriasAlvo: Math.round(usuario.gastoTotal) }))}
              >
                Usar meu gasto total: {numero(usuario.gastoTotal)} kcal
              </button>
            )}
          </div>
          <div className="campo">
            <label htmlFor="prot">Proteínas (g)</label>
            <input id="prot" type="number" min="0" max="1000" value={form.proteinasAlvo} onChange={atualizar('proteinasAlvo')} />
          </div>
        </div>

        <div className="linha-campos">
          <div className="campo">
            <label htmlFor="agua">Água (ml)</label>
            <input id="agua" type="number" min="0" max="10000" value={form.aguaAlvoMl} onChange={atualizar('aguaAlvoMl')} />
          </div>
          <div className="campo">
            <label htmlFor="pesoAlvo">Peso alvo (kg)</label>
            <input id="pesoAlvo" type="number" step="0.1" min="1" max="500" value={form.pesoAlvo} onChange={atualizar('pesoAlvo')} />
          </div>
        </div>

        <button className="btn" type="submit" disabled={salvando}>
          {salvando ? 'Salvando…' : 'Salvar meta'}
        </button>
      </form>

      <section className="cartao">
        <div className="cartao-cabeca">
          <div>
            <span className="olho">Rastreabilidade</span>
            <h3>Histórico de metas</h3>
          </div>
          <span className="tag">{historico.length}</span>
        </div>

        {historico.length === 0 ? (
          <div className="vazio">
            <strong>A primeira meta aparece aqui</strong>
            <p>Defina uma meta para começar o histórico.</p>
          </div>
        ) : (
          <ul className="lista lista-compacta">
            {historico.map((m) => (
              <li className="item" key={m.id}>
                <div>
                  <div className="item-titulo">{numero(m.caloriasAlvo)} kcal · {numero(m.pesoAlvo, 1)} kg</div>
                  <div className="item-meta">
                    {dataCurta(m.criadoEm)} · água {numero(m.aguaAlvoMl)} ml · proteína {numero(m.proteinasAlvo)} g
                  </div>
                </div>
                {m.ativa ? <Selo tom="ok">ativa</Selo> : <Selo>substituída</Selo>}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
