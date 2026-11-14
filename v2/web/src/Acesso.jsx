import { useMemo, useState } from 'react'
import { ErroApi, api, sessao } from './api.js'
import { Icone } from './Icones.jsx'
import { ModalTermos } from './Termos.jsx'

/* Entrada do app: login, cadastro, verificação de e-mail e segundo fator.
   Layout em duas colunas no desktop, formulário único no celular. */

function medirSenha(senha) {
  if (!senha) return { nivel: 0, rotulo: 'Digite uma senha', cor: 'var(--borda-forte)' }
  let pontos = 0
  if (senha.length >= 8) pontos += 1
  if (senha.length >= 12) pontos += 1
  if (/[A-Z]/.test(senha) && /[a-z]/.test(senha)) pontos += 1
  if (/\d/.test(senha)) pontos += 1
  if (/[^A-Za-z0-9]/.test(senha)) pontos += 1

  const nivel = Math.min(4, pontos)
  const mapa = [
    { rotulo: 'Muito fraca', cor: 'var(--perigo)' },
    { rotulo: 'Fraca', cor: 'var(--perigo)' },
    { rotulo: 'Razoável', cor: 'var(--aviso)' },
    { rotulo: 'Boa', cor: 'var(--info)' },
    { rotulo: 'Forte', cor: 'var(--acento)' },
  ]
  return { nivel: Math.max(1, nivel), ...mapa[Math.max(1, nivel)] }
}

const MENSAGENS = {
  email_ja_cadastrado: 'Este e-mail já tem conta. Use "Entrar".',
  senha_invalida: 'A senha precisa ter pelo menos 8 caracteres.',
  termos_nao_aceitos: 'É necessário aceitar os termos para criar a conta.',
  peso_invalido: 'Informe um peso entre 0 e 500 kg.',
  altura_invalida: 'Informe a altura em metros (ex.: 1,72).',
  nome_invalido: 'Informe seu nome completo.',
  email_invalido: 'Informe um e-mail válido.',
  codigo_invalido: 'Código inválido ou expirado. Confira o e-mail.',
  credenciais_invalidas: 'E-mail ou senha incorretos.',
  senha_incorreta: 'Senha atual incorreta.',
  dados_incompletos: 'Preencha todos os campos.',
}

const mensagemDe = (falha, padrao) => {
  const codigo = falha?.corpo?.erro
  return MENSAGENS[codigo] ?? (falha instanceof ErroApi ? falha.message : padrao)
}

