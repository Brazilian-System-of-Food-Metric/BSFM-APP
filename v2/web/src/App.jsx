import { useCallback, useEffect, useState } from 'react'
import { api, sessao } from './api.js'
import Acesso from './Acesso.jsx'
import Analisar from './Analisar.jsx'
import Diario from './Diario.jsx'
import Landing from './Landing.jsx'
import Metas from './Metas.jsx'
import Painel from './Painel.jsx'
import Perfil from './Perfil.jsx'
import Planos from './Planos.jsx'
import Recuperar from './Recuperar.jsx'
import { Icone } from './Icones.jsx'
import { iniciais } from './ui.jsx'

/* ==========================================================================
   BSFM — casco do aplicativo.
   Um único código para desktop e celular: sidebar no desktop, barra inferior
   no celular. O tema fica em localStorage; o default é o claro "soft clean".
   ========================================================================== */

function lerTemaSalvo() {
  try {
    return localStorage.getItem('bsfm.tema') ?? 'claro'
  } catch {
    return 'claro'
  }
}

function useTema() {
  const [tema, setTema] = useState(lerTemaSalvo)

  useEffect(() => {
    document.documentElement.dataset.tema = tema
    try {
      localStorage.setItem('bsfm.tema', tema)
    } catch {
      /* navegação privada pode bloquear o storage; o tema vale só nesta aba */
    }
    const meta = document.querySelector('meta[name="theme-color"]')
    if (meta) meta.setAttribute('content', tema === 'escuro' ? '#111815' : '#f4f6f1')
  }, [tema])

  return [tema, () => setTema((t) => (t === 'escuro' ? 'claro' : 'escuro'))]
}

const ABAS = [
  { id: 'painel', rotulo: 'Painel', icone: 'painel', titulo: 'Painel', sub: 'Resumo do seu dia' },
  { id: 'analisar', rotulo: 'Analisar', icone: 'analisar', titulo: 'Analisar alimento', sub: 'Busque, fotografe ou leia um rótulo' },
  { id: 'diario', rotulo: 'Diário', icone: 'diario', titulo: 'Diário alimentar', sub: 'Tudo o que você registrou' },
  { id: 'planos', rotulo: 'Planos', icone: 'planos', titulo: 'Planos da semana', sub: 'Monte a semana alimentar' },
  { id: 'metas', rotulo: 'Metas', icone: 'metas', titulo: 'Metas', sub: 'Calorias, água e peso alvo' },
  { id: 'perfil', rotulo: 'Perfil', icone: 'perfil', titulo: 'Perfil', sub: 'Conta, segurança e aplicativo' },
]

const GRUPOS = [
  ['Acompanhar', ['painel', 'analisar', 'diario']],
  ['Planejar', ['planos', 'metas']],
  ['Conta', ['perfil']],
]

function TelaCarregando() {
  return (
    <div className="tela-carregando">
      <div className="marca-simbolo">BS</div>
      <div className="carregando-barra"><span /></div>
      <p>Carregando seu painel…</p>
    </div>
  )
}

