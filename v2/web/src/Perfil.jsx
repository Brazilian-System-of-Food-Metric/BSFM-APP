import { useEffect, useMemo, useState } from 'react'
import { ErroApi, api } from './api.js'
import { Icone } from './Icones.jsx'
import Emergencias from './Emergencias.jsx'
import { ModalTermos } from './Termos.jsx'
import { Selo, dataCompleta, dataCurta, iniciais, numero } from './ui.jsx'

/* Perfil: conta, segurança (senha, 2FA, dispositivos), saúde, privacidade,
   emergências e aplicativo. Tudo o que a v1 espalhava em modais, aqui em
   seções com estado explícito. */


function distanciaKm(a, b) {
  const raio = 6371
  const rad = (graus) => (graus * Math.PI) / 180
  const dLat = rad(b.lat - a.lat)
  const dLng = rad(b.lng - a.lng)
  const h =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(rad(a.lat)) * Math.cos(rad(b.lat)) * Math.sin(dLng / 2) ** 2
  return 2 * raio * Math.asin(Math.sqrt(h))
}

const SECOES = [
  ['conta', 'Conta'],
  ['seguranca', 'Segurança'],
  ['saude', 'Saúde'],
  ['privacidade', 'Privacidade'],
  ['emergencias', 'Emergências'],
  ['app', 'Aplicativo'],
]

