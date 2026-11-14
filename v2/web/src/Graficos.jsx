/* ============================================================================
   Gráficos em SVG puro — sem biblioteca, sem dependência de CDN.
   Precisam ser leves, acessíveis e legíveis nos dois temas.
   ========================================================================== */

const numero = (v, casas = 1) =>
  (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas })

/** Linha de evolução (peso, IMC). */
export function GraficoLinha({ pontos = [], unidade = '', cor = 'var(--acento)', altura = 150 }) {
  if (pontos.length === 0) {
    return (
      <div className="vazio" style={{ padding: '24px 16px' }}>
        <strong>Sem dados ainda</strong>
        Registre uma medição para começar a série.
      </div>
    )
  }

  // Com um único ponto não existe linha: mostrar o valor é mais honesto que
  // desenhar uma reta horizontal com mínimo e máximo iguais.
  if (pontos.length === 1) {
    const unico = pontos[0]
    return (
      <div className="grafico-ponto">
        <strong>{numero(unico.valor)}{unidade ? ` ${unidade === 'peso' ? 'kg' : ''}` : ''}</strong>
        <span>{pontoLabel(unico)} · primeiro registro</span>
      </div>
    )
  }

  const largura = 560
  const margem = { topo: 12, base: 22, esq: 34, dir: 12 }
  const valores = pontos.map((p) => p.valor)
  const min = Math.min(...valores)
  const max = Math.max(...valores)
  const faixa = max - min || 1
  const alturaUtil = altura - margem.topo - margem.base
  const larguraUtil = largura - margem.esq - margem.dir

  const x = (i) => margem.esq + (pontos.length === 1 ? larguraUtil / 2 : (i / (pontos.length - 1)) * larguraUtil)
  const y = (v) => margem.topo + alturaUtil - ((v - min) / faixa) * alturaUtil

  const caminho = pontos.map((p, i) => `${i === 0 ? 'M' : 'L'} ${x(i).toFixed(1)} ${y(p.valor).toFixed(1)}`).join(' ')
  const area = `${caminho} L ${x(pontos.length - 1).toFixed(1)} ${margem.topo + alturaUtil} L ${x(0).toFixed(1)} ${margem.topo + alturaUtil} Z`

  return (
    <figure className="grafico" style={{ margin: 0 }}>
      <svg viewBox={`0 0 ${largura} ${altura}`} role="img"
        aria-label={`Evolução de ${unidade || 'valores'}: de ${numero(min)} a ${numero(max)}`}>
        {[0, 0.5, 1].map((fracao) => (
          <line
            key={fracao}
            x1={margem.esq}
            x2={largura - margem.dir}
            y1={margem.topo + alturaUtil * fracao}
            y2={margem.topo + alturaUtil * fracao}
            stroke="var(--borda)"
            strokeDasharray="3 4"
          />
        ))}

        <path d={area} fill={cor} opacity="0.10" />
        <path d={caminho} fill="none" stroke={cor} strokeWidth="2.2" strokeLinejoin="round" strokeLinecap="round" />

        {pontos.map((p, i) => (
          <circle key={i} cx={x(i)} cy={y(p.valor)} r="3.4" fill={cor} />
        ))}

        <text x={margem.esq - 6} y={margem.topo + 4} textAnchor="end" fontSize="10" fill="var(--texto-fraco)">
          {numero(max)}
        </text>
        <text x={margem.esq - 6} y={margem.topo + alturaUtil} textAnchor="end" fontSize="10" fill="var(--texto-fraco)">
          {numero(min)}
        </text>
        <text x={margem.esq} y={altura - 6} fontSize="10" fill="var(--texto-fraco)">
          {pontoLabel(pontos[0])}
        </text>
        {pontos.length > 1 && (
          <text x={largura - margem.dir} y={altura - 6} textAnchor="end" fontSize="10" fill="var(--texto-fraco)">
            {pontoLabel(pontos[pontos.length - 1])}
          </text>
        )}
      </svg>
    </figure>
  )
}

function pontoLabel(ponto) {
  if (!ponto?.data) return ''
  return new Date(ponto.data).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })
}

/** Barras de hidratação por dia. */
export function GraficoBarras({ barras = [], meta = 0, altura = 130 }) {
  if (barras.length === 0) {
    return <div className="rodape-cartao">Sem registros na semana.</div>
  }

  const maximo = Math.max(meta, ...barras.map((b) => b.valor), 1)

  return (
    <div className="grafico-barras" role="img" aria-label="Consumo de água nos últimos sete dias">
      {barras.map((b, i) => {
        const percentual = Math.min(100, (b.valor / maximo) * 100)
        const bateu = meta > 0 && b.valor >= meta

        return (
          <div className="barra-dia" key={i}>
            <div className="barra-dia-trilho" style={{ height: altura }}>
              <div
                className={`barra-dia-valor${bateu ? ' meta' : ''}`}
                style={{ height: `${percentual}%` }}
                title={`${numero(b.valor, 0)} ml`}
              />
            </div>
            <span className="barra-dia-rotulo">{b.rotulo}</span>
          </div>
        )
      })}
    </div>
  )
}
