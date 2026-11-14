import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, ErroApi, sessao } from './api.js'

/* ==========================================================================
   Aplicação BSFM — React
   Telas: Entrar, Painel, Analisar, Diário, Perfil.
   Toda requisição passa por src/api.js (sessão + assinatura por sessão).
   ========================================================================== */

const iniciais = (nome = '') =>
  nome
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? '')
    .join('') || '?'

const numero = (valor, casas = 0) =>
  (valor ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas })

/* --------------------------------------------------------------- Entrar --- */

function Entrar({ aoEntrar, aoCriarConta }) {
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState('')
  const [carregando, setCarregando] = useState(false)

  async function enviar(evento) {
    evento.preventDefault()
    setErro('')
    setCarregando(true)

    try {
      const dados = await api.login(email.trim(), senha)
      sessao.definir(dados)
      aoEntrar(dados.usuario)
    } catch (falha) {
      setErro(falha instanceof ErroApi ? falha.message : 'Não foi possível entrar. Verifique a conexão.')
    } finally {
      setCarregando(false)
    }
  }

  return (
    <div className="login">
      <form className="login-card" onSubmit={enviar}>
        <div className="marca">
          <div className="marca-simbolo">BS</div>
          <div>
            <div className="marca-nome">BSFM</div>
            <div className="marca-tag">Métricas nutricionais</div>
          </div>
        </div>

        <h1>Entrar</h1>
        <p className="sub">Acompanhe calorias, água e evolução em um só lugar.</p>

        {erro && <div className="erro" role="alert">{erro}</div>}

        <div className="campo">
          <label htmlFor="email">E-mail</label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>

        <div className="campo">
          <label htmlFor="senha">Senha</label>
          <input
            id="senha"
            type="password"
            autoComplete="current-password"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            required
          />
        </div>

        <button className="btn btn-bloco" type="submit" disabled={carregando}>
          {carregando ? 'Entrando…' : 'Entrar'}
        </button>

        <button
          type="button"
          className="btn btn-fantasma btn-bloco"
          style={{ marginTop: 10 }}
          onClick={aoCriarConta}
        >
          Criar conta
        </button>
      </form>
    </div>
  )
}

/* --------------------------------------------------------------- Cadastro --- */