export default function Acesso({
  modoInicial = 'entrar',
  aoEntrar,
  aoRecuperar,
  aoVoltarSite,
  instalar,
  podeInstalar,
  instalado,
  tema,
  alternarTema,
}) {
  const [modo, setModo] = useState(modoInicial === 'cadastro' ? 'cadastro' : 'entrar')
  const [erro, setErro] = useState('')
  const [sucesso, setSucesso] = useState('')
  const [carregando, setCarregando] = useState(false)
  const [codigo, setCodigo] = useState('')
  const [codigo2fa, setCodigo2fa] = useState('')
  const [precisa2fa, setPrecisa2fa] = useState(false)
  const [verSenha, setVerSenha] = useState(false)
  const [termosAberto, setTermosAberto] = useState(false)
  const [form, setForm] = useState({
    nome: '',
    email: '',
    senha: '',
    nascimento: '',
    peso: '',
    altura: '',
    sexo: 'Feminino',
    nivelAtividade: 'Sedentario',
    aceitouTermos: false,
  })

  const forca = useMemo(() => medirSenha(form.senha), [form.senha])

  const atualizar = (campo) => (evento) => {
    const valor = campo === 'aceitouTermos' ? evento.target.checked : evento.target.value
    setForm((atual) => ({ ...atual, [campo]: valor }))
  }

  function trocarModo(novo) {
    setErro('')
    setSucesso('')
    setModo(novo)
  }

  async function entrar(evento) {
    evento.preventDefault()
    setErro('')
    setCarregando(true)
    try {
      const dados = await api.login(form.email.trim(), form.senha, codigo2fa.trim() || null)
      sessao.definir(dados)
      aoEntrar(dados.usuario)
    } catch (falha) {
      const codigoErro = falha?.corpo?.erro
      if (codigoErro === 'totp_necessario' || codigoErro === 'totp_invalido') {
        setPrecisa2fa(true)
        setErro(
          codigoErro === 'totp_necessario'
            ? 'Sua conta usa verificação em duas etapas. Informe o código do aplicativo autenticador.'
            : 'Código de verificação inválido ou expirado. Tente o código atual.',
        )
      } else {
        setErro(mensagemDe(falha, 'Não foi possível entrar.'))
      }
    } finally {
      setCarregando(false)
    }
  }

  async function criar(evento) {
    evento.preventDefault()
    setErro('')
    setSucesso('')
    setCarregando(true)
    try {
      await api.cadastrar({
        nome: form.nome.trim(),
        email: form.email.trim(),
        senha: form.senha,
        dataNascimento: form.nascimento || null,
        peso: Number(form.peso) || null,
        altura: Number(form.altura) || null,
        sexo: form.sexo,
        nivelAtividade: form.nivelAtividade,
        aceitouTermos: form.aceitouTermos,
        versaoTermos: '1.0',
      })
      setSucesso(`Enviamos um código de 6 dígitos para ${form.email.trim()}.`)
      setModo('verificar')
    } catch (falha) {
      setErro(mensagemDe(falha, 'Não foi possível criar a conta.'))
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
      const dados = await api.login(form.email.trim(), form.senha, null)
      sessao.definir(dados)
      aoEntrar(dados.usuario)
    } catch (falha) {
      setErro(mensagemDe(falha, 'Não foi possível verificar o e-mail.'))
    } finally {
      setCarregando(false)
    }
  }

  async function reenviar() {
    setErro('')
    try {
      await api.reenviarVerificacao(form.email.trim())
      setSucesso('Se houver uma conta aguardando verificação, enviamos um novo código.')
    } catch {
      setErro('Não foi possível reenviar agora. Tente em alguns instantes.')
    }
  }

  const enviar = modo === 'entrar' ? entrar : modo === 'cadastro' ? criar : verificar

  const titulo =
    modo === 'entrar' ? 'Entrar na sua conta' : modo === 'cadastro' ? 'Criar conta gratuita' : 'Confirme seu e-mail'

  const subtitulo =
    modo === 'entrar'
      ? 'Continue de onde parou: calorias, água e evolução em um só lugar.'
      : modo === 'cadastro'
        ? 'Leva menos de um minuto. Seus cálculos são feitos no servidor.'
        : `Digite o código de 6 dígitos que enviamos para ${form.email.trim()}.`

  return (
    <div className="acesso">
      <aside className="acesso-lado">
        <div className="marca marca-clara">
          <div className="marca-simbolo">BS</div>
          <div className="marca-texto">
            <span className="marca-nome">BSFM</span>
            <span className="marca-tag">Brazilian System of Food Metric</span>
          </div>
        </div>

        <div className="acesso-chamada">
          <span className="olho olho-claro">Projeto acadêmico · PIM</span>
          <h2>Nutrição clara, com dados que você entende.</h2>
          <p>
            Registre refeições, acompanhe a hidratação e veja a evolução do seu corpo — com a
            tabela TACO por trás de cada número.
          </p>
        </div>

        <ul className="acesso-lista">
          <li><Icone nome="comida" tamanho={18} /> 582 alimentos brasileiros analisados por porção</li>
          <li><Icone nome="grafico" tamanho={18} /> Metas, água e evolução em gráficos legíveis</li>
          <li><Icone nome="escudo" tamanho={18} /> Sessão assinada, LGPD e verificação em duas etapas</li>
        </ul>

        <div className="acesso-rodape">
          <span>TACO 4ª ed. · NEPA/UNICAMP</span>
          <span>Projeto open source</span>
        </div>
      </aside>

      <main className="acesso-painel">
        <div className="acesso-barra">
          <button className="btn btn-fantasma btn-mini" onClick={aoVoltarSite}>
            <Icone nome="voltar" tamanho={15} /> Voltar ao site
          </button>
          <button className="botao-icone" onClick={alternarTema} aria-label="Alternar tema">
            <Icone nome={tema === 'escuro' ? 'sol' : 'lua'} tamanho={18} />
          </button>
        </div>

        <form className="cartao-acesso" onSubmit={enviar}>
          <div className="acesso-marca-mobile marca">
            <div className="marca-simbolo">BS</div>
            <div className="marca-texto">
              <span className="marca-nome">BSFM</span>
              <span className="marca-tag">Métricas nutricionais</span>
            </div>
          </div>

          <h1>{titulo}</h1>
          <p className="subtitulo">{subtitulo}</p>

          {modo !== 'verificar' && (
            <div className="segmentos segmentos-bloco" role="tablist" aria-label="Entrar ou criar conta">
              <button
                type="button"
                role="tab"
                aria-selected={modo === 'entrar'}
                className={`segmento${modo === 'entrar' ? ' ativo' : ''}`}
                onClick={() => trocarModo('entrar')}
              >
                Entrar
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={modo === 'cadastro'}
                className={`segmento${modo === 'cadastro' ? ' ativo' : ''}`}
                onClick={() => trocarModo('cadastro')}
              >
                Criar conta
              </button>
            </div>
          )}

          {erro && (
            <div className="erro" role="alert">
              <Icone nome="info" tamanho={16} /> <span>{erro}</span>
            </div>
          )}
          {sucesso && !erro && (
            <div className="sucesso" role="status">
              <Icone nome="check" tamanho={16} /> <span>{sucesso}</span>
            </div>
          )}

          {modo === 'entrar' && (
            <>
              <div className="campo">
                <label htmlFor="email">E-mail</label>
                <input
                  id="email"
                  type="email"
                  autoComplete="username"
                  value={form.email}
                  onChange={atualizar('email')}
                  placeholder="voce@email.com"
                  required
                />
              </div>

              <div className="campo">
                <label htmlFor="senha">Senha</label>
                <div className="campo-senha">
                  <input
                    id="senha"
                    type={verSenha ? 'text' : 'password'}
                    autoComplete="current-password"
                    value={form.senha}
                    onChange={atualizar('senha')}
                    placeholder="Sua senha"
                    required
                  />
                  <button
                    type="button"
                    className="campo-senha-olho"
                    onClick={() => setVerSenha((v) => !v)}
                    aria-label={verSenha ? 'Ocultar senha' : 'Mostrar senha'}
                  >
                    <Icone nome={verSenha ? 'olhoOff' : 'olho'} tamanho={18} />
                  </button>
                </div>
              </div>

              {precisa2fa && (
                <div className="campo campo-2fa">
                  <label htmlFor="codigo2fa">Código de verificação (2FA)</label>
                  <input
                    id="codigo2fa"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    maxLength={6}
                    value={codigo2fa}
                    onChange={(e) => setCodigo2fa(e.target.value.replace(/\D/g, ''))}
                    placeholder="000000"
                    autoFocus
                  />
                  <span className="campo-dica">Abra o aplicativo autenticador e digite o código de 6 dígitos.</span>
                </div>
              )}

              <button className="btn btn-bloco" type="submit" disabled={carregando}>
                {carregando ? 'Entrando…' : 'Entrar'}
              </button>

              <button type="button" className="link-esqueci" onClick={aoRecuperar}>
                Esqueci minha senha
              </button>
            </>
          )}

          {modo === 'cadastro' && (
            <>
              <div className="campo">
                <label htmlFor="nome">Nome completo</label>
                <input
                  id="nome"
                  value={form.nome}
                  onChange={atualizar('nome')}
                  required
                  minLength={3}
                  autoComplete="name"
                  placeholder="Como quer ser chamado"
                />
              </div>

              <div className="campo">
                <label htmlFor="email-c">E-mail</label>
                <input
                  id="email-c"
                  type="email"
                  value={form.email}
                  onChange={atualizar('email')}
                  required
                  autoComplete="email"
                  placeholder="voce@email.com"
                />
              </div>

              <div className="campo">
                <label htmlFor="senha-c">Senha</label>
                <div className="campo-senha">
                  <input
                    id="senha-c"
                    type={verSenha ? 'text' : 'password'}
                    value={form.senha}
                    onChange={atualizar('senha')}
                    required
                    minLength={8}
                    autoComplete="new-password"
                    placeholder="Mínimo de 8 caracteres"
                  />
                  <button
                    type="button"
                    className="campo-senha-olho"
                    onClick={() => setVerSenha((v) => !v)}
                    aria-label={verSenha ? 'Ocultar senha' : 'Mostrar senha'}
                  >
                    <Icone nome={verSenha ? 'olhoOff' : 'olho'} tamanho={18} />
                  </button>
                </div>
                <div className="forca" aria-hidden="true">
                  {[1, 2, 3, 4].map((n) => (
                    <span
                      key={n}
                      style={{
                        background: n <= forca.nivel ? forca.cor : 'var(--surface-3)',
                      }}
                    />
                  ))}
                </div>
                <span className="campo-dica" style={{ color: forca.cor }}>{forca.rotulo}</span>
              </div>

              <div className="linha-campos">
                <div className="campo">
                  <label htmlFor="nascimento">Nascimento</label>
                  <input
                    id="nascimento"
                    type="date"
                    value={form.nascimento}
                    onChange={atualizar('nascimento')}
                    max={new Date().toISOString().slice(0, 10)}
                    required
                  />
                </div>
                <div className="campo">
                  <label htmlFor="sexo">Sexo</label>
                  <select id="sexo" value={form.sexo} onChange={atualizar('sexo')}>
                    <option>Feminino</option>
                    <option>Masculino</option>
                  </select>
                </div>
              </div>

              <div className="linha-campos">
                <div className="campo">
                  <label htmlFor="peso">Peso (kg)</label>
                  <input
                    id="peso"
                    type="number"
                    inputMode="decimal"
                    step="0.1"
                    min="20"
                    max="400"
                    value={form.peso}
                    onChange={atualizar('peso')}
                    required
                    placeholder="68,5"
                  />
                </div>
                <div className="campo">
                  <label htmlFor="altura">Altura (m)</label>
                  <input
                    id="altura"
                    type="number"
                    inputMode="decimal"
                    step="0.01"
                    min="0.8"
                    max="2.5"
                    value={form.altura}
                    onChange={atualizar('altura')}
                    required
                    placeholder="1,72"
                  />
                </div>
              </div>

              <div className="campo">
                <label htmlFor="ativ">Nível de atividade</label>
                <select id="ativ" value={form.nivelAtividade} onChange={atualizar('nivelAtividade')}>
                  <option value="Sedentario">Sedentário — pouco exercício</option>
                  <option value="Ativo">Ativo — 3 a 5 vezes por semana</option>
                  <option value="Muito Ativo">Muito ativo — todo dia</option>
                </select>
              </div>

              <label className="aceite">
                <input
                  type="checkbox"
                  checked={form.aceitouTermos}
                  onChange={atualizar('aceitouTermos')}
                />
                <span>
                  Li e aceito os{' '}
                  <button type="button" className="link-inline" onClick={() => setTermosAberto(true)}>
                    termos de uso e a política de privacidade
                  </button>
                  , incluindo o tratamento dos meus dados de saúde para cálculo nutricional (LGPD).
                </span>
              </label>

              <button className="btn btn-bloco" type="submit" disabled={carregando || !form.aceitouTermos}>
                {carregando ? 'Criando conta…' : 'Criar conta'}
              </button>

              <p className="acesso-legal">
                Você pode exportar ou apagar seus dados quando quiser, na tela de Perfil.
              </p>
            </>
          )}

          {modo === 'verificar' && (
            <>
              <div className="campo">
                <label htmlFor="codigo">Código de 6 dígitos</label>
                <input
                  id="codigo"
                  className="campo-codigo"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                  value={codigo}
                  onChange={(e) => setCodigo(e.target.value.replace(/\D/g, ''))}
                  required
                  autoFocus
                  placeholder="000000"
                />
              </div>

              <button className="btn btn-bloco" type="submit" disabled={carregando || codigo.length !== 6}>
                {carregando ? 'Verificando…' : 'Verificar e entrar'}
              </button>

              <div className="acesso-duplo">
                <button type="button" className="btn btn-fantasma btn-mini" onClick={reenviar}>
                  Reenviar código
                </button>
                <button type="button" className="btn btn-fantasma btn-mini" onClick={() => trocarModo('entrar')}>
                  Voltar ao login
                </button>
              </div>
            </>
          )}

          <div className="acesso-instalar">
            {instalado ? (
              <span className="tag tag-ok"><Icone nome="check" tamanho={12} /> App instalado</span>
            ) : (
              <button type="button" className="btn btn-fantasma btn-mini" onClick={instalar} disabled={!podeInstalar}>
                <Icone nome="instalar" tamanho={15} />
                {podeInstalar ? 'Instalar aplicativo' : 'Instalar pelo menu do navegador'}
              </button>
            )}
          </div>
        </form>
      </main>

      <ModalTermos aberto={termosAberto} aoFechar={() => setTermosAberto(false)} />
    </div>
  )
}
