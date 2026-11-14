/* ============================================================================
   Landing pública do BSFM.
   É a porta de entrada: apresenta o projeto, permite instalar o app e leva ao
   login. Existe separada do aplicativo para o site institucional não exigir
   conta — igual à landing original.
   ========================================================================== */

const Icone = ({ d, tamanho = 22 }) => (
  <svg width={tamanho} height={tamanho} viewBox="0 0 24 24" fill="none" stroke="currentColor"
    strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d={d} />
  </svg>
)

// Basta colar a URL de um vídeo (mp4) ou de um embed do YouTube para trocar a
// galeria de capturas pelo vídeo. Vazio = mostra as telas reais do app.
const VIDEO_DEMO = ''

const CAPTURAS = [
  { arquivo: '/demo/painel.png', titulo: 'Painel', descricao: 'Calorias do dia, hidratação e evolução' },
  { arquivo: '/demo/rotulo.png', titulo: 'Rótulo', descricao: 'OCR lido no próprio aparelho' },
  { arquivo: '/demo/metas.png', titulo: 'Metas', descricao: 'Alvos e gráficos de peso e IMC' },
  { arquivo: '/demo/perfil.png', titulo: 'Perfil', descricao: 'Saúde, dispositivos e instalação' },
]

const RECURSOS = [
  {
    titulo: 'Catálogo TACO completo',
    texto: '582 alimentos brasileiros com valores por 100 g. O servidor calcula a porção — o cliente não envia número.',
    icone: 'M4 6h16M4 12h16M4 18h10',
  },
  {
    titulo: 'Análise por foto',
    texto: 'Foto do prato é reconhecida e convertida em nome de alimento; o catálogo devolve os nutrientes.',
    icone: 'M3 8a2 2 0 0 1 2-2h2l1.5-2h7L17 6h2a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8Zm9 9a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z',
  },
  {
    titulo: 'Diário alimentar',
    texto: 'Cada registro com porção, calorias e macros, agrupado por dia para consulta rápida.',
    icone: 'M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01',
  },
  {
    titulo: 'Metas com histórico',
    texto: 'Meta calórica, proteica, de água e de peso. Cada definição fica registrada — dá para ver quando mudou.',
    icone: 'M12 3v3m0 12v3M3 12h3m12 0h3M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z',
  },
  {
    titulo: 'Hidratação',
    texto: 'Registro em um toque, com meta derivada do peso e acompanhamento semanal.',
    icone: 'M12 2.7s6 6.3 6 10.3a6 6 0 0 1-12 0c0-4 6-10.3 6-10.3Z',
  },
  {
    titulo: 'Evolução corporal',
    texto: 'Série histórica de peso e IMC, com variação e distância da meta.',
    icone: 'M3 17l5-6 4 3 5-7 4 4',
  },
]

const PASSOS = [
  { numero: '1', titulo: 'Crie sua conta', texto: 'Nome, e-mail e senha. IMC e gasto calórico são calculados no servidor.' },
  { numero: '2', titulo: 'Registre o que come', texto: 'Busque na base TACO ou envie uma foto do prato.' },
  { numero: '3', titulo: 'Acompanhe a evolução', texto: 'Painel com calorias do dia, água, metas e histórico de peso.' },
]

const DOCS = [
  { titulo: 'Guia do usuário', url: 'https://brazilian-system-of-food-metric.github.io/software-documentation/guia-usuario/introducao.html' },
  { titulo: 'Guia do desenvolvedor', url: 'https://brazilian-system-of-food-metric.github.io/software-documentation/guia-desenvolvedor/index.html' },
  { titulo: 'Roadmap', url: 'https://brazilian-system-of-food-metric.github.io/software-documentation/roadmap/index.html' },
  { titulo: 'Changelog', url: 'https://brazilian-system-of-food-metric.github.io/software-documentation/changelog/index.html' },
]