function Cadastro({ aoEntrar, aoVoltar }) {
  const [form, setForm] = useState({
    nome: '',
    email: '',
    senha: '',
    peso: '',
    altura: '',
    sexo: 'Feminino',
    nivelAtividade: 'Sedentario',
    aceitouTermos: false,
  })
  const [erro, setErro] = useState('')
  const [carregando, setCarregando] = useState(false)
  const [codigo, setCodigo] = useState('')
  const [etapa, setEtapa] = useState('dados')
  const [aviso, setAviso] = useState('')

  const atualizar = (campo) => (evento) => {
    const valor = campo === 'aceitouTermos' ? evento.target.checked : evento.target.value
    setForm((atual) => ({ ...atual, [campo]: valor }))
  }

  async function criar(evento) {
    evento.preventDefault()
    setErro('')
    setCarregando(true)

    try {
      await api.cadastrar({
        nome: form.nome.trim(),
        email: form.email.trim(),
        senha: form.senha,
        peso: Number(form.peso) || null,
        altura: Number(form.altura) || null,
        sexo: form.sexo,
        nivelAtividade: form.nivelAtividade,
        aceitouTermos: form.aceitouTermos,
        versaoTermos: '1.0',
      })

      setEtapa('verificar')
      setAviso('Conta criada. Enviamos um código de 6 dígitos para o seu e-mail.')
    } catch (falha) {
      const mensagem =
        falha?.corpo?.erro === 'email_ja_cadastrado'
          ? 'Este e-mail já tem conta. Tente entrar.'
          : falha instanceof ErroApi
            ? falha.message
            : 'Não foi possível criar a conta.'
      setErro(mensagem)
    } finally {
      setCarregando(false)
    }
  }

  async function verificar(evento) {
    evento.preventDefault()
    setErro('')
    setCarregando(true)

    try {
      await api.verificarEmail(form.email.trim(), codigo.trim())
      const dados = await api.login(form.email.trim(), form.senha)
      sessao.definir(dados)
      aoEntrar(dados.usuario)
    } catch (falha) {
      setErro(
        falha?.corpo?.erro === 'codigo_invalido'
          ? 'Código inválido ou expirado. Confira o e-mail.'
          : falha instanceof ErroApi
            ? falha.message
            : 'Não foi possível verificar.',
      )
    } finally {
      setCarregando(false)
    }
  }

  return (
    <div className="login">
      <form className="login-card" onSubmit={etapa === 'dados' ? criar : verificar}>
        <div className="marca">
          <div className="marca-simbolo">BS</div>
          <div>
            <div className="marca-nome">BSFM</div>
            <div className="marca-tag">Métricas nutricionais</div>
          </div>
        </div>

        <h1>{etapa === 'dados' ? 'Criar conta' : 'Verificar e-mail'}</h1>
        <p className="sub">
          {etapa === 'dados'
            ? 'Leva menos de um minuto. O IMC e o gasto calórico são calculados no servidor.'
            : aviso}
        </p>

        {erro && <div className="erro" role="alert">{erro}</div>}
        {etapa === 'verificar' && !erro && <div className="sucesso">{aviso}</div>}

        {etapa === 'dados' ? (
          <>
            <div className="campo">
              <label htmlFor="nome">Nome completo</label>
              <input id="nome" value={form.nome} onChange={atualizar('nome')} required minLength={3} />
            </div>

            <div className="campo">
              <label htmlFor="email-cadastro">E-mail</label>
              <input id="email-cadastro" type="email" value={form.email} onChange={atualizar('email')} required />
            </div>

            <div className="campo">
              <label htmlFor="senha-cadastro">Senha (mínimo 8 caracteres)</label>
              <input
                id="senha-cadastro"
                type="password"
                value={form.senha}
                onChange={atualizar('senha')}
                required
                minLength={8}
                autoComplete="new-password"
              />
            </div>

            <div style={{ display: 'flex', gap: 10 }}>
              <div className="campo" style={{ flex: 1 }}>
                <label htmlFor="peso">Peso (kg)</label>
                <input id="peso" type="number" step="0.1" value={form.peso} onChange={atualizar('peso')} />
              </div>
              <div className="campo" style={{ flex: 1 }}>
                <label htmlFor="altura">Altura (m)</label>
                <input id="altura" type="number" step="0.01" value={form.altura} onChange={atualizar('altura')} />
              </div>
            </div>

            <div className="campo">
              <label htmlFor="sexo">Sexo</label>
              <select id="sexo" value={form.sexo} onChange={atualizar('sexo')}>
                <option>Feminino</option>
                <option>Masculino</option>
              </select>
            </div>

            <div className="campo">
              <label htmlFor="atividade">Nível de atividade</label>
              <select id="atividade" value={form.nivelAtividade} onChange={atualizar('nivelAtividade')}>
                <option value="Sedentario">Sedentário</option>
                <option value="Ativo">Ativo</option>
                <option value="Muito Ativo">Muito ativo</option>
              </select>
            </div>

            <label
              style={{ display: 'flex', gap: 8, alignItems: 'flex-start', fontSize: 13, color: 'var(--text-muted)', marginBottom: 16 }}
            >
              <input
                type="checkbox"
                checked={form.aceitouTermos}
                onChange={atualizar('aceitouTermos')}
                style={{ marginTop: 3 }}
              />
              Autorizo o tratamento dos meus dados de saúde para cálculo nutricional (LGPD).
            </label>

            <button className="btn btn-bloco" type="submit" disabled={carregando || !form.aceitouTermos}>
              {carregando ? 'Criando…' : 'Criar conta'}
            </button>
          </>
        ) : (
          <>
            <div className="campo">
              <label htmlFor="codigo">Código de 6 dígitos</label>
              <input
                id="codigo"
                inputMode="numeric"
                maxLength={6}
                value={codigo}
                onChange={(e) => setCodigo(e.target.value.replace(/\D/g, ''))}
                required
                autoFocus
              />
            </div>

            <button className="btn btn-bloco" type="submit" disabled={carregando || codigo.length !== 6}>
              {carregando ? 'Verificando…' : 'Verificar e entrar'}
            </button>
          </>
        )}

        <button type="button" className="btn btn-fantasma btn-bloco" style={{ marginTop: 10 }} onClick={aoVoltar}>
          Já tenho conta
        </button>
      </form>
    </div>
  )
}

