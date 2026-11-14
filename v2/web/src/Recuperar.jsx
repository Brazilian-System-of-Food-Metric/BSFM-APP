import { useState } from 'react'
import { ErroApi, api } from './api.js'
import { Icone } from './Icones.jsx'

/* Recuperação de senha em dois passos — e-mail → código → nova senha.
   O servidor nunca devolve o código; ele chega por e-mail (Brevo). */

export default function Recuperar({ aoVoltar, aoEntrar }) {
  const [etapa, setEtapa] = useState('email')
  const [email, setEmail] = useState('')
  const [codigo, setCodigo] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState('')
  const [aviso, setAviso] = useState('')
  const [carregando, setCarregando] = useState(false)

  async function pedirCodigo(evento) {
    evento.preventDefault()
    setErro('')
    setCarregando(true)
    try {
      await api.esqueciSenha(email.trim())
      setAviso('Se este e-mail existir, enviamos um código de 6 dígitos.')
      setEtapa('codigo')
    } catch (falha) {
      setErro(falha instanceof ErroApi ? falha.message : 'Não foi possível enviar o código.')
    } finally {
      setCarregando(false)
    }
  }

  async function redefinir(evento) {
    evento.preventDefault()
    setErro('')
    setCarregando(true)
    try {
      await api.redefinirSenha(email.trim(), codigo.trim(), senha)
      setAviso('Senha atualizada. Você já pode entrar.')
      setTimeout(() => aoEntrar?.(), 1200)
    } catch (falha) {
      setErro(
        falha?.corpo?.erro === 'codigo_invalido'
          ? 'Código inválido ou expirado. Confira o e-mail.'
          : falha instanceof ErroApi
            ? falha.message
            : 'Não foi possível redefinir.',
      )
    } finally {
      setCarregando(false)
    }
  }

  return (
    <div className="acesso acesso-simples">
      <main className="acesso-painel">
        <form className="cartao-acesso" onSubmit={etapa === 'email' ? pedirCodigo : redefinir}>
          <div className="marca">
            <div className="marca-simbolo">BS</div>
            <div className="marca-texto">
              <span className="marca-nome">BSFM</span>
              <span className="marca-tag">Recuperar acesso</span>
            </div>
          </div>

          <div className="passos-recuperar" aria-hidden="true">
            <span className="passo-bolinha passo-ativo">1</span>
            <span className={`passo-linha${etapa === 'codigo' ? ' passo-linha-ativa' : ''}`} />
            <span className={`passo-bolinha${etapa === 'codigo' ? ' passo-ativo' : ''}`}>2</span>
          </div>

          <h1>{etapa === 'email' ? 'Esqueci minha senha' : 'Definir nova senha'}</h1>
          <p className="subtitulo">
            {etapa === 'email'
              ? 'Informe o e-mail da conta. Enviaremos um código de 6 dígitos.'
              : `Digite o código enviado para ${email} e escolha a nova senha.`}
          </p>

          {erro && <div className="erro" role="alert"><Icone nome="info" tamanho={16} /><span>{erro}</span></div>}
          {aviso && !erro && <div className="sucesso" role="status"><Icone nome="check" tamanho={16} /><span>{aviso}</span></div>}

          {etapa === 'email' ? (
            <>
              <div className="campo">
                <label htmlFor="rec-email">E-mail</label>
                <input
                  id="rec-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  autoComplete="username"
                  autoFocus
                  placeholder="voce@email.com"
                />
              </div>
              <button className="btn btn-bloco" type="submit" disabled={carregando}>
                {carregando ? 'Enviando…' : 'Enviar código'}
              </button>
            </>
          ) : (
            <>
              <div className="campo">
                <label htmlFor="rec-codigo">Código de 6 dígitos</label>
                <input
                  id="rec-codigo"
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
              <div className="campo">
                <label htmlFor="rec-senha">Nova senha (mínimo 8 caracteres)</label>
                <input
                  id="rec-senha"
                  type="password"
                  value={senha}
                  onChange={(e) => setSenha(e.target.value)}
                  required
                  minLength={8}
                  autoComplete="new-password"
                />
              </div>
              <button className="btn btn-bloco" type="submit" disabled={carregando || codigo.length !== 6}>
                {carregando ? 'Salvando…' : 'Redefinir senha'}
              </button>
            </>
          )}

          <button type="button" className="btn btn-fantasma btn-bloco" style={{ marginTop: 10 }} onClick={aoVoltar}>
            ← Voltar para o login
          </button>
        </form>
      </main>
    </div>
  )
}
