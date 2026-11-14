import { useCallback, useEffect, useState } from 'react'
import { ErroApi, api } from './api.js'
import { GraficoBarras, GraficoLinha } from './Graficos.jsx'
import { Icone } from './Icones.jsx'
import {
  Barra,
  Donut,
  Selo,
  dataCurta,
  hojePorExtenso,
  numero,
  primeiroNome,
  saudacao,
} from './ui.jsx'

/* Painel: o resumo do dia em uma leitura só — calorias, macros, água,
   composição corporal e evolução. */

export default function Painel({ usuario, irPara, avisar, aoAtualizar }) {
  const [dados, setDados] = useState(null)
  const [carregando, setCarregando] = useState(true)
  const [salvandoPeso, setSalvandoPeso] = useState(false)

  const carregar = useCallback(async () => {
    try {
      const [perfil, metas, resumo, agua, semanaAgua, evolucao] = await Promise.all([
        api.perfil(),
        api.metas(),
        api.resumo(),
        api.aguaHoje(),
        api.aguaSemanal(),
        api.evolucao(),
      ])
      setDados({ perfil, metas, resumo, agua, semanaAgua, evolucao })
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao carregar o painel.', 'erro')
    } finally {
      setCarregando(false)
    }
  }, [avisar])

  useEffect(() => {
    carregar()
  }, [carregar])

  async function adicionarAgua(ml) {
    try {
      const resultado = await api.registrarAgua(ml)
      setDados((atual) =>
        atual
          ? {
              ...atual,
              agua: { ...atual.agua, totalMl: resultado.totalMl, percentual: resultado.percentual },
            }
          : atual,
      )
      avisar(`+${numero(ml)} ml registrados`)
      api.aguaSemanal().then((semanaAgua) => setDados((atual) => (atual ? { ...atual, semanaAgua } : atual))).catch(() => {})
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao registrar água.', 'erro')
    }
  }

  async function desfazerAgua() {
    try {
      await api.removerUltimaAgua()
      // O DELETE responde 204 sem corpo: recarregar é a fonte da verdade.
      const [agua, semanaAgua] = await Promise.all([api.aguaHoje(), api.aguaSemanal()])
      setDados((atual) => (atual ? { ...atual, agua, semanaAgua } : atual))
      avisar('Último registro removido')
    } catch {
      avisar('Não há registro para remover.', 'erro')
    }
  }

  async function atualizarPeso(evento) {
    evento.preventDefault()
    const valor = Number(evento.target.pesoNovo.value)
    if (!(valor > 0)) return

    setSalvandoPeso(true)
    try {
      await api.atualizarMedidas(valor, dados?.perfil?.altura || 1.7)
      avisar('Peso atualizado e série recalculada')
      await carregar()
      aoAtualizar?.()
      evento.target.reset()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Não foi possível atualizar o peso.', 'erro')
    } finally {
      setSalvandoPeso(false)
    }
  }

  const percCalorias = dados ? Math.round((dados.resumo.calorias / (dados.metas.caloriasAlvo || 1)) * 100) : 0
  const percAgua = dados ? Math.round((dados.agua.totalMl / (dados.agua.metaMl || 1)) * 100) : 0
  const restante = Math.max(0, (dados?.metas?.caloriasAlvo ?? 0) - (dados?.resumo?.calorias ?? 0))
  const variacao = dados?.evolucao?.variacaoPeso ?? 0
  const distancia = dados?.evolucao?.distanciaDaMeta ?? 0

  const macros = (() => {
    const p = dados?.resumo?.proteinas ?? 0
    const c = dados?.resumo?.carbos ?? 0
    const g = dados?.resumo?.gorduras ?? 0
    const kcalP = p * 4
    const kcalC = c * 4
    const kcalG = g * 9
    const total = kcalP + kcalC + kcalG
    const pct = (v) => (total > 0 ? Math.round((v / total) * 100) : 0)
    return { p, c, g, pctP: pct(kcalP), pctC: pct(kcalC), pctG: pct(kcalG), total }
  })()

  const semana = dados?.semanaAgua?.serie ?? []

  if (carregando && !dados) {
    return (
      <div className="pagina">
        <div className="cartao cartao-esqueleto">
          <div className="esqueleto" style={{ width: '30%', height: 26 }} />
          <div className="esqueleto" style={{ width: '55%' }} />
          <div className="esqueleto" style={{ width: '40%' }} />
        </div>
        <div className="grade grade-3">
          <div className="cartao"><div className="esqueleto" /><div className="esqueleto" style={{ width: '60%' }} /></div>
          <div className="cartao"><div className="esqueleto" /><div className="esqueleto" style={{ width: '60%' }} /></div>
          <div className="cartao"><div className="esqueleto" /><div className="esqueleto" style={{ width: '60%' }} /></div>
        </div>
      </div>
    )
  }

  const saudeCompleta = dados?.perfil?.saudeCompleta
    ?? (dados?.perfil?.diabetes !== 'Não informado' || Boolean(dados?.perfil?.intolerancia))

  return (
    <div className="pagina">
      {!saudeCompleta && (
        <div className="cartao cartao-aviso">
          <span className="cartao-aviso-icone"><Icone nome="coracao" tamanho={18} /></span>
          <div>
            <strong>Complete seu perfil de saúde</strong>
            <p>Informe diabetes e intolerâncias para o feedback considerar o seu caso — como na v1.</p>
          </div>
          <button className="btn btn-mini" onClick={() => irPara('perfil')}>Completar</button>
        </div>
      )}

      <section className="cartao cartao-dia">
        <div className="cartao-dia-texto">
          <span className="olho">{hojePorExtenso()}</span>
          <h2>{saudacao()}{usuario?.nome ? `, ${primeiroNome(usuario.nome)}` : ''}</h2>
          <p>
            {percCalorias === 0
              ? 'Nada registrado hoje. Comece pela primeira refeição.'
              : percCalorias > 100
                ? 'Você passou da meta de calorias. O diário mostra onde ajustar.'
                : `Você já usou ${percCalorias}% da meta de hoje.`}
          </p>
          <div className="acoes">
            <button className="btn" onClick={() => irPara('analisar')}>
              <Icone nome="mais" tamanho={16} /> Registrar alimento
            </button>
            <button className="btn btn-fantasma" onClick={() => irPara('diario')}>
              <Icone nome="diario" tamanho={16} /> Ver diário
            </button>
          </div>
        </div>

        <div className="cartao-dia-numeros">
          <Donut
            percentual={percCalorias}
            valor={numero(dados?.resumo?.calorias)}
            unidade="kcal"
            rotulo={`de ${numero(dados?.metas?.caloriasAlvo)}`}
          />
          <div className="cartao-dia-restante">
            <strong>{numero(restante)}</strong>
            <span>kcal restantes</span>
            <small>{dados?.resumo?.analises ?? 0} registro(s) hoje</small>
          </div>
        </div>
      </section>

      <section className="cartao cartao-macros">
        <div className="cartao-cabeca">
          <div>
            <span className="olho">Composição do dia</span>
            <h3>Macronutrientes</h3>
          </div>
          <Selo tom="ok">{macros.total > 0 ? `${numero(macros.total)} kcal em macros` : 'Sem registros'}</Selo>
        </div>

        <div className="distribuicao" role="img" aria-label="Distribuição de calorias entre proteína, carboidrato e gordura">
          <span className="distribuicao-p" style={{ width: `${macros.pctP}%` }} />
          <span className="distribuicao-c" style={{ width: `${macros.pctC}%` }} />
          <span className="distribuicao-g" style={{ width: `${macros.pctG}%` }} />
        </div>

        <div className="macros-lista">
          <div className="macro">
            <span className="macro-cor macro-cor-p" />
            <div>
              <strong>{numero(macros.p, 1)} g</strong>
              <small>Proteína · {macros.pctP}%</small>
            </div>
          </div>
          <div className="macro">
            <span className="macro-cor macro-cor-c" />
            <div>
              <strong>{numero(macros.c, 1)} g</strong>
              <small>Carboidrato · {macros.pctC}%</small>
            </div>
          </div>
          <div className="macro">
            <span className="macro-cor macro-cor-g" />
            <div>
              <strong>{numero(macros.g, 1)} g</strong>
              <small>Gordura · {macros.pctG}%</small>
            </div>
          </div>
        </div>

        <div className="meta-proteina">
          <div className="meta-proteina-topo">
            <span>Proteína vs. meta</span>
            <strong>{numero(macros.p, 1)} / {numero(dados?.metas?.proteinasAlvo)} g</strong>
          </div>
          <Barra percentual={(macros.p / (dados?.metas?.proteinasAlvo || 1)) * 100} />
        </div>
      </section>

      <div className="grade grade-painel">
        <section className="cartao cartao-agua">
          <div className="cartao-cabeca">
            <div>
              <span className="olho">Hidratação</span>
              <h3>Água de hoje</h3>
            </div>
            <span className="agua-icone"><Icone nome="agua" tamanho={18} /></span>
          </div>

          <div className="valor-simples">
            {numero(dados?.agua?.totalMl)}<small>/ {numero(dados?.agua?.metaMl)} ml</small>
          </div>
          <Barra percentual={percAgua} alerta={percAgua > 100} />

          <div className="acoes" style={{ marginTop: 14 }}>
            <button className="btn btn-fantasma btn-mini" onClick={() => adicionarAgua(250)}>+ 250 ml</button>
            <button className="btn btn-fantasma btn-mini" onClick={() => adicionarAgua(500)}>+ 500 ml</button>
            <button className="btn btn-fantasma btn-mini" onClick={desfazerAgua}>Desfazer</button>
          </div>

          <form
            className="campo-agua"
            onSubmit={(e) => {
              e.preventDefault()
              const valor = Number(e.target.ml.value)
              if (valor > 0) adicionarAgua(valor)
              e.target.reset()
            }}
          >
            <input
              name="ml"
              type="number"
              min="1"
              max="3000"
              placeholder="Outro valor (ml)"
              aria-label="Quantidade em mililitros"
            />
            <button className="btn btn-mini" type="submit">Registrar</button>
          </form>

          {semana.length > 0 && (
            <div className="agua-semana">
              <GraficoBarras
                barras={semana.map((d) => ({
                  rotulo: new Date(d.data).toLocaleDateString('pt-BR', { weekday: 'narrow' }),
                  valor: d.totalMl ?? 0,
                }))}
                meta={dados?.agua?.metaMl ?? 0}
                altura={84}
              />
              <p className="rodape-cartao">
                Média de {numero(dados?.semanaAgua?.mediaMl)} ml · {dados?.semanaAgua?.diasComMeta ?? 0} dia(s) na meta
              </p>
            </div>
          )}
        </section>

        <section className="cartao cartao-numeros">
          <div className="cartao-cabeca">
            <div>
              <span className="olho">Corpo</span>
              <h3>Seus números</h3>
            </div>
          </div>

          <div className="numero-linha">
            <div className="numero-item">
              <span className="numero-icone"><Icone nome="balanca" tamanho={16} /></span>
              <div>
                <small>IMC</small>
                <strong>{numero(dados?.perfil?.imc, 1)}</strong>
                <span>{dados?.perfil?.classificacaoImc ?? '—'}</span>
              </div>
            </div>
            <div className="numero-item">
              <span className="numero-icone"><Icone nome="fogo" tamanho={16} /></span>
              <div>
                <small>Taxa metabólica</small>
                <strong>{numero(dados?.perfil?.tmb)}</strong>
                <span>kcal em repouso</span>
              </div>
            </div>
            <div className="numero-item">
              <span className="numero-icone"><Icone nome="raio" tamanho={16} /></span>
              <div>
                <small>Gasto total</small>
                <strong>{numero(dados?.perfil?.gastoTotal)}</strong>
                <span>kcal por dia</span>
              </div>
            </div>
          </div>

          <div className="peso-bloco">
            <div className="peso-topo">
              <div>
                <span className="olho">Peso atual</span>
                <strong>{numero(dados?.perfil?.peso, 1)} kg</strong>
              </div>
              <Selo tom={variacao > 0 ? 'aviso' : 'ok'}>
                {variacao > 0 ? '+' : ''}{numero(variacao, 1)} kg
              </Selo>
            </div>
            <p className="rodape-cartao">
              Meta {numero(dados?.metas?.pesoAlvo, 1)} kg
              {distancia !== 0 && ` · ${distancia > 0 ? 'faltam' : 'passou'} ${numero(Math.abs(distancia), 1)} kg`}
            </p>
            <form className="campo-agua" onSubmit={atualizarPeso}>
              <input
                name="pesoNovo"
                type="number"
                step="0.1"
                min="1"
                max="400"
                placeholder="Novo peso (kg)"
                aria-label="Novo peso em quilos"
                required
              />
              <button className="btn btn-mini" type="submit" disabled={salvandoPeso}>
                {salvandoPeso ? 'Salvando…' : 'Atualizar'}
              </button>
            </form>
          </div>
        </section>
      </div>

      <section className="cartao">
        <div className="cartao-cabeca">
          <div>
            <span className="olho">Histórico</span>
            <h3>Evolução do peso</h3>
          </div>
          <button className="btn btn-fantasma btn-mini" onClick={() => irPara('metas')}>
            Ver metas <Icone nome="avancar" tamanho={14} />
          </button>
        </div>

        {(dados?.evolucao?.serie ?? []).length > 0 ? (
          <>
            <GraficoLinha
              pontos={(dados.evolucao.serie ?? []).map((p) => ({ data: p.data, valor: p.peso }))}
              unidade="peso"
              altura={170}
            />
            <ul className="lista lista-compacta">
              {(dados.evolucao.serie ?? []).slice(-4).reverse().map((ponto, indice) => (
                <li className="item" key={`${ponto.data}-${indice}`}>
                  <div>
                    <div className="item-titulo">{numero(ponto.peso, 1)} kg</div>
                    <div className="item-meta">{dataCurta(ponto.data)}</div>
                  </div>
                  <span className="etiqueta-kcal">IMC <strong>{numero(ponto.imc, 1)}</strong></span>
                </li>
              ))}
            </ul>
          </>
        ) : (
          <div className="vazio">
            <strong>Sem histórico ainda</strong>
            <p>Atualize seu peso para começar a série.</p>
          </div>
        )}
      </section>
    </div>
  )
}