/* ------------------------------------------------------------------ Card --- */

function Indicador({ titulo, valor, unidade, rodape, percentual, excedido = false, carregando }) {
  return (
    <div className="card">
      <div className="card-titulo">{titulo}</div>
      {carregando ? (
        <>
          <div className="esqueleto" style={{ width: '60%', height: 28 }} />
          <div className="esqueleto" style={{ width: '40%' }} />
        </>
      ) : (
        <>
          <div className="card-valor">
            {valor}
            {unidade && <small>{unidade}</small>}
          </div>
          {typeof percentual === 'number' && (
            <div className={`barra${excedido ? ' excedida' : ''}`}>
              <span style={{ width: `${Math.min(100, Math.max(0, percentual))}%` }} />
            </div>
          )}
          {rodape && <div className="card-rodape">{rodape}</div>}
        </>
      )}
    </div>
  )
}

/* ---------------------------------------------------------------- Painel --- */

function Painel({ aoIrPara, avisar }) {
  const [dados, setDados] = useState(null)
  const [carregando, setCarregando] = useState(true)

  const carregar = useCallback(async () => {
    setCarregando(true)
    try {
      const [perfil, metas, resumo, agua, evolucao] = await Promise.all([
        api.perfil(),
        api.metas(),
        api.resumo(),
        api.aguaHoje(),
        api.evolucao(),
      ])
      setDados({ perfil, metas, resumo, agua, evolucao })
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
      setDados((atual) => (atual ? { ...atual, agua: { ...atual.agua, totalMl: resultado.totalMl } } : atual))
      avisar(`+${ml} ml registrados`, 'ok')
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao registrar água.', 'erro')
    }
  }

  const percentualCalorias = dados ? Math.round((dados.resumo.calorias / (dados.metas.caloriasAlvo || 1)) * 100) : 0
  const percentualAgua = dados ? Math.round((dados.agua.totalMl / (dados.agua.metaMl || 1)) * 100) : 0
  const variacao = dados?.evolucao?.variacaoPeso ?? 0

  return (
    <div className="pagina">
      <div className="grade grade-4" style={{ marginBottom: 16 }}>
        <Indicador
          titulo="Calorias hoje"
          valor={numero(dados?.resumo?.calorias)}
          unidade={`/ ${numero(dados?.metas?.caloriasAlvo)} kcal`}
          percentual={percentualCalorias}
          excedido={percentualCalorias > 100}
          rodape={`${dados?.resumo?.analises ?? 0} registro(s) no dia`}
          carregando={carregando}
        />
        <Indicador
          titulo="Água"
          valor={numero(dados?.agua?.totalMl)}
          unidade={`/ ${numero(dados?.agua?.metaMl)} ml`}
          percentual={percentualAgua}
          rodape={
            <span style={{ display: 'inline-flex', gap: 8 }}>
              <button className="btn btn-fantasma btn-mini" onClick={() => adicionarAgua(250)}>+250</button>
              <button className="btn btn-fantasma btn-mini" onClick={() => adicionarAgua(500)}>+500</button>
            </span>
          }
          carregando={carregando}
        />
        <Indicador
          titulo="IMC"
          valor={numero(dados?.perfil?.imc, 1)}
          rodape={dados?.perfil?.classificacaoImc ?? '—'}
          carregando={carregando}
        />
        <Indicador
          titulo="Peso"
          valor={numero(dados?.perfil?.peso, 1)}
          unidade="kg"
          rodape={`meta ${numero(dados?.metas?.pesoAlvo, 1)} kg`}
          carregando={carregando}
        />
      </div>

      <div className="grade grade-2">
        <div className="card">
          <div className="card-titulo">Evolução</div>
          {carregando ? (
            <div className="esqueleto" style={{ height: 80 }} />
          ) : (
            <>
              <div className="card-valor" style={{ fontSize: 24 }}>
                {variacao > 0 ? '+' : ''}
                {numero(variacao, 1)} <small>kg desde o início</small>
              </div>
              <div className="card-rodape" style={{ marginBottom: 12 }}>
                {dados?.evolucao?.distanciaDaMeta > 0
                  ? `${numero(dados.evolucao.distanciaDaMeta, 1)} kg acima da meta`
                  : 'Meta de peso alcançada'}
              </div>
              <ul className="lista">
                {(dados?.evolucao?.serie ?? []).slice(-4).reverse().map((ponto, indice) => (
                  <li className="item" key={`${ponto.data}-${indice}`}>
                    <span className="item-meta">{new Date(ponto.data).toLocaleDateString('pt-BR')}</span>
                    <span className="item-valor">{numero(ponto.peso, 1)} kg</span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>

        <div className="card">
          <div className="card-titulo">Atalhos</div>
          <p className="card-rodape" style={{ marginTop: 0 }}>
            Registrar refeição é o que move o painel.
          </p>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 14 }}>
            <button className="btn btn-bloco" onClick={() => aoIrPara('analisar')}>
              Registrar alimento
            </button>
            <button className="btn btn-fantasma btn-bloco" onClick={() => aoIrPara('diario')}>
              Ver diário
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------- Analisar --- */

function Analisar({ avisar, aoRegistrar }) {
  const [consulta, setConsulta] = useState('')
  const [resultados, setResultados] = useState([])
  const [buscando, setBuscando] = useState(false)
  const [escolhido, setEscolhido] = useState(null)
  const [gramas, setGramas] = useState(100)
  const [previa, setPrevia] = useState(null)
  const [salvando, setSalvando] = useState(false)
  const temporizador = useRef(null)

  // Busca com debounce: uma requisição a cada 300 ms de pausa.
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
    }, 300)

    return () => clearTimeout(temporizador.current)
  }, [consulta])

  // Prévia nutricional: quem calcula é o servidor.
  useEffect(() => {
    if (!escolhido) {
      setPrevia(null)
      return
    }

    let cancelado = false
    ;(async () => {
      try {
        const resposta = await api.previa(escolhido.nome, gramas)
        if (!cancelado) setPrevia(resposta)
      } catch {
        if (!cancelado) setPrevia(null)
      }
    })()

    return () => {
      cancelado = true
    }
  }, [escolhido, gramas])

  async function registrar() {
    if (!escolhido) return
    setSalvando(true)
    try {
      await api.registrarAnalise(escolhido.nome, gramas)
      avisar(`${escolhido.nome} registrado (${gramas} g)`, 'ok')
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

  return (
    <div className="pagina" style={{ maxWidth: 760 }}>
      <div className="busca">
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
              <button
                key={item.nome}
                className="sugestao"
                role="option"
                onClick={() => setEscolhido(item)}
              >
                <span>{item.nome}</span>
                <small>{numero(item.por100g?.kcal)} kcal/100 g</small>
              </button>
            ))}
          </div>
        )}

        {buscando && <div className="card-rodape" style={{ marginTop: 8 }}>Buscando…</div>}
      </div>

      {escolhido ? (
        <div className="card">
          <div className="card-titulo">Confirmar</div>
          <div className="item" style={{ marginBottom: 16, border: 'none', background: 'transparent', padding: 0 }}>
            <div>
              <div className="item-nome">{escolhido.nome}</div>
              <div className="item-meta">{escolhido.grupo}</div>
            </div>
          </div>

          <div className="campo">
            <label htmlFor="gramas">Porção (g)</label>
            <input
              id="gramas"
              type="number"
              min="1"
              max="3000"
              value={gramas}
              onChange={(e) => setGramas(Number(e.target.value) || 1)}
            />
          </div>

          {previa?.porcao && (
            <table className="tabela" style={{ marginBottom: 16 }}>
              <thead>
                <tr>
                  <th>Nutriente</th>
                  <th style={{ textAlign: 'right' }}>Na porção</th>
                </tr>
              </thead>
              <tbody>
                <tr><td>Calorias</td><td className="num">{numero(previa.porcao.kcal)} kcal</td></tr>
                <tr><td>Proteína</td><td className="num">{numero(previa.porcao.proteina, 1)} g</td></tr>
                <tr><td>Carboidrato</td><td className="num">{numero(previa.porcao.carboidrato, 1)} g</td></tr>
                <tr><td>Gordura</td><td className="num">{numero(previa.porcao.gordura, 1)} g</td></tr>
              </tbody>
            </table>
          )}

          <div style={{ display: 'flex', gap: 10 }}>
            <button className="btn" onClick={registrar} disabled={salvando}>
              {salvando ? 'Registrando…' : 'Registrar'}
            </button>
            <button className="btn btn-fantasma" onClick={() => setEscolhido(null)}>
              Cancelar
            </button>
          </div>
        </div>
      ) : (
        <div className="vazio">
          <strong>O que você comeu?</strong>
          Digite o nome do alimento. O servidor calcula os nutrientes a partir da tabela TACO.
        </div>
      )}
    </div>
  )
}

