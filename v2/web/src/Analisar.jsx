import { useEffect, useRef, useState } from 'react'
import { ErroApi, api } from './api.js'
import CapturaMidia from './CapturaMidia.jsx'
import Rotulo from './Rotulo.jsx'
import { Icone } from './Icones.jsx'
import { Anel, Selo, numero } from './ui.jsx'

/* Analisar: buscar no catálogo TACO, fotografar o prato ou ler um rótulo.
   O servidor calcula a porção e a pontuação usa meta + perfil de saúde. */

const ATALHOS = ['arroz', 'feijão', 'frango', 'banana', 'ovo', 'aveia']

const ROTULO_CLASSIFICACAO = {
  adequado: { texto: 'Equilibrado', tom: 'ok' },
  atencao: { texto: 'Atenção', tom: 'aviso' },
  evitar: { texto: 'Melhor evitar', tom: 'perigo' },
}

export default function Analisar({ avisar, aoRegistrar }) {
  const [modo, setModo] = useState('buscar')
  const [consulta, setConsulta] = useState('')
  const [resultados, setResultados] = useState([])
  const [buscando, setBuscando] = useState(false)
  const [escolhido, setEscolhido] = useState(null)
  const [gramas, setGramas] = useState(100)
  const [previa, setPrevia] = useState(null)
  const [avaliacao, setAvaliacao] = useState(null)
  const [salvando, setSalvando] = useState(false)
  const [analisandoFoto, setAnalisandoFoto] = useState(false)
  const [provedor, setProvedor] = useState(null)
  const temporizador = useRef(null)

  useEffect(() => {
    api.provedor().then(setProvedor).catch(() => setProvedor(null))
  }, [])

  // Busca com atraso: só consulta quando a pessoa parou de digitar.
  useEffect(() => {
    clearTimeout(temporizador.current)
    if (consulta.trim().length < 2) {
      setResultados([])
      return
    }
    temporizador.current = setTimeout(async () => {
      setBuscando(true)
      try {
        const resposta = await api.buscarAlimentos(consulta)
        setResultados(resposta.itens ?? [])
      } catch {
        setResultados([])
      } finally {
        setBuscando(false)
      }
    }, 280)
    return () => clearTimeout(temporizador.current)
  }, [consulta])

  // Prévia nutricional da porção + avaliação contextual no servidor.
  useEffect(() => {
    if (!escolhido) {
      setPrevia(null)
      setAvaliacao(null)
      return
    }

    let cancelado = false
    const temporizadorAvaliacao = setTimeout(async () => {
      try {
        const resposta = await api.previa(escolhido.nome, gramas)
        if (cancelado) return
        setPrevia(resposta)

        const porcao = resposta?.porcao
        if (porcao) {
          const nota = await api.avaliar({
            kcal: porcao.kcal,
            proteinas: porcao.proteina,
            carbos: porcao.carboidrato,
            gorduras: porcao.gordura,
            fibra: porcao.fibra,
          })
          if (!cancelado) setAvaliacao(nota)
        }
      } catch {
        if (!cancelado) {
          setPrevia(null)
          setAvaliacao(null)
        }
      }
    }, 260)

    return () => {
      cancelado = true
      clearTimeout(temporizadorAvaliacao)
    }
  }, [escolhido, gramas])

  async function analisarBase64(base64) {
    setAnalisandoFoto(true)
    try {
      const resultado = await api.analisarImagem(base64, gramas)
      const nome = resultado?.alimento?.nome
      if (!nome) {
        avisar('Não foi possível identificar o alimento na imagem.', 'erro')
        return
      }
      setEscolhido({ nome, grupo: resultado.alimento.grupo ?? 'Foto', por100g: resultado.porcao })
      avisar(`Reconhecido: ${nome}`)
    } catch (falha) {
      const motivo = falha?.corpo?.motivo
      const desligado = motivo === 'provedor_de_visao_indisponivel' || motivo === 'provedor_de_visao_ausente'
      avisar(
        desligado
          ? 'Reconhecimento por imagem desligado no servidor. Use a busca por nome ou o rótulo.'
          : falha instanceof ErroApi
            ? falha.message
            : 'Falha ao analisar a imagem.',
        'erro',
      )
    } finally {
      setAnalisandoFoto(false)
    }
  }

  async function analisarArquivo(arquivo) {
    const base64 = await new Promise((resolver, rejeitar) => {
      const leitor = new FileReader()
      leitor.onload = () => resolver(String(leitor.result).split(',')[1])
      leitor.onerror = rejeitar
      leitor.readAsDataURL(arquivo)
    })
    await analisarBase64(base64)
  }

  async function registrar() {
    if (!escolhido) return
    setSalvando(true)
    try {
      await api.registrarAnalise(escolhido.nome, gramas)
      avisar(`${escolhido.nome} registrado no diário`)
      setEscolhido(null)
      setConsulta('')
      setResultados([])
      aoRegistrar()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao registrar.', 'erro')
    } finally {
      setSalvando(false)
    }
  }

  const abas = [
    ['buscar', 'Buscar', 'analisar'],
    ['foto', 'Foto', 'foto'],
    ['rotulo', 'Rótulo', 'rotulo'],
  ]

  const classificacao = avaliacao ? ROTULO_CLASSIFICACAO[avaliacao.classificacao] ?? ROTULO_CLASSIFICACAO.atencao : null

  return (
    <div className="pagina pagina-estreita">
      <div className="segmentos" role="tablist" aria-label="Modo de análise">
        {abas.map(([id, rotulo, icone]) => (
          <button
            key={id}
            role="tab"
            aria-selected={modo === id}
            className={`segmento${modo === id ? ' ativo' : ''}`}
            onClick={() => setModo(id)}
          >
            <Icone nome={icone} tamanho={17} />
            {rotulo}
          </button>
        ))}
      </div>

      {modo === 'buscar' && (
        <>
          <div className="busca">
            <span className="busca-icone"><Icone nome="analisar" tamanho={18} /></span>
            <input
              type="search"
              placeholder="Buscar alimento: arroz, feijão, frango…"
              value={consulta}
              onChange={(e) => {
                setConsulta(e.target.value)
                setEscolhido(null)
              }}
              aria-label="Buscar alimento"
            />
            {resultados.length > 0 && !escolhido && (
              <div className="sugestoes" role="listbox">
                {resultados.map((item) => (
                  <button key={item.nome} className="sugestao" role="option" onClick={() => setEscolhido(item)}>
                    <span>
                      <strong>{item.nome}</strong>
                      <small>{item.grupo}</small>
                    </span>
                    <span className="kcal">{numero(item.por100g?.kcal)} kcal/100 g</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {buscando && <p className="rodape-cartao">Buscando no catálogo…</p>}

          {!consulta && !escolhido && (
            <div className="cartao cartao-inline">
              <span className="olho">Atalhos</span>
              <div className="acoes">
                {ATALHOS.map((a) => (
                  <button key={a} className="chip" onClick={() => setConsulta(a)}>{a}</button>
                ))}
              </div>
            </div>
          )}
        </>
      )}

      {modo === 'foto' && (
        <div className="cartao" style={{ marginBottom: 14 }}>
          <div className="cartao-cabeca">
            <div>
              <span className="olho">Reconhecimento</span>
              <h3>Foto do prato</h3>
            </div>
            <Selo tom={provedor?.disponivel ? 'ok' : 'neutro'} icone={provedor?.disponivel ? 'check' : 'info'}>
              {provedor?.disponivel ? 'ativo' : 'busca por nome'}
            </Selo>
          </div>
          <p className="rodape-cartao" style={{ marginTop: 0 }}>
            O servidor identifica o alimento e o catálogo calcula os nutrientes da porção.
          </p>
          {provedor && !provedor.disponivel && (
            <div className="callout" style={{ marginBottom: 14 }}>
              <Icone nome="info" tamanho={16} />
              <span>Reconhecimento por imagem desligado no servidor. A busca por nome e o rótulo continuam funcionando.</span>
            </div>
          )}
          <CapturaMidia
            titulo="Fotografar o prato"
            descricao="Vista de cima, com o prato inteiro no enquadramento."
            rotuloArquivo="Escolher foto"
            aoArquivo={analisarArquivo}
            aoBase64={analisarBase64}
            avisar={avisar}
            processando={analisandoFoto}
            textoProcessando="Analisando a imagem…"
          />
        </div>
      )}

      {modo === 'rotulo' && <Rotulo avisar={avisar} aoRegistrar={aoRegistrar} />}

      {modo !== 'rotulo' && (escolhido ? (
        <div className="cartao cartao-resultado">
          <div className="cartao-cabeca">
            <div>
              <span className="olho">{escolhido.grupo}</span>
              <h3>{escolhido.nome}</h3>
            </div>
            <span className="etiqueta-kcal">
              <strong>{numero(escolhido.por100g?.kcal)}</strong> kcal/100 g
            </span>
          </div>

          <div className="campo">
            <label htmlFor="gramas">Porção</label>
            <div className="porcao">
              <button className="botao-icone" onClick={() => setGramas((g) => Math.max(1, g - 10))} aria-label="Diminuir 10 g">
                <Icone nome="menos" tamanho={16} />
              </button>
              <input
                id="gramas"
                type="number"
                min="1"
                max="3000"
                value={gramas}
                onChange={(e) => setGramas(Number(e.target.value) || 1)}
              />
              <span className="porcao-unidade">g</span>
              <button className="botao-icone" onClick={() => setGramas((g) => Math.min(3000, g + 10))} aria-label="Aumentar 10 g">
                <Icone nome="mais" tamanho={16} />
              </button>
            </div>
            <div className="acoes" style={{ marginTop: 10 }}>
              {[50, 100, 150, 200].map((porcao) => (
                <button
                  key={porcao}
                  className={`chip${gramas === porcao ? ' chip-ativo' : ''}`}
                  onClick={() => setGramas(porcao)}
                >
                  {porcao} g
                </button>
              ))}
            </div>
          </div>

          {previa?.porcao && (
            <div className="nutrientes">
              <div className="nutriente nutriente-kcal">
                <strong>{numero(previa.porcao.kcal)}</strong>
                <span>kcal</span>
              </div>
              <div className="nutriente">
                <strong>{numero(previa.porcao.proteina, 1)}</strong>
                <span>proteína (g)</span>
              </div>
              <div className="nutriente">
                <strong>{numero(previa.porcao.carboidrato, 1)}</strong>
                <span>carboidrato (g)</span>
              </div>
              <div className="nutriente">
                <strong>{numero(previa.porcao.gordura, 1)}</strong>
                <span>gordura (g)</span>
              </div>
              <div className="nutriente">
                <strong>{numero(previa.porcao.fibra, 1)}</strong>
                <span>fibra (g)</span>
              </div>
            </div>
          )}

          {avaliacao && (
            <div className={`avaliacao avaliacao-${avaliacao.classificacao}`}>
              <Anel pontuacao={avaliacao.pontuacao} tamanho={52} />
              <div className="avaliacao-texto">
                <div className="avaliacao-topo">
                  <strong>{avaliacao.mensagem}</strong>
                  <Selo tom={classificacao.tom} icone={avaliacao.podeConsumir ? 'check' : 'info'}>
                    {classificacao.texto}
                  </Selo>
                </div>
                <p className="rodape-cartao" style={{ marginTop: 2 }}>
                  {avaliacao.dicas?.[0] ?? `Meta de ${numero(avaliacao.metaKcal)} kcal · ${numero(avaliacao.consumidoKcal)} consumidas hoje`}
                </p>
                {avaliacao.dicas?.length > 1 && (
                  <ul className="avaliacao-dicas">
                    {avaliacao.dicas.slice(1, 3).map((dica) => (
                      <li key={dica}>{dica}</li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          )}

          <div className="acoes" style={{ marginTop: 18 }}>
            <button className="btn" onClick={registrar} disabled={salvando}>
              {salvando ? 'Registrando…' : 'Registrar no diário'}
            </button>
            <button className="btn btn-fantasma" onClick={() => setEscolhido(null)}>Cancelar</button>
          </div>
        </div>
      ) : (
        <div className="vazio">
          <span className="vazio-icone"><Icone nome="comida" tamanho={22} /></span>
          <strong>O que você comeu?</strong>
          <p>Busque pelo nome, envie uma foto ou leia o rótulo de um produto.</p>
        </div>
      ))}
    </div>
  )
}
