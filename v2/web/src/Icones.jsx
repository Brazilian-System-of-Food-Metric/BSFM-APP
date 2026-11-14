/* Ícones em SVG, um só componente. Sem dependência externa e sem fonte de
   ícones: o app continua funcionando offline e o bundle não cresce. */

const CAMINHOS = {
  painel: 'M4 13h6V4H4v9Zm0 7h6v-4H4v4Zm10 0h6v-9h-6v9Zm0-16v4h6V4h-6Z',
  analisar: 'M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm10 2-4.35-4.35',
  diario: 'M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01',
  planos: 'M8 2v4m8-4v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z',
  metas: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm0-5a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm0-3a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z',
  perfil: 'M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z',
  instalar: 'M12 3v12m0 0 4-4m-4 4-4-4M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2',
  sol: 'M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10ZM12 1v2m0 18v2M4.2 4.2l1.4 1.4m12.8 12.8 1.4 1.4M1 12h2m18 0h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4',
  lua: 'M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z',
  sair: 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4m7 14 5-5-5-5m5 5H9',
  agua: 'M12 2.7s6 6.3 6 10.3a6 6 0 0 1-12 0c0-4 6-10.3 6-10.3Z',
  mais: 'M12 5v14M5 12h14',
  menos: 'M5 12h14',
  voltar: 'm15 18-6-6 6-6',
  avancar: 'm9 18 6-6-6-6',
  foto: 'M3 8a2 2 0 0 1 2-2h2l1.5-2h7L17 6h2a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8Zm9 9a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z',
  rotulo: 'M20.6 13.4 12 22 3 13V3h10l7.6 7.6a2 2 0 0 1 0 2.8ZM7.5 7.5h.01',
  cadeado: 'M7 11V8a5 5 0 0 1 10 0v3M5 11h14v10H5z',
  escudo: 'M12 3l8 3v6c0 4.6-3.3 7.9-8 9-4.7-1.1-8-4.4-8-9V6l8-3Z M9 12l2 2 4-4',
  check: 'm5 12 4.5 4.5L19 6',
  x: 'M6 6l12 12M18 6 6 18',
  lixeira: 'M4 7h16M9 7V5h6v2m-8 0 1 13h8l1-13M10 11v6M14 11v6',
  copiar: 'M9 9h11v11H9zM5 15H4V4h11v1',
  baixar: 'M12 3v12m0 0 4-4m-4 4-4-4M4 19h16',
  editar: 'M4 20h4L20 8l-4-4L4 16v4ZM14 6l4 4',
  coracao: 'M20.8 5.6a5 5 0 0 0-7.1 0L12 7.3l-1.7-1.7a5 5 0 1 0-7.1 7.1L12 21l8.8-8.3a5 5 0 0 0 0-7.1Z',
  livro: 'M4 4h6a3 3 0 0 1 3 3v13a2 2 0 0 0-2-2H4V4Zm16 0h-6a3 3 0 0 0-3 3v13a2 2 0 0 1 2-2h7V4Z',
  acessibilidade: 'M12 3a2 2 0 1 0 0 4 2 2 0 0 0 0-4ZM5 8l7 1 7-1M12 9v5m0 0-3 7m3-7 3 7',
  chave: 'M14 7a4 4 0 1 0 3 6.9L21 18v3h-3l-1-1h-2v-2l-3.5-3.5',
  dispositivo: 'M7 2h10a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2Zm4 16h2',
  email: 'M3 6h18v12H3zM3 7l9 6 9-6',
  calendario: 'M8 2v4m8-4v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z',
  mapa: 'M9 3 3 5v16l6-2 6 2 6-2V3l-6 2-6-2Zm0 0v16m6-14v16',
  telefone: 'M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .4 1.9.7 2.8a2 2 0 0 1-.5 2.1L8.1 9.9a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.5c.9.3 1.8.6 2.8.7a2 2 0 0 1 1.7 2Z',
  balanca: 'M12 3v18M5 21h14M6 7h12M6 7 3 13h6L6 7Zm12 0-3 6h6l-3-6Z',
  fogo: 'M12 22c4 0 6-2.7 6-6 0-4-3-6.5-3-9 0 0-2 1.5-2 4 0-1.5-1-3-2-4 0 2.5-5 5-5 9 0 3.3 2 6 6 6Z',
  raio: 'M13 2 4 14h6l-1 8 9-12h-6l1-8Z',
  info: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm0-13h.01M11 12h1v5h1',
  olho: 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Zm10 3a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z',
  olhoOff: 'M3 3l18 18M10.6 10.6a2 2 0 0 0 2.8 2.8M9.9 5.2A10 10 0 0 1 12 5c6.5 0 10 7 10 7a17 17 0 0 1-3 3.8M6.6 6.6A17 17 0 0 0 2 12s3.5 7 10 7a10 10 0 0 0 3.4-.6',
  atualizar: 'M21 12a9 9 0 1 1-2.6-6.4M21 3v6h-6',
  grafico: 'M3 3v18h18M7 14l3-4 3 3 4-6',
  meta: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm0-5a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z',
  comida: 'M12 8c-3 0-5 2-5 5 0 4 3 6 5 6s5-2 5-6c0-3-2-5-5-5Zm0 0c0-3 2-5 4-5M12 8c0-1-1-2-2-2',
  wifiOff: 'M2 2l20 20M8.5 16.5a5 5 0 0 1 7 0M5 12.9a10 10 0 0 1 5.2-2.7M19 12.9a10 10 0 0 0-3.2-2.1M2 8.8a15 15 0 0 1 5.5-3.3M22 8.8a15 15 0 0 0-5.5-3.3M12 20h.01',
}

export function Icone({ nome, tamanho = 20, traco = 1.7, className }) {
  return (
    <svg
      width={tamanho}
      height={tamanho}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={traco}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      className={className}
    >
      <path d={CAMINHOS[nome] ?? CAMINHOS.info} />
    </svg>
  )
}

export { CAMINHOS }