/* ----------------------------------------------------------------- Diário --- */

function Diario({ avisar, versao }) {
  const [itens, setItens] = useState([])
  const [carregando, setCarregando] = useState(true)

  useEffect(() => {
    let ativo = true
    ;(async () => {
      setCarregando(true)
      try {
        const lista = await api.analises(50)
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

  const total = useMemo(
    () => itens.reduce((soma, item) => soma + (item.calorias ?? 0), 0),
    [itens],
  )

  return (
    <div className="pagina" style={{ maxWidth: 820 }}>
      <div className="grade grade-4" style={{ marginBottom: 16 }}>
        <Indicador titulo="Registros" valor={itens.length} carregando={carregando} />
        <Indicador titulo="Calorias no total" valor={numero(total)} unidade="kcal" carregando={carregando} />
      </div>

      {carregando ? (
        <div className="card">
          <div className="esqueleto" style={{ width: '70%' }} />
          <div className="esqueleto" style={{ width: '50%' }} />
          <div className="esqueleto" style={{ width: '60%' }} />
        </div>
      ) : itens.length === 0 ? (
        <div className="vazio">
          <strong>Nada registrado ainda</strong>
          Use a aba Analisar para adicionar o primeiro alimento.
        </div>
      ) : (
        <ul className="lista">
          {itens.map((item) => (
            <li className="item" key={item.id}>
              <div>
                <div className="item-nome">{item.alimento}</div>
                <div className="item-meta">
                  {item.porcao} · {new Date(item.dataAnalise).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}
                </div>
              </div>
              <div className="item-valor">{numero(item.calorias)} kcal</div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/* ----------------------------------------------------------------- Perfil --- */

function Perfil({ usuario, aoSair, avisar }) {
  const [sessoes, setSessoes] = useState([])

  useEffect(() => {
    api.sessoes().then(setSessoes).catch(() => setSessoes([]))
  }, [])

  async function sair() {
    try {
      await api.logout()
    } catch {
      /* sair localmente mesmo se a rede falhar */
    }
    aoSair()
  }

  return (
    <div className="pagina" style={{ maxWidth: 720 }}>
      <div className="card" style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div className="avatar" style={{ width: 48, height: 48, fontSize: 16 }}>
            {iniciais(usuario?.nome)}
          </div>
          <div>
            <div className="item-nome">{usuario?.nome}</div>
            <div className="item-meta">{usuario?.email}</div>
          </div>
          {usuario?.emailVerificado && <span className="tag tag-ok" style={{ marginLeft: 'auto' }}>e-mail verificado</span>}
        </div>

        <table className="tabela" style={{ marginTop: 18 }}>
          <tbody>
            <tr><td>Idade</td><td className="num">{usuario?.idade} anos</td></tr>
            <tr><td>Altura</td><td className="num">{numero(usuario?.altura, 2)} m</td></tr>
            <tr><td>Peso</td><td className="num">{numero(usuario?.peso, 1)} kg</td></tr>
            <tr><td>IMC</td><td className="num">{numero(usuario?.imc, 1)} — {usuario?.classificacaoImc}</td></tr>
            <tr><td>TMB</td><td className="num">{numero(usuario?.tmb)} kcal</td></tr>
            <tr><td>Gasto total</td><td className="num">{numero(usuario?.gastoTotal)} kcal</td></tr>
            <tr><td>2FA</td><td className="num">{usuario?.totpHabilitado ? 'ativo' : 'desligado'}</td></tr>
          </tbody>
        </table>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="card-titulo">Sessões</div>
        {sessoes.length === 0 ? (
          <div className="card-rodape">Nenhuma sessão listada.</div>
        ) : (
          <ul className="lista">
            {sessoes.slice(0, 5).map((s) => (
              <li className="item" key={s.id}>
                <div>
                  <div className="item-nome">
                    {s.atual ? 'Esta sessão' : 'Outro dispositivo'}
                  </div>
                  <div className="item-meta">
                    desde {new Date(s.criadaEm).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}
                  </div>
                </div>
                {s.revogadaEm ? <span className="tag">encerrada</span> : <span className="tag tag-ok">ativa</span>}
              </li>
            ))}
          </ul>
        )}
      </div>

      <button className="btn btn-perigo" onClick={sair}>Sair da conta</button>
    </div>
  )
}

/* ------------------------------------------------------------------ Shell --- */

const ABAS = [
  { id: 'painel', rotulo: 'Painel', icone: '◧' },
  { id: 'analisar', rotulo: 'Analisar', icone: '＋' },
  { id: 'diario', rotulo: 'Diário', icone: '☰' },
  { id: 'perfil', rotulo: 'Perfil', icone: '☺' },
]

function App() {
  const [usuario, setUsuario] = useState(null)
  const [modoAcesso, setModoAcesso] = useState('entrar')
  const [aba, setAba] = useState('painel')
  const [versao, setVersao] = useState(0)
  const [mensagem, setMensagem] = useState(null)

  const avisar = useCallback((texto, tipo = 'ok') => {
    setMensagem({ texto, tipo })
  }, [])

  useEffect(() => {
    if (!mensagem) return
    const t = setTimeout(() => setMensagem(null), 3200)
    return () => clearTimeout(t)
  }, [mensagem])

  useEffect(() => {
    if (!sessao.ativa) return
    api
      .perfil()
      .then(setUsuario)
      .catch(() => sessao.limpar())
  }, [])

  if (!usuario && !sessao.ativa) {
    return modoAcesso === 'cadastro' ? (
      <Cadastro aoEntrar={(dados) => setUsuario(dados)} aoVoltar={() => setModoAcesso('entrar')} />
    ) : (
      <Entrar
        aoEntrar={(dados) => setUsuario(dados)}
        aoCriarConta={() => setModoAcesso('cadastro')}
      />
    )
  }

  function sair() {
    sessao.limpar()
    setUsuario(null)
    setModoAcesso('entrar')
    setAba('painel')
  }

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="marca">
          <div className="marca-simbolo">BS</div>
          <div>
            <div className="marca-nome">BSFM</div>
            <div className="marca-tag">Nutrição</div>
          </div>
        </div>

        <nav aria-label="Navegação principal">
          {ABAS.map((item) => (
            <button
              key={item.id}
              className="nav-item"
              aria-current={aba === item.id ? 'page' : undefined}
              onClick={() => setAba(item.id)}
            >
              <span className="icone" aria-hidden="true">{item.icone}</span>
              {item.rotulo}
            </button>
          ))}
        </nav>

        <div className="sidebar-rodape">
          <div className="item-meta" style={{ padding: '8px 8px 0' }}>
            Sessão assinada por requisição
          </div>
        </div>
      </aside>

      <main className="conteudo">
        <header className="topo">
          <h2>{ABAS.find((a) => a.id === aba)?.rotulo}</h2>
          <div className="usuario">
            <span className="nome">{usuario?.nome}</span>
            <div className="avatar">{iniciais(usuario?.nome)}</div>
          </div>
        </header>

        {mensagem && (
          <div style={{ padding: '12px 28px 0' }}>
            <div className={mensagem.tipo === 'erro' ? 'erro' : 'sucesso'} role="status">
              {mensagem.texto}
            </div>
          </div>
        )}

        {aba === 'painel' && <Painel aoIrPara={setAba} avisar={avisar} />}
        {aba === 'analisar' && (
          <Analisar
            avisar={avisar}
            aoRegistrar={() => {
              setVersao((v) => v + 1)
              setAba('diario')
            }}
          />
        )}
        {aba === 'diario' && <Diario avisar={avisar} versao={versao} />}
        {aba === 'perfil' && <Perfil usuario={usuario} aoSair={sair} avisar={avisar} />}
      </main>
    </div>
  )
}

export default App