export default function Landing({ aoEntrar, aoCriarConta, instalar, podeInstalar, instalado }) {
  return (
    <div className="landing">
      <header className="landing-topo">
        <div className="landing-marca">
          <div className="marca-simbolo">BS</div>
          <div className="marca-texto">
            <span className="marca-nome">BSFM</span>
            <span className="marca-tag">Brazilian System of Food Metric</span>
          </div>
        </div>

        <nav className="landing-nav" aria-label="Seções">
          <a href="#telas">Telas</a>
          <a href="#recursos">Recursos</a>
          <a href="#como-funciona">Como funciona</a>
          <a href="#instalar">Aplicativo</a>
          <a href="#documentacao">Documentação</a>
        </nav>

        <div className="landing-acoes">
          {!instalado && (
            <button className="btn btn-fantasma btn-mini" onClick={instalar} disabled={!podeInstalar}>
              Instalar
            </button>
          )}
          <button className="btn btn-mini" onClick={aoEntrar}>Entrar</button>
        </div>
      </header>

      <main>
        <section className="hero">
          <span className="selo">Projeto acadêmico · PIM</span>
          <h1>Nutrição inteligente com dados que fazem sentido</h1>
          <p>
            Registre refeições, acompanhe calorias e hidratação, defina metas e veja sua evolução.
            Os valores vêm da tabela TACO — a mesma referência usada por nutricionistas no Brasil.
          </p>
          <div className="hero-acoes">
            <button className="btn" onClick={aoCriarConta}>Criar conta grátis</button>
            {!instalado && (
              <button className="btn btn-fantasma" onClick={instalar} disabled={!podeInstalar}>
                <Icone d="M12 3v12m0 0 4-4m-4 4-4-4M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" tamanho={18} />
                Instalar aplicativo
              </button>
            )}
          </div>
          <p className="hero-nota">
            {instalado
              ? 'Aplicativo instalado neste dispositivo.'
              : podeInstalar
                ? 'Instalável no celular e no computador. Abre em tela cheia, funciona offline.'
                : 'No iPhone: Compartilhar → Adicionar à Tela de Início.'}
          </p>
        </section>

        <section id="telas" className="bloco">
          <h2>Veja o app por dentro</h2>
          {VIDEO_DEMO ? (
            <div className="video-moldura">
              {VIDEO_DEMO.includes('youtube') || VIDEO_DEMO.includes('youtu.be') ? (
                <iframe
                  src={VIDEO_DEMO}
                  title="Demonstração do BSFM"
                  loading="lazy"
                  allow="accelerometer; autoplay; clipboard-write; encrypted-media; picture-in-picture"
                  allowFullScreen
                />
              ) : (
                <video src={VIDEO_DEMO} controls preload="metadata" />
              )}
            </div>
          ) : (
            <>
              <p>Todas as telas abaixo são do app em produção, não são protótipos.</p>
              <div className="galeria">
                {CAPTURAS.map((c) => (
                  <figure className="captura" key={c.arquivo}>
                    <img src={c.arquivo} alt={`Tela de ${c.titulo}`} loading="lazy" width="560" height="394" />
                    <figcaption>
                      <strong>{c.titulo}</strong>
                      <span>{c.descricao}</span>
                    </figcaption>
                  </figure>
                ))}
              </div>
            </>
          )}
        </section>
<section id="recursos" className="bloco">
          <h2>O que o BSFM faz</h2>
          <div className="grade-recursos">
            {RECURSOS.map((r) => (
              <article className="recurso" key={r.titulo}>
                <span className="recurso-icone"><Icone d={r.icone} /></span>
                <h3>{r.titulo}</h3>
                <p>{r.texto}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="como-funciona" className="bloco">
          <h2>Como funciona</h2>
          <div className="passos">
            {PASSOS.map((p) => (
              <article className="passo" key={p.numero}>
                <span className="passo-numero">{p.numero}</span>
                <h3>{p.titulo}</h3>
                <p>{p.texto}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="instalar" className="bloco bloco-instalar">
          <div>
            <h2>Leve o BSFM no bolso</h2>
            <p>
              O aplicativo é instalável direto do navegador — sem loja, sem download de APK.
              Depois de instalado, abre em tela cheia, com ícone próprio, e o shell continua
              disponível mesmo sem internet. As mesmas funções existem no computador.
            </p>
          </div>
          <div className="instalar-acoes">
            <button className="btn" onClick={instalar} disabled={!podeInstalar || instalado}>
              {instalado ? 'Já instalado' : podeInstalar ? 'Instalar agora' : 'Use o menu do navegador'}
            </button>
            <span className="rodape-cartao">
              {podeInstalar
                ? 'O navegador confirmou que o app é instalável.'
                : 'No iPhone: Compartilhar → Adicionar à Tela de Início.'}
            </span>
          </div>
        </section>

        <section id="numeros" className="bloco">
          <h2>O projeto em números</h2>
          <div className="grade-numeros">
            <div className="numero">
              <strong>582</strong>
              <span>alimentos na base TACO</span>
            </div>
            <div className="numero">
              <strong>281</strong>
              <span>verificações automatizadas</span>
            </div>
            <div className="numero">
              <strong>14</strong>
              <span>tabelas no banco</span>
            </div>
            <div className="numero">
              <strong>100%</strong>
              <span>gratuito e open source</span>
            </div>
          </div>
        </section>

        <section className="bloco">
          <h2>Status do projeto</h2>
          <table className="tabela">
            <thead>
              <tr><th>Módulo</th><th>Estado</th></tr>
            </thead>
            <tbody>
              <tr><td>Cadastro, login e recuperação de senha</td><td><span className="tag tag-ok">pronto</span></td></tr>
              <tr><td>Catálogo TACO (582 alimentos)</td><td><span className="tag tag-ok">pronto</span></td></tr>
              <tr><td>Análise por nome, foto e rótulo</td><td><span className="tag tag-ok">pronto</span></td></tr>
              <tr><td>Diário, metas, planos e hidratação</td><td><span className="tag tag-ok">pronto</span></td></tr>
              <tr><td>Aplicativo instalável (PWA)</td><td><span className="tag tag-ok">pronto</span></td></tr>
              <tr><td>Mapa de unidades de saúde</td><td><span className="tag">em desenvolvimento</span></td></tr>
              <tr><td>Reconhecimento por imagem com IA</td><td><span className="tag">requer chave no servidor</span></td></tr>
            </tbody>
          </table>
        </section>

        <section id="documentacao" className="bloco">
          <h2>Documentação do projeto</h2>
          <div className="grade-docs">
            {DOCS.map((d) => (
              <a className="doc" key={d.titulo} href={d.url} target="_blank" rel="noreferrer">
                <span>{d.titulo}</span>
                <span aria-hidden="true">↗</span>
              </a>
            ))}
          </div>
        </section>

        <section className="bloco bloco-final">
          <h2>Quer conhecer o projeto?</h2>
          <p>Crie sua conta em menos de um minuto e registre a primeira refeição.</p>
          <div className="hero-acoes">
            <button className="btn" onClick={aoCriarConta}>Criar conta</button>
            <button className="btn btn-fantasma" onClick={aoEntrar}>Já tenho conta</button>
          </div>
        </section>
      </main>

      <footer className="landing-rodape">
        <span>BSFM · Brazilian System of Food Metric</span>
        <span>Dados: TACO 4ª ed. (NEPA/UNICAMP)</span>
      </footer>
    </div>
  )
}
