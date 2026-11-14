import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, ErroApi, sessao } from './api.js'
import { GraficoBarras, GraficoLinha } from './Graficos.jsx'
import Landing from './Landing.jsx'
import Recuperar from './Recuperar.jsx'
import Rotulo from './Rotulo.jsx'
import Prescricao from './Prescricao.jsx'

/* ==========================================================================
   BSFM — interface
   Um único código para desktop e celular: a diferença é só o layout
   (sidebar no desktop, barra inferior no mobile). Toda ação existe nas duas.
   ========================================================================== */

/* ---------------------------------------------------------------- ícones --- */

const Traco = ({ d, tamanho = 20 }) => (
  <svg
    width={tamanho}
    height={tamanho}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.7"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
  >
    <path d={d} />
  </svg>
)

const Icones = {
  painel: (p) => <Traco {...p} d="M4 13h6V4H4v9Zm0 7h6v-4H4v4Zm10 0h6v-9h-6v9Zm0-16v4h6V4h-6Z" />,
  analisar: (p) => (
    <Traco {...p} d="M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm10 2-4.35-4.35" />
  ),
  diario: (p) => <Traco {...p} d="M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01" />,
  perfil: (p) => <Traco {...p} d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z" />,
  instalar: (p) => <Traco {...p} d="M12 3v12m0 0 4-4m-4 4-4-4M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" />,
  sol: (p) => <Traco {...p} d="M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10ZM12 1v2m0 18v2M4.2 4.2l1.4 1.4m12.8 12.8 1.4 1.4M1 12h2m18 0h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4" />,
  lua: (p) => <Traco {...p} d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z" />,
  sair: (p) => <Traco {...p} d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4m7 14 5-5-5-5m5 5H9" />,
  agua: (p) => <Traco {...p} d="M12 2.7s6 6.3 6 10.3a6 6 0 0 1-12 0c0-4 6-10.3 6-10.3Z" />,
  mais: (p) => <Traco {...p} d="M12 5v14M5 12h14" />,
  voltar: (p) => <Traco {...p} d="m15 18-6-6 6-6" />,
  foto: (p) => <Traco {...p} d="M3 8a2 2 0 0 1 2-2h2l1.5-2h7L17 6h2a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8Zm9 9a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z" />,
  rotulo: (p) => <Traco {...p} d="M4 4h16v16H4z M8 9h8M8 13h5" />,
  planos: (p) => <Traco {...p} d="M8 2v4m8-4v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z" />,
  metas: (p) => <Traco {...p} d="M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm0-5a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm0-3a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z" />,
}

/* ----------------------------------------------------------------- ajuda --- */

const iniciais = (nome = '') =>
  nome
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? '')
    .join('') || '?'

const numero = (valor, casas = 0) =>
  (valor ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas })

const dataCurta = (iso) => new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })

const dataLonga = (iso) =>
  new Date(iso).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' })

/* ------------------------------------------------------------------ tema --- */

function useTema() {
  const [tema, setTema] = useState(() => localStorage.getItem('bsfm.tema') ?? 'escuro')

  useEffect(() => {
    document.documentElement.dataset.tema = tema
    localStorage.setItem('bsfm.tema', tema)
  }, [tema])

  return [tema, () => setTema((t) => (t === 'escuro' ? 'claro' : 'escuro'))]
}

/* ---------------------------------------------------------------- entrada --- */

