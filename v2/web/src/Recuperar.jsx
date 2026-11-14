import { useState } from 'react'
import { ErroApi, api } from './api.js'

/* ============================================================================
   Recuperação de senha em dois passos — exatamente o fluxo da v1:
   e-mail → código de 6 dígitos → nova senha.
   ========================================================================== */

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
    <div className="acesso">
      <form className="cartao-acesso" onSubmit={etapa === 'email' ? pedirCodigo : redefinir}>
        <div className="marca">
          <div className="marca-simbolo">BS</div>
          <div className="marca-texto">
            <span className="marca-nome">BSFM</span>
            <span className="marca-tag">Recuperar acesso</span>
          </div>
        </div>

        <h1>{etapa === 'email' ? 'Esqueci minha senha' : 'Definir nova senha'}</h1>
        <p className="subtitulo">
          {etapa === 'email'
            ? 'Informe o e-mail da conta. Enviaremos um código de 6 dígitos.'
            : `Digite o código enviado para ${email} e escolha a nova senha.`}
        </p>

        {erro && <div className="erro" role="alert">{erro}</div>}
        {aviso && !erro && <div className="sucesso">{aviso}</div>}

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
                inputMode="numeric"
                maxLength={6}
                value={codigo}
                onChange={(e) => setCodigo(e.target.value.replace(/\D/g, ''))}
                required
                autoFocus
                style={{ letterSpacing: 8, fontSize: 22, textAlign: 'center' }}
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
    </div>
  )
}
