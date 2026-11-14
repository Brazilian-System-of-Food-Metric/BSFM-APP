import { Icone } from './Icones.jsx'

/* Utilidades de apresentação compartilhadas pelas telas. */

export const numero = (valor, casas = 0) =>
  (valor ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas })

export const dataCurta = (iso) =>
  new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })

export const dataLonga = (iso) =>
  new Date(iso).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' })

export const dataCompleta = (iso) =>
  new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: 'long', year: 'numeric' })

export const iniciais = (nome = '') =>
  nome
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? '')
    .join('') || '?'

export const primeiroNome = (nome = '') => nome.trim().split(/\s+/)[0] ?? ''

export function saudacao() {
  const hora = new Date().getHours()
  if (hora < 12) return 'Bom dia'
  if (hora < 18) return 'Boa tarde'
  return 'Boa noite'
}

export function hojePorExtenso() {
  return new Date().toLocaleDateString('pt-BR', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
  })
}

/** Anel de progresso do dia (calorias). */
export function Donut({ percentual = 0, valor, unidade, rotulo, tamanho = 138 }) {
  const raio = 56
  const circunferencia = 2 * Math.PI * raio
  const preenchido = Math.min(100, Math.max(0, percentual)) / 100
  const excedeu = percentual > 100

  return (
    <div className="donut" style={{ width: tamanho, height: tamanho }}>
      <svg viewBox="0 0 140 140" width={tamanho} height={tamanho}>
        <circle cx="70" cy="70" r={raio} fill="none" stroke="var(--surface-3)" strokeWidth="12" />
        {percentual > 0 && (
          <circle
            cx="70"
            cy="70"
            r={raio}
            fill="none"
            stroke={excedeu ? 'var(--aviso)' : 'var(--acento)'}
            strokeWidth="12"
            strokeLinecap="round"
            strokeDasharray={`${circunferencia * preenchido} ${circunferencia}`}
          />
        )}
      </svg>
      <div className="donut-centro">
        <strong>{valor}</strong>
        <small>{unidade}</small>
        {rotulo && <small className="donut-rotulo">{rotulo}</small>}
      </div>
    </div>
  )
}

/** Pontuação de 0 a 10 em anel compacto. */
export function Anel({ pontuacao = 0, tamanho = 44 }) {
  const cor = pontuacao >= 8 ? 'var(--acento)' : pontuacao >= 5 ? 'var(--aviso)' : 'var(--perigo)'
  return (
    <span
      className="anel"
      style={{
        width: tamanho,
        height: tamanho,
        ['--p']: `${Math.max(0, Math.min(10, pontuacao)) * 10}%`,
        ['--cor']: cor,
      }}
      aria-label={`Pontuação ${pontuacao} de 10`}
    >
      <b>{pontuacao}</b>
    </span>
  )
}

export function Vazio({ icone = 'info', titulo, children, acao }) {
  return (
    <div className="vazio">
      <span className="vazio-icone"><Icone nome={icone} tamanho={22} /></span>
      {titulo && <strong>{titulo}</strong>}
      {children && <p>{children}</p>}
      {acao}
    </div>
  )
}

export function Barra({ percentual = 0, alerta = false }) {
  return (
    <div className={`barra${alerta ? ' alerta' : ''}`}>
      <span style={{ width: `${Math.min(100, Math.max(0, percentual))}%` }} />
    </div>
  )
}

export function Selo({ children, tom = 'neutro', icone }) {
  return (
    <span className={`tag tag-${tom}`}>
      {icone && <Icone nome={icone} tamanho={12} />}
      {children}
    </span>
  )
}
