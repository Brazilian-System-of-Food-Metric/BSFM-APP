import { useEffect, useMemo, useState } from 'react'
import Mapa from './Mapa.jsx'
import { Icone } from './Icones.jsx'

/* ============================================================================
   Rede de saúde da Grande São Paulo.

   A base vem do CNES (Cadastro Nacional de Estabelecimentos de Saúde), via API
   de Dados Abertos do SUS, e é servida como JSON estático em /dados/saude-sp.json.
   O arquivo é carregado só quando esta seção abre — não pesa no bundle do app.

   Campos por registro: n=nome, ti=tipo, e=endereço, b=bairro, f=telefone,
   la/long=coordenadas, es=esfera administrativa, sus=atende pelo SUS, mu=município.
   ========================================================================== */

const FILTROS = [
  ['todos', 'Todas'],
  ['Hospital', 'Hospitais'],
  ['UPA', 'UPA'],
  ['Pronto atendimento', 'Pronto atendimento'],
  ['UBS / Posto', 'UBS e postos'],
  ['Policlínica', 'Policlínicas'],
]

function distanciaKm(a, b) {
  const raio = 6371
  const rad = (g) => (g * Math.PI) / 180
  const dLat = rad(b.la - a.lat)
  const dLng = rad(b.lo - a.lng)
  const h =
    Math.sin(dLat / 2) ** 2 + Math.cos(rad(a.lat)) * Math.cos(rad(b.la)) * Math.sin(dLng / 2) ** 2
  return 2 * raio * Math.asin(Math.sqrt(h))
}

const numero = (v, casas = 1) =>
  (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas })

export default function Emergencias({ posicao, localizar, buscandoLocal }) {
  const [base, setBase] = useState(null)
  const [erro, setErro] = useState('')
  const [busca, setBusca] = useState('')
  const [tipo, setTipo] = useState('todos')
  const [soSus, setSoSus] = useState(false)
  const [limite, setLimite] = useState(40)

  useEffect(() => {
    let ativo = true
    fetch('/dados/saude-sp.json')
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
      .then((d) => ativo && setBase(d))
      .catch(() => ativo && setErro('Não foi possível carregar a rede de saúde.'))
    return () => {
      ativo = false
    }
  }, [])

  const filtrados = useMemo(() => {
    if (!base) return []
    const termo = busca.trim().toLowerCase()

    const lista = base.filter((u) => {
      if (tipo !== 'todos' && u.ti !== tipo) return false
      if (soSus && !u.sus) return false
      if (!termo) return true
      return (
        (u.n ?? '').toLowerCase().includes(termo) ||
        (u.b ?? '').toLowerCase().includes(termo) ||
        (u.mu ?? '').toLowerCase().includes(termo) ||
        (u.e ?? '').toLowerCase().includes(termo)
      )
    })

    if (!posicao) return lista
    return lista
      .map((u) => ({ ...u, distancia: distanciaKm(posicao, u) }))
      .sort((a, b) => a.distancia - b.distancia)
  }, [base, busca, tipo, soSus, posicao])

  const noMapa = useMemo(() => filtrados.slice(0, 150), [filtrados])
  const visiveis = filtrados.slice(0, limite)

  return (
    <section className="cartao" id="perfil-emergencias">
      <div className="cartao-cabeca">
        <div>
          <span className="olho">Grande São Paulo</span>
          <h3>Rede de saúde</h3>
          <p className="rodape-cartao" style={{ margin: '4px 0 0' }}>
            {base ? `${base.length} estabelecimentos do CNES` : 'Carregando base do SUS…'}
            {filtrados.length !== (base?.length ?? 0) && ` · ${filtrados.length} no filtro`}
          </p>
        </div>
        <button className="btn btn-fantasma btn-mini" onClick={localizar} disabled={buscandoLocal}>
          <Icone nome="mapa" tamanho={15} /> {buscandoLocal ? 'Localizando…' : 'Usar minha localização'}
        </button>
      </div>

      {erro && <div className="erro" style={{ marginTop: 12 }}>{erro}</div>}

      {base && (
        <>
          <div className="filtros-saude">
            <input
              type="search"
              className="campo-busca"
              placeholder="Buscar por nome, bairro ou cidade…"
              value={busca}
              onChange={(e) => {
                setBusca(e.target.value)
                setLimite(40)
              }}
              aria-label="Buscar estabelecimento de saúde"
            />
            <label className="chip-sus">
              <input type="checkbox" checked={soSus} onChange={(e) => setSoSus(e.target.checked)} />
              Só atendimento SUS
            </label>
          </div>

          <div className="chips" role="tablist" aria-label="Tipo de unidade">
            {FILTROS.map(([id, rotulo]) => (
              <button
                key={id}
                role="tab"
                aria-selected={tipo === id}
                className={`chip${tipo === id ? ' ativo' : ''}`}
                onClick={() => {
                  setTipo(id)
                  setLimite(40)
                }}
              >
                {rotulo}
              </button>
            ))}
          </div>

          <Mapa pontos={noMapa} posicao={posicao} />

          <ul className="lista lista-compacta">
            {visiveis.map((u) => (
              <li className="item" key={`${u.n}-${u.la}-${u.lo}`}>
                <div style={{ minWidth: 0 }}>
                  <div className="item-titulo">
                    {u.n}
                    {u.sus && <span className="tag tag-sus">SUS</span>}
                  </div>
                  <div className="item-meta">
                    {[u.ti, u.e, u.b, u.mu].filter(Boolean).join(' · ')}
                    {u.distancia != null && ` · ${numero(u.distancia, 1)} km`}
                  </div>
                </div>
                <div className="acoes">
                  {u.f && (
                    <a className="btn btn-fantasma btn-mini" href={`tel:${String(u.f).replace(/\D/g, '')}`}>
                      <Icone nome="telefone" tamanho={13} /> Ligar
                    </a>
                  )}
                  <a
                    className="btn btn-fantasma btn-mini"
                    href={`https://waze.com/ul?ll=${u.la},${u.lo}&navigate=yes`}
                    target="_blank"
                    rel="noreferrer"
                  >
                    Waze
                  </a>
                </div>
              </li>
            ))}
          </ul>

          {visiveis.length === 0 && (
            <div className="vazio" style={{ marginTop: 12 }}>
              <strong>Nenhum resultado</strong>
              Tente outro termo ou remova o filtro de tipo.
            </div>
          )}

          {filtrados.length > visiveis.length && (
            <button className="btn btn-fantasma btn-bloco" style={{ marginTop: 12 }} onClick={() => setLimite((l) => l + 60)}>
              Mostrar mais ({(filtrados.length - visiveis.length).toLocaleString('pt-BR')} restantes)
            </button>
          )}
        </>
      )}

      <p className="rodape-cartao" style={{ marginTop: 14 }}>
        Fonte: CNES / Ministério da Saúde (Dados Abertos do SUS). Em emergência ligue 192 (SAMU).
        Esta lista é referência e não substitui atendimento.
      </p>
    </section>
  )
}