export default function Perfil({
  usuario,
  aoSair,
  avisar,
  instalar,
  podeInstalar,
  instalado,
  tema,
  alternarTema,
  aoAtualizarPerfil,
}) {
  const [sessoes, setSessoes] = useState([])
  const [conta, setConta] = useState({
    nome: '',
    email: '',
    nascimento: '',
    peso: '',
    altura: '',
    diabetes: 'Não informado',
    intolerancia: '',
  })
  const [senhaEmail, setSenhaEmail] = useState('')
  const [senhaAtual, setSenhaAtual] = useState('')
  const [novaSenha, setNovaSenha] = useState('')
  const [confirmarSenha, setConfirmarSenha] = useState('')
  const [doisFatores, setDoisFatores] = useState(null)
  const [senha2fa, setSenha2fa] = useState('')
  const [codigo2fa, setCodigo2fa] = useState('')
  const [desativando2fa, setDesativando2fa] = useState(false)
  const [termosAberto, setTermosAberto] = useState(false)
  const [posicao, setPosicao] = useState(null)
  const [buscandoLocal, setBuscandoLocal] = useState(false)
  const [exportando, setExportando] = useState(false)
  const [excluindo, setExcluindo] = useState(false)
  const [senhaExclusao, setSenhaExclusao] = useState('')
  const [confirmacaoExclusao, setConfirmacaoExclusao] = useState('')
  const [excluindoConta, setExcluindoConta] = useState(false)

  useEffect(() => {
    if (!usuario) return
    setConta({
      nome: usuario.nome ?? '',
      email: usuario.email ?? '',
      nascimento: usuario.dataNascimento?.slice(0, 10) ?? '',
      peso: Number(usuario.peso ?? 0).toFixed(1),
      altura: Number(usuario.altura ?? 0).toFixed(2),
      diabetes: usuario.diabetes ?? 'Não informado',
      intolerancia: usuario.intolerancia ?? '',
    })
  }, [usuario])

  const carregarSessoes = () => {
    api.sessoes().then(setSessoes).catch(() => setSessoes([]))
  }

  useEffect(() => {
    carregarSessoes()
  }, [])

  const atualizarConta = (campo) => (e) => setConta((c) => ({ ...c, [campo]: e.target.value }))

  async function sair() {
    try {
      await api.logout()
    } catch {
      /* sair localmente mesmo com a rede fora */
    }
    aoSair()
  }

  async function salvarNome(evento) {
    evento.preventDefault()
    try {
      await api.alterarNome(conta.nome.trim())
      avisar('Nome atualizado')
      aoAtualizarPerfil()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Não foi possível salvar o nome.', 'erro')
    }
  }

  async function salvarEmail(evento) {
    evento.preventDefault()
    try {
      await api.trocarEmail(conta.email.trim(), senhaEmail)
      setSenhaEmail('')
      avisar('E-mail atualizado. Verifique a nova caixa.')
      aoAtualizarPerfil()
    } catch (falha) {
      const codigo = falha?.corpo?.erro
      avisar(
        codigo === 'senha_incorreta'
          ? 'Senha incorreta para trocar o e-mail.'
          : codigo === 'email_em_uso'
            ? 'Este e-mail já pertence a outra conta.'
            : 'Não foi possível trocar o e-mail.',
        'erro',
      )
    }
  }

  async function salvarNascimento(evento) {
    evento.preventDefault()
    try {
      await api.definirNascimento(conta.nascimento)
      avisar('Data de nascimento atualizada — idade recalculada')
      aoAtualizarPerfil()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Não foi possível salvar a data.', 'erro')
    }
  }

  async function salvarMedidas(evento) {
    evento.preventDefault()
    try {
      await api.atualizarMedidas(Number(conta.peso), Number(conta.altura))
      avisar('Medidas atualizadas — IMC, TMB e gasto recalculados')
      aoAtualizarPerfil()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Não foi possível salvar as medidas.', 'erro')
    }
  }

  async function salvarSaude(evento) {
    evento.preventDefault()
    try {
      await api.atualizarSaude(conta.diabetes, conta.intolerancia.trim())
      avisar('Perfil de saúde atualizado')
      aoAtualizarPerfil()
    } catch (falha) {
      avisar(falha instanceof ErroApi ? falha.message : 'Não foi possível salvar o perfil de saúde.', 'erro')
    }
  }

  async function trocarSenha(evento) {
    evento.preventDefault()
    if (novaSenha !== confirmarSenha) {
      avisar('A confirmação não confere com a nova senha.', 'erro')
      return
    }
    try {
      const resposta = await api.trocarSenha(senhaAtual, novaSenha)
      setSenhaAtual('')
      setNovaSenha('')
      setConfirmarSenha('')
      avisar(
        resposta?.sessoesRevogadas > 0
          ? `Senha alterada. ${resposta.sessoesRevogadas} outra(s) sessão(ões) encerrada(s).`
          : 'Senha alterada',
      )
      carregarSessoes()
    } catch (falha) {
      const codigo = falha?.corpo?.erro
      avisar(
        codigo === 'senha_atual_invalida' ? 'A senha atual está incorreta.' : 'Não foi possível trocar a senha.',
        'erro',
      )
    }
  }

  async function iniciar2fa(evento) {
    evento.preventDefault()
    try {
      const dados = await api.iniciar2fa(senha2fa)
      setDoisFatores(dados)
      setSenha2fa('')
      avisar('Segredo gerado. Confirme com o código do aplicativo.')
    } catch (falha) {
      avisar(
        falha?.corpo?.erro === 'senha_incorreta'
          ? 'Senha incorreta.'
          : falha?.corpo?.erro === '2fa_ja_ativo'
            ? 'A verificação em duas etapas já está ativa.'
            : 'Não foi possível iniciar a ativação.',
        'erro',
      )
    }
  }

  async function confirmar2fa(evento) {
    evento.preventDefault()
    try {
      await api.ativar2fa(codigo2fa)
      setDoisFatores(null)
      setCodigo2fa('')
      avisar('Verificação em duas etapas ativada')
      aoAtualizarPerfil()
    } catch {
      avisar('Código inválido. Confira o horário do aparelho e tente de novo.', 'erro')
    }
  }

  async function desativar2fa(evento) {
    evento.preventDefault()
    try {
      await api.desativar2fa(senha2fa, codigo2fa)
      setDesativando2fa(false)
      setSenha2fa('')
      setCodigo2fa('')
      avisar('Verificação em duas etapas desligada')
      aoAtualizarPerfil()
    } catch {
      avisar('Não foi possível desativar: confira a senha e o código.', 'erro')
    }
  }

  async function encerrarSessao(id) {
    try {
      await api.encerrarSessao(id)
      avisar('Sessão encerrada')
      carregarSessoes()
    } catch {
      avisar('Não foi possível encerrar a sessão.', 'erro')
    }
  }

  async function encerrarOutras() {
    try {
      await api.encerrarOutrasSessoes()
      avisar('Todas as outras sessões foram encerradas')
      carregarSessoes()
    } catch {
      avisar('Não foi possível encerrar as sessões.', 'erro')
    }
  }

  async function exportar() {
    setExportando(true)
    try {
      const dados = await api.exportar()
      const blob = new Blob([JSON.stringify(dados, null, 2)], { type: 'application/json' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `bsfm-dados-${new Date().toISOString().slice(0, 10)}.json`
      document.body.appendChild(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
      avisar('Arquivo com seus dados gerado')
    } catch {
      avisar('Não foi possível exportar agora.', 'erro')
    } finally {
      setExportando(false)
    }
  }

  async function copiar(texto, mensagem) {
    try {
      await navigator.clipboard.writeText(texto)
      avisar(mensagem)
    } catch {
      avisar(`Copie manualmente: ${texto}`, 'erro')
    }
  }

  async function excluirConta(evento) {
    evento.preventDefault()
    setExcluindoConta(true)
    try {
      await api.excluirConta(senhaExclusao, confirmacaoExclusao)
      avisar('Conta e dados excluídos')
      aoSair()
    } catch (falha) {
      const codigo = falha?.corpo?.erro
      avisar(
        codigo === 'senha_incorreta'
          ? 'Senha incorreta. A conta não foi excluída.'
          : codigo === 'confirmacao_invalida'
            ? 'Digite EXCLUIR para confirmar.'
            : 'Não foi possível excluir a conta agora.',
        'erro',
      )
    } finally {
      setExcluindoConta(false)
    }
  }

  function localizar() {
    if (!navigator.geolocation) {
      avisar('Este navegador não informa a localização.', 'erro')
      return
    }
    setBuscandoLocal(true)
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setPosicao({ lat: pos.coords.latitude, lng: pos.coords.longitude })
        setBuscandoLocal(false)
        avisar('Localização obtida — unidades ordenadas por distância')
      },
      () => {
        setBuscandoLocal(false)
        avisar('Permissão de localização negada. A lista continua disponível.', 'erro')
      },
      { enableHighAccuracy: true, timeout: 9000 },
    )
  }

  
  const outrasSessoes = sessoes.filter((s) => !s.revogadaEm && !s.atual)
  const sessoesAtivas = sessoes.filter((s) => !s.revogadaEm)

  return (
    <div className="pagina pagina-estreita">
      <section className="cartao cartao-perfil">
        <div className="perfil-identidade">
          <div className="avatar avatar-grande">{iniciais(usuario?.nome)}</div>
          <div className="perfil-quem">
            <span className="olho">Sua conta</span>
            <h2>{usuario?.nome}</h2>
            <p>
              {usuario?.email}
              {usuario?.emailVerificado ? (
                <Selo tom="ok" icone="check">verificado</Selo>
              ) : (
                <Selo tom="aviso">e-mail pendente</Selo>
              )}
            </p>
            <small>No BSFM desde {usuario?.criadoEm ? dataCompleta(usuario.criadoEm) : '—'}</small>
          </div>
        </div>

        <div className="perfil-stats">
          <div><strong>{numero(usuario?.imc, 1)}</strong><span>IMC</span></div>
          <div><strong>{numero(usuario?.tmb)}</strong><span>TMB</span></div>
          <div><strong>{numero(usuario?.gastoTotal)}</strong><span>gasto</span></div>
          <div><strong>{usuario?.idade ?? '—'}</strong><span>anos</span></div>
        </div>
      </section>

      <nav className="perfil-nav" aria-label="Seções do perfil">
        {SECOES.map(([id, rotulo]) => (
          <button
            key={id}
            className="chip"
            onClick={() => document.getElementById(`perfil-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
          >
            {rotulo}
          </button>
        ))}
      </nav>

      <section className="cartao" id="perfil-conta">
        <div className="cartao-cabeca">
          <div><span className="olho">Dados pessoais</span><h3>Conta</h3></div>
          {usuario?.totpHabilitado && <Selo tom="ok" icone="cadeado">2FA ativo</Selo>}
        </div>

        <form onSubmit={salvarNome} className="form-linha">
          <div className="campo">
            <label htmlFor="nomePerfil">Nome</label>
            <input id="nomePerfil" value={conta.nome} onChange={atualizarConta('nome')} required minLength={3} />
          </div>
          <button className="btn btn-fantasma btn-mini" type="submit">Salvar nome</button>
        </form>

        <form onSubmit={salvarNascimento} className="form-linha">
          <div className="campo">
            <label htmlFor="nascimento">Data de nascimento</label>
            <input id="nascimento" type="date" value={conta.nascimento} onChange={atualizarConta('nascimento')} required />
            <span className="campo-dica">A idade usada no TMB passa a ser derivada desta data.</span>
          </div>
          <button className="btn btn-fantasma btn-mini" type="submit">Salvar data</button>
        </form>

        <form onSubmit={salvarEmail} className="form-linha">
          <div className="campo">
            <label htmlFor="emailPerfil">E-mail</label>
            <input id="emailPerfil" type="email" value={conta.email} onChange={atualizarConta('email')} required />
          </div>
          <div className="campo">
            <label htmlFor="senhaEmail">Senha atual</label>
            <input
              id="senhaEmail"
              type="password"
              value={senhaEmail}
              onChange={(e) => setSenhaEmail(e.target.value)}
              autoComplete="current-password"
              required
            />
          </div>
          <button className="btn btn-fantasma btn-mini" type="submit">Salvar e-mail</button>
        </form>
      </section>

      <section className="cartao" id="perfil-seguranca">
        <div className="cartao-cabeca">
          <div><span className="olho">Proteção</span><h3>Segurança</h3></div>
          <Selo tom={usuario?.totpHabilitado ? 'ok' : 'neutro'}>
            {usuario?.totpHabilitado ? '2FA ativo' : '2FA desligado'}
          </Selo>
        </div>

        <form onSubmit={trocarSenha}>
          <div className="linha-campos">
            <div className="campo">
              <label htmlFor="senhaAtual">Senha atual</label>
              <input id="senhaAtual" type="password" value={senhaAtual} onChange={(e) => setSenhaAtual(e.target.value)} autoComplete="current-password" required />
            </div>
            <div className="campo">
              <label htmlFor="novaSenha">Nova senha</label>
              <input id="novaSenha" type="password" value={novaSenha} onChange={(e) => setNovaSenha(e.target.value)} autoComplete="new-password" minLength={8} required />
            </div>
          </div>
          <div className="linha-campos">
            <div className="campo">
              <label htmlFor="confirmarSenha">Confirmar nova senha</label>
              <input id="confirmarSenha" type="password" value={confirmarSenha} onChange={(e) => setConfirmarSenha(e.target.value)} autoComplete="new-password" minLength={8} required />
            </div>
            <div className="campo campo-acao">
              <button className="btn btn-fantasma" type="submit">Trocar senha</button>
            </div>
          </div>
          <p className="rodape-cartao">Trocar a senha encerra as outras sessões — a atual continua válida.</p>
        </form>

        <div className="divisor" />

        {!usuario?.totpHabilitado && !doisFatores && (
          <form className="form-2fa" onSubmit={iniciar2fa}>
            <div>
              <strong>Ativar verificação em duas etapas</strong>
              <p className="rodape-cartao">
                Use um aplicativo autenticador (Google Authenticator, 1Password, Aegis…). A senha é
                necessária para começar.
              </p>
            </div>
            <div className="campo">
              <label htmlFor="senha2fa">Senha atual</label>
              <input id="senha2fa" type="password" value={senha2fa} onChange={(e) => setSenha2fa(e.target.value)} autoComplete="current-password" required />
            </div>
            <button className="btn btn-mini" type="submit">Começar ativação</button>
          </form>
        )}

        {!usuario?.totpHabilitado && doisFatores && (
          <div className="form-2fa">
            <div>
              <strong>Confirme o código</strong>
              <p className="rodape-cartao">
                Adicione a conta no autenticador com o segredo abaixo e digite o código de 6 dígitos.
              </p>
            </div>

            <div className="segredo">
              <code>{doisFatores.segredo}</code>
              <button type="button" className="btn btn-fantasma btn-mini" onClick={() => copiar(doisFatores.segredo, 'Segredo copiado')}>
                <Icone nome="copiar" tamanho={14} /> Copiar
              </button>
            </div>

            <div className="acoes">
              <a className="btn btn-fantasma btn-mini" href={doisFatores.otpauth}>Abrir no autenticador</a>
              <span className="rodape-cartao">Emissor: {doisFatores.emissor} · Conta: {doisFatores.conta}</span>
            </div>

            <form onSubmit={confirmar2fa} className="form-linha">
              <div className="campo">
                <label htmlFor="codigoAtivar">Código de 6 dígitos</label>
                <input
                  id="codigoAtivar"
                  inputMode="numeric"
                  maxLength={6}
                  value={codigo2fa}
                  onChange={(e) => setCodigo2fa(e.target.value.replace(/\D/g, ''))}
                  className="campo-codigo"
                  required
                />
              </div>
              <button className="btn" type="submit" disabled={codigo2fa.length !== 6}>Ativar 2FA</button>
              <button type="button" className="btn btn-fantasma" onClick={() => { setDoisFatores(null); setCodigo2fa('') }}>
                Cancelar
              </button>
            </form>
          </div>
        )}

        {usuario?.totpHabilitado && !desativando2fa && (
          <div className="form-2fa-ativo">
            <div>
              <strong>Verificação em duas etapas ativa</strong>
              <p className="rodape-cartao">
                No próximo login, além da senha, pediremos o código do autenticador.
              </p>
            </div>
            <button className="btn btn-perigo btn-mini" onClick={() => setDesativando2fa(true)}>Desativar</button>
          </div>
        )}

        {usuario?.totpHabilitado && desativando2fa && (
          <form className="form-2fa" onSubmit={desativar2fa}>
            <div>
              <strong>Desativar verificação em duas etapas</strong>
              <p className="rodape-cartao">Confirme com a senha e um código atual.</p>
            </div>
            <div className="linha-campos">
              <div className="campo">
                <label htmlFor="senhaDesativar">Senha</label>
                <input id="senhaDesativar" type="password" value={senha2fa} onChange={(e) => setSenha2fa(e.target.value)} required />
              </div>
              <div className="campo">
                <label htmlFor="codigoDesativar">Código</label>
                <input id="codigoDesativar" inputMode="numeric" maxLength={6} value={codigo2fa} onChange={(e) => setCodigo2fa(e.target.value.replace(/\D/g, ''))} required />
              </div>
            </div>
            <div className="acoes">
              <button className="btn btn-perigo" type="submit">Desativar 2FA</button>
              <button type="button" className="btn btn-fantasma" onClick={() => { setDesativando2fa(false); setSenha2fa(''); setCodigo2fa('') }}>
                Cancelar
              </button>
            </div>
          </form>
        )}

        <div className="divisor" />

        <div className="cartao-cabeca">
          <div><span className="olho">Dispositivos</span><h3>Sessões</h3></div>
          {outrasSessoes.length > 0 && (
            <button className="btn btn-fantasma btn-mini" onClick={encerrarOutras}>
              Encerrar as outras ({outrasSessoes.length})
            </button>
          )}
        </div>

        {sessoesAtivas.length === 0 ? (
          <p className="rodape-cartao">Nenhuma sessão ativa listada.</p>
        ) : (
          <ul className="lista lista-compacta">
            {sessoes.map((s) => (
              <li className="item" key={s.id}>
                <div>
                  <div className="item-titulo">
                    {s.atual ? 'Este dispositivo' : 'Outro dispositivo'}
                    {s.revogadaEm && <Selo>encerrada</Selo>}
                  </div>
                  <div className="item-meta">
                    desde {dataCurta(s.criadaEm)}
                    {s.ultimoUsoEm ? ` · último uso ${dataCurta(s.ultimoUsoEm)}` : ''}
                  </div>
                </div>
                {!s.revogadaEm && !s.atual && (
                  <button className="btn btn-fantasma btn-mini" onClick={() => encerrarSessao(s.id)}>
                    Encerrar
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="cartao" id="perfil-saude">
        <div className="cartao-cabeca">
          <div><span className="olho">Contexto clínico</span><h3>Perfil de saúde</h3></div>
          {usuario?.saudeCompleta ? <Selo tom="ok" icone="check">completo</Selo> : <Selo tom="aviso">incompleto</Selo>}
        </div>
        <p className="rodape-cartao" style={{ marginTop: 0 }}>
          Usado para adaptar o feedback alimentar. Fica guardado com consentimento LGPD registrado.
        </p>

        <form onSubmit={salvarSaude}>
          <div className="linha-campos">
            <div className="campo">
              <label htmlFor="diabetes">Diabetes</label>
              <select id="diabetes" value={conta.diabetes} onChange={atualizarConta('diabetes')}>
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
                value={conta.intolerancia}
                onChange={atualizarConta('intolerancia')}
                placeholder="Ex: lactose, glúten"
                maxLength={400}
              />
            </div>
          </div>
          <button className="btn btn-fantasma btn-mini" type="submit">Salvar saúde</button>
        </form>

        <div className="divisor" />

        <form onSubmit={salvarMedidas}>
          <div className="cartao-cabeca">
            <div><span className="olho">Antropometria</span><h4>Peso e altura</h4></div>
          </div>
          <div className="linha-campos">
            <div className="campo">
              <label htmlFor="pesoPerfil">Peso (kg)</label>
              <input id="pesoPerfil" type="number" step="0.1" min="20" max="400" value={conta.peso} onChange={atualizarConta('peso')} required />
            </div>
            <div className="campo">
              <label htmlFor="alturaPerfil">Altura (m)</label>
              <input id="alturaPerfil" type="number" step="0.01" min="0.8" max="2.5" value={conta.altura} onChange={atualizarConta('altura')} required />
            </div>
            <div className="campo campo-acao">
              <button className="btn btn-fantasma" type="submit">Recalcular</button>
            </div>
          </div>
        </form>
      </section>

      <section className="cartao" id="perfil-privacidade">
        <div className="cartao-cabeca">
          <div><span className="olho">LGPD</span><h3>Privacidade e dados</h3></div>
          <Selo tom="ok" icone="escudo">consentimento registrado</Selo>
        </div>
        <p className="rodape-cartao" style={{ marginTop: 0 }}>
          Você pode exportar tudo em JSON, ler os termos completos e apagar registros individualmente
          no diário. A exclusão da conta é feita pelo contato do projeto.
        </p>
        <div className="acoes">
          <button className="btn btn-fantasma btn-mini" onClick={exportar} disabled={exportando}>
            <Icone nome="baixar" tamanho={15} /> {exportando ? 'Gerando…' : 'Baixar meus dados'}
          </button>
          <button className="btn btn-fantasma btn-mini" onClick={() => setTermosAberto(true)}>
            <Icone nome="livro" tamanho={15} /> Ler termos completos
          </button>
        </div>

        <div className="zona-perigo">
          <div>
            <strong>Excluir minha conta</strong>
            <p className="rodape-cartao">
              Remove conta, refeições, água, metas, análises e sessões. A ação não pode ser desfeita.
            </p>
          </div>

          {!excluindo ? (
            <button className="btn btn-perigo btn-mini" onClick={() => setExcluindo(true)}>
              Excluir conta
            </button>
          ) : (
            <form onSubmit={excluirConta} className="form-exclusao">
              <div className="linha-campos">
                <div className="campo">
                  <label htmlFor="senhaExclusao">Senha atual</label>
                  <input
                    id="senhaExclusao"
                    type="password"
                    value={senhaExclusao}
                    onChange={(e) => setSenhaExclusao(e.target.value)}
                    required
                    autoComplete="current-password"
                  />
                </div>
                <div className="campo">
                  <label htmlFor="confirmacaoExclusao">Digite EXCLUIR</label>
                  <input
                    id="confirmacaoExclusao"
                    value={confirmacaoExclusao}
                    onChange={(e) => setConfirmacaoExclusao(e.target.value)}
                    placeholder="EXCLUIR"
                    required
                  />
                </div>
              </div>
              <div className="acoes">
                <button className="btn btn-perigo btn-mini" type="submit" disabled={excluindoConta}>
                  {excluindoConta ? 'Excluindo…' : 'Confirmar exclusão'}
                </button>
                <button
                  type="button"
                  className="btn btn-fantasma btn-mini"
                  onClick={() => {
                    setExcluindo(false)
                    setSenhaExclusao('')
                    setConfirmacaoExclusao('')
                  }}
                >
                  Manter conta
                </button>
              </div>
            </form>
          )}
        </div>
      </section>

      <Emergencias posicao={posicao} localizar={localizar} buscandoLocal={buscandoLocal} />

      <section className="cartao" id="perfil-app">
        <div className="cartao-cabeca">
          <div><span className="olho">Experiência</span><h3>Aplicativo</h3></div>
          {instalado ? <Selo tom="ok" icone="check">instalado</Selo> : <Selo>PWA</Selo>}
        </div>
        <p className="rodape-cartao" style={{ marginTop: 0 }}>
          {instalado
            ? 'Este dispositivo já usa a versão instalada do BSFM.'
            : 'Instale para abrir em tela cheia, com ícone próprio e funcionamento offline.'}
        </p>
        <div className="acoes">
          {!instalado && (
            <button className="btn" onClick={instalar} disabled={!podeInstalar}>
              <Icone nome="instalar" tamanho={17} />
              {podeInstalar ? 'Instalar aplicativo' : 'Use o menu do navegador'}
            </button>
          )}
          <button className="btn btn-fantasma" onClick={alternarTema}>
            <Icone nome={tema === 'escuro' ? 'sol' : 'lua'} tamanho={17} />
            Tema {tema === 'escuro' ? 'claro' : 'escuro'}
          </button>
        </div>
      </section>

      <button className="btn btn-perigo btn-bloco" onClick={sair}>
        <Icone nome="sair" tamanho={17} /> Sair da conta
      </button>

      <ModalTermos aberto={termosAberto} aoFechar={() => setTermosAberto(false)} />
    </div>
  )
}