export default function App() {
  const [usuario, setUsuario] = useState(null)
  const [carregandoSessao, setCarregandoSessao] = useState(() => sessao.ativa)
  const [rota, setRota] = useState(() => (sessao.ativa ? 'app' : 'landing'))
  const [aba, setAba] = useState('painel')
  const [versao, setVersao] = useState(0)
  const [mensagem, setMensagem] = useState(null)
  const [convite, setConvite] = useState(null)
  const [instalado, setInstalado] = useState(false)
  const [tema, alternarTema] = useTema()

  const avisar = useCallback((texto, tipo = 'ok') => setMensagem({ texto, tipo, id: Date.now() }), [])

  useEffect(() => {
    if (!mensagem) return
    const t = setTimeout(() => setMensagem(null), 3800)
    return () => clearTimeout(t)
  }, [mensagem])

  // Sessão existente: busca o perfil antes de decidir a tela.
  useEffect(() => {
    if (!sessao.ativa) {
      setCarregandoSessao(false)
      return
    }
    let ativo = true
    api
      .perfil()
      .then((perfil) => {
        if (ativo) {
          setUsuario(perfil)
          setRota('app')
        }
      })
      .catch(() => {
        sessao.limpar()
        if (ativo) setRota('landing')
      })
      .finally(() => {
        if (ativo) setCarregandoSessao(false)
      })
    return () => {
      ativo = false
    }
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

  useEffect(() => {
    if (usuario) window.scrollTo({ top: 0, behavior: 'smooth' })
  }, [aba, usuario])

  async function instalar() {
    if (!convite) return
    convite.prompt()
    const escolha = await convite.userChoice
    if (escolha.outcome === 'accepted') setConvite(null)
  }

  function entrar(perfil) {
    setUsuario(perfil)
    setRota('app')
    setAba('painel')
    setVersao((v) => v + 1)
    // O login devolve o resumo (UsuarioSaida). O perfil completo tem
    // dataNascimento, criadoEm, saúde e emailVerificado — busca em seguida.
    api.perfil().then(setUsuario).catch(() => {})
  }

  function sair() {
    sessao.limpar()
    setUsuario(null)
    setAba('painel')
    setRota('landing')
  }

  const recarregarPerfil = useCallback(() => {
    api.perfil().then(setUsuario).catch(() => {})
  }, [])

  if (carregandoSessao) return <TelaCarregando />

  if (!usuario) {
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
          tema={tema}
          alternarTema={alternarTema}
        />
      )
    }

    return (
      <Acesso
        modoInicial={rota}
        aoEntrar={entrar}
        aoVoltarSite={() => setRota('landing')}
        aoRecuperar={() => setRota('recuperar')}
        instalar={instalar}
        podeInstalar={Boolean(convite)}
        instalado={instalado}
        tema={tema}
        alternarTema={alternarTema}
      />
    )
  }

  const atual = ABAS.find((a) => a.id === aba) ?? ABAS[0]

  return (
    <div className="app">
      <aside className="sidebar">
        <button className="marca marca-botao" onClick={() => setAba('painel')} aria-label="Ir para o painel">
          <div className="marca-simbolo">BS</div>
          <div className="marca-texto">
            <span className="marca-nome">BSFM</span>
            <span className="marca-tag">Nutrição</span>
          </div>
        </button>

        <nav aria-label="Navegação principal" className="sidebar-nav">
          {GRUPOS.map(([grupo, ids]) => (
            <div className="nav-grupo" key={grupo}>
              <div className="nav-rotulo">{grupo}</div>
              {ids.map((id) => {
                const item = ABAS.find((a) => a.id === id)
                return (
                  <button
                    key={id}
                    className="nav-item"
                    aria-current={aba === id ? 'page' : undefined}
                    onClick={() => setAba(id)}
                  >
                    <Icone nome={item.icone} tamanho={19} />
                    <span>{item.rotulo}</span>
                    {id === 'diario' && versao > 0 && <span className="nav-ponto" />}
                  </button>
                )
              })}
            </div>
          ))}
        </nav>

        <div className="sidebar-rodape">
          {!instalado && (
            <div className="cartao-instalar">
              <span className="cartao-instalar-icone"><Icone nome="instalar" tamanho={17} /></span>
              <strong>Aplicativo BSFM</strong>
              <p>Instale para abrir em tela cheia, com ícone próprio e offline.</p>
              <button className="btn btn-mini btn-bloco" onClick={instalar} disabled={!convite}>
                {convite ? 'Instalar' : 'Use o menu do navegador'}
              </button>
            </div>
          )}

          <button className="nav-item" onClick={alternarTema}>
            <Icone nome={tema === 'escuro' ? 'sol' : 'lua'} tamanho={19} />
            <span>Tema {tema === 'escuro' ? 'claro' : 'escuro'}</span>
          </button>

          <button className="sidebar-usuario" onClick={() => setAba('perfil')}>
            <span className="avatar">{iniciais(usuario.nome)}</span>
            <span className="sidebar-usuario-texto">
              <strong>{usuario.nome?.split(' ')[0]}</strong>
              <small>Ver perfil</small>
            </span>
            <Icone nome="avancar" tamanho={15} />
          </button>
        </div>
      </aside>

      <div className="conteudo">
        <header className="topo">
          <div className="topo-marca">
            <div className="marca-simbolo">BS</div>
            <span className="marca-nome">BSFM</span>
          </div>

          <div className="topo-titulos">
            <h1>{atual.titulo}</h1>
            <p>{atual.sub}</p>
          </div>

          <div className="topo-acoes">
            {!instalado && (
              <button className="btn btn-fantasma btn-mini topo-instalar" onClick={instalar} disabled={!convite}>
                <Icone nome="instalar" tamanho={15} /> Instalar
              </button>
            )}
            <button className="botao-icone" onClick={alternarTema} aria-label="Alternar tema">
              <Icone nome={tema === 'escuro' ? 'sol' : 'lua'} tamanho={18} />
            </button>
            <button className="chip-usuario" onClick={() => setAba('perfil')} aria-label="Abrir perfil">
              <span className="avatar">{iniciais(usuario.nome)}</span>
              <span className="nome">{usuario.nome?.split(' ')[0]}</span>
            </button>
          </div>
        </header>

        {aba === 'painel' && (
          <Painel
            usuario={usuario}
            irPara={setAba}
            avisar={avisar}
            aoAtualizar={() => setVersao((v) => v + 1)}
          />
        )}
        {aba === 'analisar' && (
          <Analisar
            avisar={avisar}
            aoRegistrar={() => {
              setVersao((v) => v + 1)
              setAba('diario')
            }}
          />
        )}
        {aba === 'diario' && <Diario avisar={avisar} versao={versao} irPara={setAba} />}
        {aba === 'planos' && <Planos avisar={avisar} />}
        {aba === 'metas' && <Metas avisar={avisar} usuario={usuario} />}
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
            aoAtualizarPerfil={recarregarPerfil}
          />
        )}
      </div>

      <nav className="barra-mobile" aria-label="Navegação principal">
        {ABAS.map((item) => (
          <button
            key={item.id}
            className="aba"
            aria-current={aba === item.id ? 'page' : undefined}
            onClick={() => setAba(item.id)}
          >
            <Icone nome={item.icone} tamanho={21} />
            <span>{item.rotulo}</span>
          </button>
        ))}
      </nav>

      {mensagem && (
        <div className={`aviso-flutuante${mensagem.tipo === 'erro' ? ' erro' : ''}`} role="status" key={mensagem.id}>
          <Icone nome={mensagem.tipo === 'erro' ? 'info' : 'check'} tamanho={16} />
          <span>{mensagem.texto}</span>
          <button onClick={() => setMensagem(null)} aria-label="Fechar aviso">
            <Icone nome="x" tamanho={14} />
          </button>
        </div>
      )}
    </div>
  )
}