function Acesso({ aoEntrar, aoRecuperar, modoInicial = 'entrar', aoVoltarSite, instalar, podeInstalar, instalado }) {
  const [modo, setModo] = useState(modoInicial)
  const [erro, setErro] = useState('')
  const [carregando, setCarregando] = useState(false)
  const [codigo, setCodigo] = useState('')
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

  const atualizar = (campo) => (evento) => {
    const valor = campo === 'aceitouTermos' ? evento.target.checked : evento.target.value
    setForm((atual) => ({ ...atual, [campo]: valor }))
  }

  async function entrar(evento) {
    evento.preventDefault()
    setErro('')
    setCarregando(true)
    try {
      const dados = await api.login(form.email.trim(), form.senha)
      sessao.definir(dados)
      aoEntrar(dados.usuario)
    } catch (falha) {
      setErro(falha instanceof ErroApi ? falha.message : 'Não foi possível entrar.')
    } finally {
      setCarregando(false)
    }
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
      setModo('verificar')
    } catch (falha) {
      setErro(
        falha?.corpo?.erro === 'email_ja_cadastrado'
          ? 'Este e-mail já tem conta. Use "Entrar".'
          : falha instanceof ErroApi
            ? falha.message
            : 'Não foi possível criar a conta.',
      )
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

  const titulo = modo === 'entrar' ? 'Entrar' : modo === 'cadastro' ? 'Criar conta' : 'Verificar e-mail'
  const subtitulo =
    modo === 'entrar'
      ? 'Acompanhe calorias, água e evolução em um só lugar.'
      : modo === 'cadastro'
        ? 'Leva menos de um minuto. O IMC é calculado no servidor.'
        : `Enviamos um código de 6 dígitos para ${form.email}.`

  return (
    <div className="acesso">
      <form className="cartao-acesso" onSubmit={modo === 'entrar' ? entrar : modo === 'cadastro' ? criar : verificar}>
        <div className="marca">
          <div className="marca-simbolo">BS</div>
          <div className="marca-texto">
            <span className="marca-nome">BSFM</span>
            <span className="marca-tag">Métricas nutricionais</span>
          </div>
        </div>

        <h1>{titulo}</h1>
        <p className="subtitulo">{subtitulo}</p>

        {erro && <div className="erro" role="alert">{erro}</div>}

        {modo === 'entrar' && (
          <>
            <div className="campo">
              <label htmlFor="email">E-mail</label>
              <input id="email" type="email" autoComplete="username" value={form.email} onChange={atualizar('email')} required />
            </div>
            <div className="campo">
              <label htmlFor="senha">Senha</label>
              <input id="senha" type="password" autoComplete="current-password" value={form.senha} onChange={atualizar('senha')} required />
            </div>
            <button className="btn btn-bloco" type="submit" disabled={carregando}>
              {carregando ? 'Entrando…' : 'Entrar'}
            </button>
            <button type="button" className="link-esqueci" onClick={aoRecuperar}>
              Esqueci minha senha
            </button>
            <button type="button" className="btn btn-fantasma btn-bloco" style={{ marginTop: 10 }} onClick={() => { setErro(''); setModo('cadastro') }}>
              Criar conta
            </button>
          </>
        )}

        {modo === 'cadastro' && (
          <>
            <div className="campo">
              <label htmlFor="nome">Nome completo</label>
              <input id="nome" value={form.nome} onChange={atualizar('nome')} required minLength={3} autoComplete="name" />
            </div>
            <div className="campo">
              <label htmlFor="email-c">E-mail</label>
              <input id="email-c" type="email" value={form.email} onChange={atualizar('email')} required autoComplete="email" />
            </div>
            <div className="campo">
              <label htmlFor="senha-c">Senha (mínimo 8 caracteres)</label>
              <input id="senha-c" type="password" value={form.senha} onChange={atualizar('senha')} required minLength={8} autoComplete="new-password" />
            </div>
            <div className="linha-campos">
              <div className="campo">
                <label htmlFor="peso">Peso (kg)</label>
                <input id="peso" type="number" inputMode="decimal" step="0.1" value={form.peso} onChange={atualizar('peso')} />
              </div>
              <div className="campo">
                <label htmlFor="altura">Altura (m)</label>
                <input id="altura" type="number" inputMode="decimal" step="0.01" value={form.altura} onChange={atualizar('altura')} />
              </div>
            </div>
            <div className="linha-campos">
              <div className="campo">
                <label htmlFor="sexo">Sexo</label>
                <select id="sexo" value={form.sexo} onChange={atualizar('sexo')}>
                  <option>Feminino</option>
                  <option>Masculino</option>
                </select>
              </div>
              <div className="campo">
                <label htmlFor="ativ">Atividade</label>
                <select id="ativ" value={form.nivelAtividade} onChange={atualizar('nivelAtividade')}>
                  <option value="Sedentario">Sedentário</option>
                  <option value="Ativo">Ativo</option>
                  <option value="Muito Ativo">Muito ativo</option>
                </select>
              </div>
            </div>

            <label className="aceite">
              <input type="checkbox" checked={form.aceitouTermos} onChange={atualizar('aceitouTermos')} />
              Autorizo o tratamento dos meus dados de saúde para cálculo nutricional (LGPD).
            </label>

            <button className="btn btn-bloco" type="submit" disabled={carregando || !form.aceitouTermos}>
              {carregando ? 'Criando…' : 'Criar conta'}
            </button>
            <button type="button" className="btn btn-fantasma btn-bloco" style={{ marginTop: 10 }} onClick={() => { setErro(''); setModo('entrar') }}>
              Já tenho conta
            </button>
          </>
        )}

        <div style={{ marginTop: 18, paddingTop: 16, borderTop: '1px solid var(--borda)', textAlign: 'center' }}>
          {instalado ? (
            <span className="item-meta">Aplicativo instalado neste dispositivo</span>
          ) : (
            <button type="button" className="btn btn-fantasma btn-mini" onClick={instalar} disabled={!podeInstalar}>
              {podeInstalar ? 'Instalar aplicativo' : 'Instalar pelo menu do navegador'}
            </button>
          )}
          <div style={{ marginTop: 10 }}>
            <button type="button" className="btn btn-mini" onClick={aoVoltarSite} style={{ background: 'transparent', color: 'var(--texto-medio)' }}>
              ← Voltar ao site
            </button>
          </div>
        </div>

        {modo === 'verificar' && (
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
                style={{ letterSpacing: 8, fontSize: 22, textAlign: 'center' }}
              />
            </div>
            <button className="btn btn-bloco" type="submit" disabled={carregando || codigo.length !== 6}>
              {carregando ? 'Verificando…' : 'Verificar e entrar'}
            </button>
            <button type="button" className="btn btn-fantasma btn-bloco" style={{ marginTop: 10 }} onClick={() => { setErro(''); setModo('entrar') }}>
              Voltar
            </button>
          </>
        )}
      </form>
    </div>
  )
}

/* ------------------------------------------------------------------ donut --- */

function Donut({ percentual, valor, unidade, rotulo }) {
  const raio = 48
  const circunferencia = 2 * Math.PI * raio
  const preenchido = Math.min(100, Math.max(0, percentual)) / 100
  const excedeu = percentual > 100

  return (
    <div className="donut">
      <svg width="118" height="118" viewBox="0 0 118 118">
        <circle cx="59" cy="59" r={raio} fill="none" stroke="var(--surface-3)" strokeWidth="11" />
        <circle
          cx="59"
          cy="59"
          r={raio}
          fill="none"
          stroke={excedeu ? 'var(--aviso)' : 'var(--acento)'}
          strokeWidth="11"
          strokeLinecap="round"
          strokeDasharray={`${circunferencia * preenchido} ${circunferencia}`}
          style={{ transition: 'stroke-dasharray 0.5s ease' }}
        />
      </svg>
      <div className="donut-centro">
        <strong>{valor}</strong>
        <small>{unidade}</small>
        <small style={{ marginTop: 2 }}>{rotulo}</small>
      </div>
    </div>
  )
}

/* ----------------------------------------------------------------- painel --- */

function Painel({ irPara, avisar, aoAtualizar }) {
  const [dados, setDados] = useState(null)
  const [carregando, setCarregando] = useState(true)

  const carregar = useCallback(async () => {
    setCarregando(true)
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
      setDados((atual) => (atual ? { ...atual, agua: { ...atual.agua, totalMl: resultado.totalMl } } : atual))
      avisar(`+${ml} ml registrados`)
      aoAtualizar?.()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao registrar água.', 'erro')
    }
  }

  const percCalorias = dados ? Math.round((dados.resumo.calorias / (dados.metas.caloriasAlvo || 1)) * 100) : 0
  const percAgua = dados ? Math.round((dados.agua.totalMl / (dados.agua.metaMl || 1)) * 100) : 0
  const variacao = dados?.evolucao?.variacaoPeso ?? 0

  if (carregando && !dados) {
    return (
      <div className="pagina">
        <div className="cartao" style={{ marginBottom: 14 }}>
          <div className="esqueleto" style={{ width: '35%', height: 22 }} />
          <div className="esqueleto" style={{ width: '55%' }} />
        </div>
        <div className="grade grade-3">
          <div className="cartao"><div className="esqueleto" /><div className="esqueleto" style={{ width: '60%' }} /></div>
          <div className="cartao"><div className="esqueleto" /><div className="esqueleto" style={{ width: '60%' }} /></div>
          <div className="cartao"><div className="esqueleto" /><div className="esqueleto" style={{ width: '60%' }} /></div>
        </div>
      </div>
    )
  }

  return (
    <div className="pagina">
      {dados?.perfil?.diabetes === 'Não informado' && !dados?.perfil?.intolerancia && (
        <div className="cartao" style={{ marginBottom: 14, borderColor: 'color-mix(in srgb, var(--aviso) 45%, transparent)' }}>
          <div className="cartao-titulo">Perfil de saúde incompleto</div>
          <p className="rodape-cartao" style={{ marginTop: 0 }}>
            Informe diabetes e intolerâncias para receber recomendações adequadas.
          </p>
          <button className="btn btn-mini" onClick={() => irPara('perfil')}>Completar agora</button>
        </div>
      )}
      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="destaque">
          <Donut
            percentual={percCalorias}
            valor={numero(dados?.resumo?.calorias)}
            unidade="kcal"
            rotulo={`de ${numero(dados?.metas?.caloriasAlvo)}`}
          />
          <div className="destaque-info">
            <div className="valor">
              {numero(Math.max(0, (dados?.metas?.caloriasAlvo ?? 0) - (dados?.resumo?.calorias ?? 0)))}
              <small>kcal restantes</small>
            </div>
            <div className="nota">
              {dados?.resumo?.analises ?? 0} registro(s) hoje ·{' '}
              {percCalorias > 100 ? 'meta excedida' : `${percCalorias}% da meta`}
            </div>
            <div className="acoes" style={{ marginTop: 14 }}>
              <button className="btn btn-mini" onClick={() => irPara('analisar')}>Registrar alimento</button>
              <button className="btn btn-fantasma btn-mini" onClick={() => irPara('diario')}>Ver diário</button>
            </div>
          </div>
        </div>
      </div>

      <div className="grade grade-3" style={{ marginBottom: 14 }}>
        <div className="cartao">
          <div className="cartao-titulo">Hidratação</div>
          <div className="valor-simples">
            {numero(dados?.agua?.totalMl)}<small>/ {numero(dados?.agua?.metaMl)} ml</small>
          </div>
          <div className={`barra${percAgua > 100 ? ' alerta' : ''}`}>
            <span style={{ width: `${Math.min(100, percAgua)}%` }} />
          </div>
          <div className="acoes" style={{ marginTop: 14 }}>
            <button className="btn btn-fantasma btn-mini" onClick={() => adicionarAgua(250)}>+250 ml</button>
            <button className="btn btn-fantasma btn-mini" onClick={() => adicionarAgua(500)}>+500 ml</button>
          </div>
          <button
            className="btn btn-fantasma btn-mini"
            style={{ marginTop: 8 }}
            onClick={async () => {
              try {
                const r = await api.removerUltimaAgua()
                setDados((atual) => (atual ? { ...atual, agua: { ...atual.agua, totalMl: r.totalMl ?? 0 } } : atual))
                avisar('Último registro removido')
              } catch {
                avisar('Não há registro para remover.', 'erro')
              }
            }}
          >
            Desfazer último
          </button>
          <form
            className="campo-agua"
            onSubmit={(e) => {
              e.preventDefault()
              const valor = Number(e.target.ml.value)
              if (valor > 0) adicionarAgua(valor)
              e.target.reset()
            }}
          >
            <input name="ml" type="number" min="1" max="5000" placeholder="Outro valor (ml)" aria-label="Quantidade em mililitros" />
            <button className="btn btn-fantasma btn-mini" type="submit">Registrar</button>
          </form>
          {((dados?.semanaAgua?.serie ?? dados?.semanaAgua?.dias) ?? []).length > 0 && (
            <div style={{ marginTop: 16 }}>
              <GraficoBarras
                barras={((dados.semanaAgua.serie ?? dados.semanaAgua.dias) ?? []).map((d) => ({
                  rotulo: new Date(d.data).toLocaleDateString('pt-BR', { weekday: 'narrow' }),
                  valor: d.total ?? d.totalMl ?? d.ml ?? 0,
                }))}
                meta={dados?.agua?.metaMl ?? 0}
                altura={90}
              />
            </div>
          )}
        </div>

        <div className="cartao">
          <div className="cartao-titulo">IMC</div>
          <div className="valor-simples">{numero(dados?.perfil?.imc, 1)}</div>
          <div className="rodape-cartao">{dados?.perfil?.classificacaoImc ?? '—'}</div>
        </div>

        <div className="cartao">
          <div className="cartao-titulo">Peso</div>
          <div className="valor-simples">{numero(dados?.perfil?.peso, 1)}<small>kg</small></div>
          <div className="rodape-cartao">
            meta {numero(dados?.metas?.pesoAlvo, 1)} kg · {variacao > 0 ? '+' : ''}
            {numero(variacao, 1)} kg desde o início
          </div>
          <form
            className="campo-agua"
            style={{ marginTop: 10 }}
            onSubmit={async (e) => {
              e.preventDefault()
              const valor = Number(e.target.pesoNovo.value)
              if (valor <= 0) return
              try {
                await api.atualizarPeso(valor, dados?.perfil?.altura ?? 1.6)
                avisar('Peso atualizado')
                carregar()
              } catch {
                avisar('Não foi possível atualizar o peso.', 'erro')
              }
              e.target.reset()
            }}
          >
            <input name="pesoNovo" type="number" step="0.1" min="1" placeholder="Novo peso (kg)" aria-label="Novo peso em quilos" />
            <button className="btn btn-fantasma btn-mini" type="submit">Atualizar</button>
          </form>
        </div>
      </div>

      <div className="cartao">
        <div className="cartao-titulo">Evolução</div>
        {(dados?.evolucao?.serie ?? []).length > 0 && (
          <GraficoLinha pontos={(dados.evolucao.serie ?? []).map((p) => ({ data: p.data, valor: p.peso }))} unidade="peso" />
        )}
        {(dados?.evolucao?.serie ?? []).length === 0 ? (
          <div className="vazio">
            <strong>Sem histórico ainda</strong>
            Atualize seu peso em Perfil para começar a série.
          </div>
        ) : (
          <ul className="lista">
            {(dados.evolucao.serie ?? []).slice(-5).reverse().map((ponto, indice) => (
              <li className="item" key={`${ponto.data}-${indice}`}>
                <div>
                  <div className="item-titulo">{numero(ponto.peso, 1)} kg</div>
                  <div className="item-meta">{dataCurta(ponto.data)}</div>
                </div>
                <div className="etiqueta-kcal">IMC <strong>{numero(ponto.imc, 1)}</strong></div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

/* --------------------------------------------------------------- analisar --- */

function Analisar({ avisar, aoRegistrar }) {
  const [modo, setModo] = useState('buscar')
  const [consulta, setConsulta] = useState('')
  const [resultados, setResultados] = useState([])
  const [buscando, setBuscando] = useState(false)
  const [escolhido, setEscolhido] = useState(null)
  const [gramas, setGramas] = useState(100)
  const [previa, setPrevia] = useState(null)
  const [salvando, setSalvando] = useState(false)
  const [analisandoFoto, setAnalisandoFoto] = useState(false)
  const temporizador = useRef(null)

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

  async function enviarFoto(evento) {
    const arquivo = evento.target.files?.[0]
    if (!arquivo) return

    if (arquivo.size > 6 * 1024 * 1024) {
      avisar('Imagem acima de 6 MB. Reduza antes de enviar.', 'erro')
      return
    }

    setAnalisandoFoto(true)
    try {
      const base64 = await new Promise((resolver, rejeitar) => {
        const leitor = new FileReader()
        leitor.onload = () => resolver(String(leitor.result).split(',')[1])
        leitor.onerror = rejeitar
        leitor.readAsDataURL(arquivo)
      })

      const resultado = await api.analisarImagem(base64, gramas)
      const nome = resultado?.alimento?.nome
      if (!nome) {
        avisar('Não foi possível identificar o alimento na foto.', 'erro')
        return
      }

      setEscolhido({ nome, grupo: resultado.alimento.grupo ?? 'Foto', por100g: resultado.porcao })
      avisar(`Reconhecido: ${nome}`)
    } catch (falha) {
      const motivo = falha?.corpo?.motivo
      const mensagem =
        motivo === 'provedor_de_visao_indisponivel' || falha?.status === 503
          ? 'Reconhecimento por imagem desligado no servidor (falta BSFM_GEMINI_API_KEY). Use a busca por nome.'
          : falha instanceof ErroApi
            ? falha.message
            : 'Falha ao analisar a foto.'
      avisar(mensagem, 'erro')
    } finally {
      setAnalisandoFoto(false)
      evento.target.value = ''
    }
  }

  async function registrar() {
    if (!escolhido) return
    setSalvando(true)
    try {
      await api.registrarAnalise(escolhido.nome, gramas)
      avisar(`${escolhido.nome} registrado`)
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
    ['buscar', 'Buscar', Icones.analisar],
    ['foto', 'Foto', Icones.foto],
    ['rotulo', 'Rótulo', Icones.rotulo],
  ]

  return (
    <div className="pagina" style={{ maxWidth: 760 }}>
      <div className="segmentos" role="tablist" aria-label="Modo de análise">
        {abas.map(([id, rotulo, Icone]) => (
          <button
            key={id}
            role="tab"
            aria-selected={modo === id}
            className={`segmento${modo === id ? ' ativo' : ''}`}
            onClick={() => setModo(id)}
          >
            <Icone tamanho={17} />
            {rotulo}
          </button>
        ))}
      </div>

      {modo === 'buscar' && (
        <>
          <div className="busca">
            <span className="busca-icone"><Icones.analisar tamanho={18} /></span>
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
                    <span>{item.nome}</span>
                    <span className="kcal">{numero(item.por100g?.kcal)} kcal/100 g</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {buscando && <div className="rodape-cartao" style={{ marginBottom: 12 }}>Buscando…</div>}
        </>
      )}

      {modo === 'foto' && (
        <div className="cartao" style={{ marginBottom: 14 }}>
          <div className="cartao-titulo">Analisar por foto do prato</div>
          <p className="rodape-cartao" style={{ marginTop: 0 }}>
            Envie a foto. O servidor identifica o alimento e calcula os nutrientes.
          </p>
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            onChange={enviarFoto}
            disabled={analisandoFoto}
            style={{ marginTop: 10 }}
            aria-label="Enviar foto do prato"
          />
          {analisandoFoto && <div className="rodape-cartao" style={{ marginTop: 8 }}>Analisando a imagem…</div>}
        </div>
      )}

      {modo === 'rotulo' && <Rotulo avisar={avisar} aoRegistrar={aoRegistrar} />}

      {modo !== 'rotulo' && (escolhido ? (
        <div className="cartao">
          <div className="cartao-titulo">Confirmar registro</div>

          <div className="item" style={{ border: 'none', background: 'transparent', padding: 0, marginBottom: 16 }}>
            <div>
              <div className="item-titulo">{escolhido.nome}</div>
              <div className="item-meta">{escolhido.grupo}</div>
            </div>
            <div className="etiqueta-kcal"><strong>{numero(escolhido.por100g?.kcal)}</strong> kcal/100 g</div>
          </div>

          <div className="campo">
            <label htmlFor="gramas">Porção (gramas)</label>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <button className="botao-icone" onClick={() => setGramas((g) => Math.max(1, g - 10))} aria-label="Diminuir 10 g">
                <Icones.voltar tamanho={16} />
              </button>
              <input
                id="gramas"
                type="number"
                min="1"
                max="3000"
                value={gramas}
                onChange={(e) => setGramas(Number(e.target.value) || 1)}
                style={{ textAlign: 'center' }}
              />
              <button className="botao-icone" onClick={() => setGramas((g) => Math.min(3000, g + 10))} aria-label="Aumentar 10 g">
                <Icones.mais tamanho={16} />
              </button>
            </div>
            <div className="acoes" style={{ marginTop: 10 }}>
              {[50, 100, 150, 200].map((porcao) => (
                <button key={porcao} className="btn btn-fantasma btn-mini" onClick={() => setGramas(porcao)}>
                  {porcao} g
                </button>
              ))}
            </div>
          </div>

          {previa?.porcao && (
            <table className="tabela" style={{ marginBottom: 16 }}>
              <thead>
                <tr><th>Nutriente</th><th style={{ textAlign: 'right' }}>Na porção</th></tr>
              </thead>
              <tbody>
                <tr><td>Calorias</td><td className="num">{numero(previa.porcao.kcal)} kcal</td></tr>
                <tr><td>Proteína</td><td className="num">{numero(previa.porcao.proteina, 1)} g</td></tr>
                <tr><td>Carboidrato</td><td className="num">{numero(previa.porcao.carboidrato, 1)} g</td></tr>
                <tr><td>Gordura</td><td className="num">{numero(previa.porcao.gordura, 1)} g</td></tr>
                <tr><td>Fibra</td><td className="num">{numero(previa.porcao.fibra, 1)} g</td></tr>
              </tbody>
            </table>
          )}

          <div className="acoes">
            <button className="btn" onClick={registrar} disabled={salvando}>
              {salvando ? 'Registrando…' : 'Registrar'}
            </button>
            <button className="btn btn-fantasma" onClick={() => setEscolhido(null)}>Cancelar</button>
          </div>
        </div>
      ) : (
        <div className="vazio">
          <strong>O que você comeu?</strong>
          Digite o nome do alimento ou envie uma foto. O servidor calcula os nutrientes.
        </div>
      ))}
    </div>
  )
}

/* ----------------------------------------------------------------- diário --- */

function Diario({ avisar, versao }) {
  const [feedbacks, setFeedbacks] = useState({})

  async function pedirFeedback(item) {
    try {
      const r = await api.feedback({
        kcal: item.calorias ?? 0,
        proteina: item.proteinas ?? 0,
        carboidrato: item.carbos ?? 0,
        gordura: item.gorduras ?? 0,
        fibra: null,
        metaKcal: 2000,
        consumidoKcal: 0,
        diabetes: null,
        intolerancia: null,
      })
      setFeedbacks((f) => ({ ...f, [item.id]: r }))
    } catch {
      avisar('Não foi possível gerar o feedback.', 'erro')
    }
  }
  const [itens, setItens] = useState([])
  const [carregando, setCarregando] = useState(true)

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

  const porDia = useMemo(() => {
    const mapa = new Map()
    for (const item of itens) {
      const chave = new Date(item.dataAnalise).toISOString().slice(0, 10)
      if (!mapa.has(chave)) mapa.set(chave, [])
      mapa.get(chave).push(item)
    }
    return [...mapa.entries()].sort((a, b) => (a[0] < b[0] ? 1 : -1))
  }, [itens])

  const total = useMemo(() => itens.reduce((soma, i) => soma + (i.calorias ?? 0), 0), [itens])

  return (
    <div className="pagina" style={{ maxWidth: 760 }}>
      <div className="grade grade-2" style={{ marginBottom: 14 }}>
        <div className="cartao">
          <div className="cartao-titulo">Registros</div>
          <div className="valor-simples">{itens.length}</div>
        </div>
        <div className="cartao">
          <div className="cartao-titulo">Calorias no total</div>
          <div className="valor-simples">{numero(total)}<small>kcal</small></div>
        </div>
      </div>

      {carregando ? (
        <div className="cartao">
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
        porDia.map(([dia, lista]) => (
          <section key={dia}>
            <div className="grupo-dia">
              {dataLonga(`${dia}T12:00:00`)} · {numero(lista.reduce((s, i) => s + i.calorias, 0))} kcal
            </div>
            <ul className="lista">
              {lista.map((item) => (
                <li className="item" key={item.id}>
                  <div>
                    <div className="item-titulo">{item.alimento}</div>
                    <div className="item-meta">
                      {item.porcao} · {new Date(item.dataAnalise).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
                    </div>
                  </div>
                  <div style={{ textAlign: 'right' }}>
                    <div className="etiqueta-kcal"><strong>{numero(item.calorias)}</strong> kcal</div>
                    <button
                      className="btn btn-fantasma btn-mini"
                      style={{ marginTop: 6 }}
                      onClick={() => pedirFeedback(item)}
                    >
                      Feedback
                    </button>
                  </div>
                  {feedbacks[item.id] && (
                    <p className="rodape-cartao" style={{ flexBasis: '100%', margin: 0, display: 'flex', gap: 10, alignItems: 'center' }}>
                      <span
                        className="anel"
                        style={{ ['--p']: `${feedbacks[item.id].pontuacao * 10}%`, ['--cor']: feedbacks[item.id].pontuacao >= 8 ? 'var(--acento)' : feedbacks[item.id].pontuacao >= 5 ? 'var(--aviso)' : 'var(--perigo)' }}
                      >
                        {feedbacks[item.id].pontuacao}
                      </span>
                      <span>{feedbacks[item.id].dicas?.[0] ?? feedbacks[item.id].mensagem}</span>
                    </p>
                  )}                </li>
              ))}
            </ul>
          </section>
        ))
      )}
    </div>
  )
}

/* ----------------------------------------------------------------- perfil --- */

function Perfil({ usuario, aoSair, avisar, instalar, podeInstalar, instalado, tema, alternarTema }) {
  const [sessoes, setSessoes] = useState([])

  useEffect(() => {
    api.sessoes().then(setSessoes).catch(() => setSessoes([]))
  }, [])

  async function sair() {
    try {
      await api.logout()
    } catch {
      /* sair localmente mesmo com a rede fora */
    }
    aoSair()
  }

  return (
    <div className="pagina" style={{ maxWidth: 720 }}>
      <div className="cartao" style={{ marginBottom: 14 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div className="avatar" style={{ width: 52, height: 52, fontSize: 17 }}>{iniciais(usuario?.nome)}</div>
          <div style={{ minWidth: 0 }}>
            <div className="item-titulo">{usuario?.nome}</div>
            <div className="item-meta">{usuario?.email}</div>
          </div>
          {usuario?.emailVerificado && <span className="tag tag-ok" style={{ marginLeft: 'auto' }}>verificado</span>}
        </div>

        <table className="tabela" style={{ marginTop: 18 }}>
          <tbody>
            <tr><td>Idade</td><td className="num">{usuario?.idade} anos</td></tr>
            <tr><td>Altura</td><td className="num">{numero(usuario?.altura, 2)} m</td></tr>
            <tr><td>Peso</td><td className="num">{numero(usuario?.peso, 1)} kg</td></tr>
            <tr><td>IMC</td><td className="num">{numero(usuario?.imc, 1)} · {usuario?.classificacaoImc}</td></tr>
            <tr><td>Taxa metabólica</td><td className="num">{numero(usuario?.tmb)} kcal</td></tr>
            <tr><td>Gasto total</td><td className="num">{numero(usuario?.gastoTotal)} kcal</td></tr>
            <tr><td>Segundo fator</td><td className="num">{usuario?.totpHabilitado ? 'ativo' : 'desligado'}</td></tr>
          </tbody>
        </table>
      </div>

      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Dados pessoais</div>
        <form
          onSubmit={async (e) => {
            e.preventDefault()
            try {
              await api.trocarEmail(e.target.email.value.trim(), e.target.senhaEmail.value)
              avisar('E-mail atualizado. Verifique a nova caixa.')
              window.location.reload()
            } catch (falha) {
              avisar(
                falha?.corpo?.erro === 'senha_incorreta'
                  ? 'Senha incorreta para trocar o e-mail.'
                  : falha?.corpo?.erro === 'email_em_uso'
                    ? 'Este e-mail já pertence a outra conta.'
                    : 'Não foi possível trocar o e-mail.',
                'erro',
              )
            }
          }}
          style={{ marginBottom: 18 }}
        >
          <div className="campo">
            <label htmlFor="novoEmail">E-mail</label>
            <input id="novoEmail" name="email" type="email" defaultValue={usuario?.email ?? ''} required />
          </div>
          <div className="campo">
            <label htmlFor="senhaEmail">Senha atual (confirmação)</label>
            <input id="senhaEmail" name="senhaEmail" type="password" required autoComplete="current-password" />
          </div>
          <button className="btn btn-fantasma btn-mini" type="submit">Salvar e-mail</button>
        </form>

        <form
          onSubmit={async (e) => {
            e.preventDefault()
            try {
              await api.definirNascimento(e.target.nascimento.value)
              avisar('Data de nascimento atualizada')
              window.location.reload()
            } catch {
              avisar('Não foi possível salvar a data.', 'erro')
            }
          }}
        >
          <div className="campo">
            <label htmlFor="nascimento">Data de nascimento</label>
            <input
              id="nascimento"
              name="nascimento"
              type="date"
              defaultValue={usuario?.dataNascimento?.slice(0, 10) ?? ''}
              required
            />
          </div>
          <button className="btn btn-fantasma btn-mini" type="submit">Salvar data</button>
        </form>
      </div>

      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Perfil de saúde</div>
        <p className="rodape-cartao" style={{ marginTop: 0 }}>
          Usado para adaptar as recomendações alimentares. Sem isso, o painel mostra um aviso
          de perfil incompleto — como na v1.
        </p>
        <form
          onSubmit={async (e) => {
            e.preventDefault()
            try {
              await api.atualizarSaude(e.target.diabetes.value, e.target.intolerancia.value.trim())
              avisar('Perfil de saúde atualizado')
              window.location.reload()
            } catch {
              avisar('Não foi possível salvar o perfil de saúde.', 'erro')
            }
          }}
        >
          <div className="campo">
            <label htmlFor="diabetes">Diabetes</label>
            <select id="diabetes" name="diabetes" defaultValue={usuario?.diabetes ?? 'Não informado'}>
              <option>Não informado</option>
              <option>Não</option>
              <option>Tipo 1</option>
              <option>Tipo 2</option>
              <option>Pré-diabetes</option>
              <option>Gestacional</option>
            </select>
          </div>
          <div className="campo">
            <label htmlFor="intolerancia">Intolerâncias / restrições</label>
            <input
              id="intolerancia"
              name="intolerancia"
              defaultValue={usuario?.intolerancia ?? ''}
              placeholder="Ex: lactose, glúten"
              maxLength={400}
            />
          </div>
          <button className="btn btn-fantasma btn-mini" type="submit">Salvar saúde</button>
        </form>
      </div>

      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Emergências próximas (São Paulo)</div>
        <ul className="lista">
          {HOSPITAIS.map((h) => (
            <li className="item" key={h.nome}>
              <div>
                <div className="item-titulo">{h.nome}</div>
                <div className="item-meta">{h.endereco} · {h.tipo}</div>
              </div>
              <div className="acoes">
                <a className="btn btn-fantasma btn-mini" href={`tel:${h.telefone.replace(/\D/g, '')}`}>Ligar</a>
                <a
                  className="btn btn-fantasma btn-mini"
                  href={`https://waze.com/ul?ll=${h.lat},${h.lng}&navigate=yes`}
                  target="_blank"
                  rel="noreferrer"
                >
                  Waze
                </a>
              </div>
            </li>
          ))}
        </ul>
        <p className="rodape-cartao" style={{ marginTop: 10 }}>
          Em emergência ligue 192 (SAMU). Esta lista é referência, não substitui atendimento.
        </p>
      </div>

      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Termos de uso e LGPD</div>
        <details>
          <summary className="rodape-cartao" style={{ cursor: 'pointer' }}>
            Ler os termos completos
          </summary>
          <div className="texto-ocr" style={{ whiteSpace: 'pre-wrap' }}>
{TERMOS}
          </div>
        </details>
      </div>
      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Sessões</div>
        {sessoes.length === 0 ? (
          <div className="rodape-cartao">Nenhuma sessão listada.</div>
        ) : (
          <ul className="lista">
            {sessoes.slice(0, 5).map((s) => (
              <li className="item" key={s.id}>
                <div>
                  <div className="item-titulo">{s.atual ? 'Este dispositivo' : 'Outro dispositivo'}</div>
                  <div className="item-meta">desde {dataCurta(s.criadaEm)}</div>
                </div>
                {s.revogadaEm ? <span className="tag">encerrada</span> : <span className="tag tag-ok">ativa</span>}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Aplicativo</div>
        <p className="rodape-cartao" style={{ marginTop: 0 }}>
          {instalado
            ? 'Este dispositivo já usa a versão instalada do BSFM.'
            : 'Instale para abrir em tela cheia, com ícone próprio e funcionamento offline.'}
        </p>
        <div className="acoes" style={{ marginTop: 12 }}>
          {!instalado && (
            <button className="btn" onClick={instalar} disabled={!podeInstalar}>
              <Icones.instalar tamanho={17} />
              {podeInstalar ? 'Instalar aplicativo' : 'Use o menu do navegador'}
            </button>
          )}
          <button className="btn btn-fantasma" onClick={alternarTema}>
            {tema === 'escuro' ? <Icones.sol tamanho={17} /> : <Icones.lua tamanho={17} />}
            Tema {tema === 'escuro' ? 'claro' : 'escuro'}
          </button>
        </div>
      </div>

      <button className="btn btn-perigo btn-bloco" onClick={sair}>
        <Icones.sair tamanho={17} />
        Sair da conta
      </button>
    </div>
  )
}

/* ------------------------------------------------------------------ metas --- */

function Metas({ avisar }) {
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
      avisar('Meta atualizada')
      carregar()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao salvar a meta.', 'erro')
    } finally {
      setSalvando(false)
    }
  }

  const atualizar = (campo) => (e) => setForm((f) => ({ ...f, [campo]: e.target.value }))

  return (
    <div className="pagina" style={{ maxWidth: 760 }}>
      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Meta atual</div>
        <div className="grade grade-3">
          <div>
            <div className="valor-simples">{numero(meta?.caloriasAlvo)}<small>kcal</small></div>
            <div className="rodape-cartao">{numero(meta?.caloriasConsumidasHoje)} consumidas hoje</div>
          </div>
          <div>
            <div className="valor-simples">{numero(meta?.aguaAlvoMl)}<small>ml</small></div>
            <div className="rodape-cartao">meta de água</div>
          </div>
          <div>
            <div className="valor-simples">{numero(meta?.pesoAlvo, 1)}<small>kg</small></div>
            <div className="rodape-cartao">peso alvo</div>
          </div>
        </div>
        <div className={`barra${(meta?.percentualCalorico ?? 0) > 100 ? ' alerta' : ''}`} style={{ marginTop: 16 }}>
          <span style={{ width: `${Math.min(100, meta?.percentualCalorico ?? 0)}%` }} />
        </div>
        <div className="rodape-cartao">{numero(meta?.percentualCalorico, 1)}% da meta calórica de hoje</div>
      </div>

      <div className="grade grade-2" style={{ marginBottom: 14 }}>
        <div className="cartao">
          <div className="cartao-titulo">Evolução do peso</div>
          <GraficoLinha
            pontos={(evolucao?.serie ?? []).map((p) => ({ data: p.data, valor: p.peso }))}
            unidade="peso"
            altura={160}
          />
        </div>
        <div className="cartao">
          <div className="cartao-titulo">Evolução do IMC</div>
          <GraficoLinha
            pontos={(evolucao?.serie ?? []).map((p) => ({ data: p.data, valor: p.imc }))}
            unidade="imc"
            cor="var(--info)"
            altura={160}
          />
        </div>
      </div>
      <form className="cartao" onSubmit={salvar} style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Definir nova meta</div>
        <div className="linha-campos">
          <div className="campo">
            <label htmlFor="kcal">Calorias (kcal)</label>
            <input id="kcal" type="number" min="1" value={form.caloriasAlvo} onChange={atualizar('caloriasAlvo')} required />
          </div>
          <div className="campo">
            <label htmlFor="prot">Proteínas (g)</label>
            <input id="prot" type="number" min="0" value={form.proteinasAlvo} onChange={atualizar('proteinasAlvo')} />
          </div>
        </div>
        <div className="linha-campos">
          <div className="campo">
            <label htmlFor="agua">Água (ml)</label>
            <input id="agua" type="number" min="0" value={form.aguaAlvoMl} onChange={atualizar('aguaAlvoMl')} />
          </div>
          <div className="campo">
            <label htmlFor="pesoAlvo">Peso alvo (kg)</label>
            <input id="pesoAlvo" type="number" step="0.1" min="1" value={form.pesoAlvo} onChange={atualizar('pesoAlvo')} />
          </div>
        </div>
        <button className="btn" type="submit" disabled={salvando}>
          {salvando ? 'Salvando…' : 'Salvar meta'}
        </button>
      </form>

      <div className="cartao">
        <div className="cartao-titulo">Histórico de metas</div>
        {historico.length === 0 ? (
          <div className="rodape-cartao">A primeira meta definida aparece aqui.</div>
        ) : (
          <ul className="lista">
            {historico.map((m) => (
              <li className="item" key={m.id}>
                <div>
                  <div className="item-titulo">{numero(m.caloriasAlvo)} kcal · {numero(m.pesoAlvo, 1)} kg</div>
                  <div className="item-meta">{dataCurta(m.criadoEm)} · água {numero(m.aguaAlvoMl)} ml</div>
                </div>
                {m.ativa ? <span className="tag tag-ok">ativa</span> : <span className="tag">substituída</span>}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

/* ----------------------------------------------------------------- planos --- */

const DIAS = ['Segunda', 'Terca', 'Quarta', 'Quinta', 'Sexta', 'Sabado', 'Domingo']
const TIPOS = ['Cafe', 'Almoco', 'Jantar', 'Lanche']

function Planos({ avisar }) {
  const [itens, setItens] = useState([])
  const [calorias, setCalorias] = useState(0)
  const [form, setForm] = useState({ diaSemana: 'Segunda', tipoRefeicao: 'Almoco', nomePrato: '', calorias: '' })
  const [salvando, setSalvando] = useState(false)

  const carregar = useCallback(async () => {
    try {
      const dados = await api.refeicoes()
      setItens(dados.itens ?? [])
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
      await api.criarRefeicao({
        diaSemana: form.diaSemana,
        tipoRefeicao: form.tipoRefeicao,
        nomePrato: form.nomePrato.trim(),
        calorias: Number(form.calorias) || 0,
      })
      avisar('Refeição adicionada ao plano')
      setForm((f) => ({ ...f, nomePrato: '', calorias: '' }))
      carregar()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Falha ao adicionar.', 'erro')
    } finally {
      setSalvando(false)
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

  return (
    <div className="pagina" style={{ maxWidth: 760 }}>
      <Prescricao avisar={avisar} aoImportar={carregar} />
      <div className="cartao" style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Plano da semana</div>
        <div className="valor-simples">{itens.length}<small>refeições</small></div>
        <div className="rodape-cartao">{numero(calorias)} kcal planejadas</div>
      </div>

      <form className="cartao" onSubmit={adicionar} style={{ marginBottom: 14 }}>
        <div className="cartao-titulo">Adicionar refeição</div>
        <div className="linha-campos">
          <div className="campo">
            <label htmlFor="dia">Dia</label>
            <select id="dia" value={form.diaSemana} onChange={(e) => setForm({ ...form, diaSemana: e.target.value })}>
              {DIAS.map((d) => <option key={d} value={d}>{d}</option>)}
            </select>
          </div>
          <div className="campo">
            <label htmlFor="tipo">Refeição</label>
            <select id="tipo" value={form.tipoRefeicao} onChange={(e) => setForm({ ...form, tipoRefeicao: e.target.value })}>
              {TIPOS.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
        </div>
        <div className="linha-campos">
          <div className="campo" style={{ flex: 2 }}>
            <label htmlFor="prato">Prato</label>
            <input id="prato" value={form.nomePrato} onChange={(e) => setForm({ ...form, nomePrato: e.target.value })} required minLength={2} placeholder="Frango com arroz" />
          </div>
          <div className="campo">
            <label htmlFor="kcalPrato">Calorias</label>
            <input id="kcalPrato" type="number" min="0" value={form.calorias} onChange={(e) => setForm({ ...form, calorias: e.target.value })} />
          </div>
        </div>
        <button className="btn" type="submit" disabled={salvando}>{salvando ? 'Salvando…' : 'Adicionar'}</button>
      </form>

      {itens.length === 0 ? (
        <div className="vazio">
          <strong>Nenhum plano ainda</strong>
          Monte a semana adicionando refeições acima.
        </div>
      ) : (
        DIAS.filter((d) => itens.some((i) => i.diaSemana === d)).map((dia) => (
          <section key={dia}>
            <div className="grupo-dia">{dia}</div>
            <ul className="lista">
              {itens.filter((i) => i.diaSemana === dia).map((item) => (
                <li className="item" key={item.id}>
                  <div>
                    <div className="item-titulo">{item.nomePrato}</div>
                    <div className="item-meta">{item.tipoRefeicao} · {numero(item.calorias)} kcal</div>
                  </div>
                  <button className="btn btn-perigo btn-mini" onClick={() => remover(item.id)}>Remover</button>
                </li>
              ))}
            </ul>
          </section>
        ))
      )}
    </div>
  )
}

/* ------------------------------------------------------------------ shell --- */

const HOSPITAIS = [
  { nome: 'Hospital das Clínicas (HC-FMUSP)', tipo: 'Hospital', endereco: 'Av. Dr. Enéas de Carvalho Aguiar, 255 - Cerqueira César', telefone: '(11) 2661-0000', lat: -23.5592, lng: -46.6694 },
  { nome: 'Hospital São Paulo (UNIFESP)', tipo: 'Hospital', endereco: 'Rua Botucatu, 740 - Vila Clementino', telefone: '(11) 5576-4000', lat: -23.5932, lng: -46.6375 },
  { nome: 'Hospital do Servidor Público Estadual (IAMSPE)', tipo: 'Hospital', endereco: 'Rua Pedro de Toledo, 1800 - Vila Clementino', telefone: '(11) 5088-8000', lat: -23.595, lng: -46.64 },
  { nome: 'UPA 24h - Sé', tipo: 'UPA', endereco: 'Rua Frederico Alvarenga, 259 - Sé', telefone: '(11) 3101-0000', lat: -23.5505, lng: -46.6333 },
  { nome: 'UPA 24h - Tatuapé', tipo: 'UPA', endereco: 'Rua Serra de Bragança, 200 - Tatuapé', telefone: '(11) 2295-0000', lat: -23.54, lng: -46.575 },
]

const TERMOS = `Ao criar uma conta no BSFM você concorda que:

1. Os dados informados (peso, altura, refeições, água) são usados exclusivamente para calcular indicadores nutricionais e exibir seu histórico.
2. O BSFM é um projeto acadêmico e não substitui consulta com nutricionista ou médico.
3. Você pode solicitar a exclusão da sua conta e dos seus dados a qualquer momento.
4. O e-mail é usado para verificação da conta e recuperação de senha.
5. Nenhum dado de saúde é compartilhado com terceiros.`

const ABAS = [
  { id: 'painel', rotulo: 'Painel', icone: Icones.painel, titulo: 'Painel', sub: 'Resumo do seu dia' },
  { id: 'analisar', rotulo: 'Analisar', icone: Icones.analisar, titulo: 'Analisar alimento', sub: 'Busque e registre em segundos' },
  { id: 'diario', rotulo: 'Diário', icone: Icones.diario, titulo: 'Diário alimentar', sub: 'Tudo o que você registrou' },
  { id: 'planos', rotulo: 'Planos', icone: Icones.planos, titulo: 'Planos da semana', sub: 'Monte a semana alimentar' },
  { id: 'metas', rotulo: 'Metas', icone: Icones.metas, titulo: 'Metas', sub: 'Calorias, água e peso alvo' },
  { id: 'perfil', rotulo: 'Perfil', icone: Icones.perfil, titulo: 'Perfil', sub: 'Conta, dispositivos e aplicativo' },
]

function App() {
  const [usuario, setUsuario] = useState(null)
  const [rota, setRota] = useState(() => (sessao.ativa ? 'app' : 'landing'))
  const [aba, setAba] = useState('painel')
  const [versao, setVersao] = useState(0)
  const [mensagem, setMensagem] = useState(null)
  const [convite, setConvite] = useState(null)
  const [instalado, setInstalado] = useState(false)
  const [tema, alternarTema] = useTema()

  const avisar = useCallback((texto, tipo = 'ok') => setMensagem({ texto, tipo }), [])

  useEffect(() => {
    if (!mensagem) return
    const t = setTimeout(() => setMensagem(null), 3200)
    return () => clearTimeout(t)
  }, [mensagem])

  useEffect(() => {
    if (!sessao.ativa) return
    api.perfil().then(setUsuario).catch(() => sessao.limpar())
  }, [])

  useEffect(() => {
    const aoConvidar = (evento) => {
      evento.preventDefault()
      setConvite(evento)
    }
    const aoInstalar = () => {
      setInstalado(true)
      setConvite(null)
    }
    window.addEventListener('beforeinstallprompt', aoConvidar)
    window.addEventListener('appinstalled', aoInstalar)
    setInstalado(window.matchMedia('(display-mode: standalone)').matches)
    return () => {
      window.removeEventListener('beforeinstallprompt', aoConvidar)
      window.removeEventListener('appinstalled', aoInstalar)
    }
  }, [])

  async function instalar() {
    if (!convite) return
    convite.prompt()
    const escolha = await convite.userChoice
    if (escolha.outcome === 'accepted') setConvite(null)
  }

  function sair() {
    sessao.limpar()
    setUsuario(null)
    setAba('painel')
    setRota('landing')
  }

  if (!usuario && !sessao.ativa) {
    // Landing pública primeiro: é a porta de entrada do projeto, com o botão de
    // instalar. Só quem escolhe "Entrar"/"Criar conta" vê o formulário.
    if (rota === 'recuperar') {
      return <Recuperar aoVoltar={() => setRota('entrar')} aoEntrar={() => setRota('entrar')} />
    }

    if (rota === 'landing') {
      return (
        <Landing
          aoEntrar={() => setRota('entrar')}
          aoCriarConta={() => setRota('cadastro')}
          instalar={instalar}
          podeInstalar={Boolean(convite)}
          instalado={instalado}
        />
      )
    }

    return (
      <Acesso
        modoInicial={rota}
        aoEntrar={setUsuario}
        aoVoltarSite={() => setRota('landing')}
        aoRecuperar={() => setRota('recuperar')}
        instalar={instalar}
        podeInstalar={Boolean(convite)}
        instalado={instalado}
      />
    )
  }

  const atual = ABAS.find((a) => a.id === aba) ?? ABAS[0]

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="marca">
          <div className="marca-simbolo">BS</div>
          <div className="marca-texto">
            <span className="marca-nome">BSFM</span>
            <span className="marca-tag">Nutrição</span>
          </div>
        </div>

        <div className="nav-rotulo">Principal</div>
        <nav aria-label="Navegação principal">
          {ABAS.map((item) => {
            const Icone = item.icone
            return (
              <button
                key={item.id}
                className="nav-item"
                aria-current={aba === item.id ? 'page' : undefined}
                onClick={() => setAba(item.id)}
              >
                <Icone tamanho={19} />
                {item.rotulo}
              </button>
            )
          })}
        </nav>

        <div className="sidebar-rodape">
          {!instalado && (
            <div className="cartao-instalar">
              <strong>Aplicativo BSFM</strong>
              <p>Instale para abrir em tela cheia e sem barra do navegador.</p>
              <button className="btn btn-mini btn-bloco" onClick={instalar} disabled={!convite}>
                {convite ? 'Instalar' : 'Use o menu do navegador'}
              </button>
            </div>
          )}
          <button className="nav-item" onClick={alternarTema}>
            {tema === 'escuro' ? <Icones.sol tamanho={19} /> : <Icones.lua tamanho={19} />}
            Tema {tema === 'escuro' ? 'claro' : 'escuro'}
          </button>
        </div>
      </aside>

      <div className="conteudo">
        <header className="topo">
          <div className="topo-titulos">
            <h1>{atual.titulo}</h1>
            <p>{atual.sub}</p>
          </div>
          <div className="topo-acoes">
            <button className="botao-icone" onClick={alternarTema} aria-label="Alternar tema">
              {tema === 'escuro' ? <Icones.sol tamanho={18} /> : <Icones.lua tamanho={18} />}
            </button>
            <div className="chip-usuario">
              <div className="avatar">{iniciais(usuario?.nome)}</div>
              <span className="nome">{usuario?.nome?.split(' ')[0]}</span>
            </div>
          </div>
        </header>

        {aba === 'painel' && <Painel irPara={setAba} avisar={avisar} aoAtualizar={() => setVersao((v) => v + 1)} />}
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
        {aba === 'planos' && <Planos avisar={avisar} />}
        {aba === 'metas' && <Metas avisar={avisar} />}
        {aba === 'perfil' && (
          <Perfil
            usuario={usuario}
            aoSair={sair}
            avisar={avisar}
            instalar={instalar}
            podeInstalar={Boolean(convite)}
            instalado={instalado}
            tema={tema}
            alternarTema={alternarTema}
          />
        )}
      </div>

      <nav className="barra-mobile" aria-label="Navegação principal">
        {ABAS.map((item) => {
          const Icone = item.icone
          return (
            <button
              key={item.id}
              className="aba"
              aria-current={aba === item.id ? 'page' : undefined}
              onClick={() => setAba(item.id)}
            >
              <Icone tamanho={22} />
              {item.rotulo}
            </button>
          )
        })}
      </nav>

      {mensagem && (
        <div className={`aviso-flutuante${mensagem.tipo === 'erro' ? ' erro' : ''}`} role="status">
          {mensagem.texto}
        </div>
      )}
    </div>
  )
}

export default App
