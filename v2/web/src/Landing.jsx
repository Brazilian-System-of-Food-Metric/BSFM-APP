import { Icone } from './Icones.jsx'

/* Landing pública. Porta de entrada do projeto: apresenta, prova que funciona,
   explica o roadmap e leva ao cadastro — igual à landing da v1, com o design
   soft clean da v2. */

const VIDEO_DEMO = '' // cole um mp4 ou embed do YouTube para trocar a galeria

const CAPTURAS = [
  { arquivo: '/demo/painel.png?v=3', titulo: 'Painel', descricao: 'Calorias, macros, água e evolução do dia' },
  { arquivo: '/demo/rotulo.png?v=3', titulo: 'Rótulo', descricao: 'OCR local: a imagem não sai do aparelho' },
  { arquivo: '/demo/metas.png?v=3', titulo: 'Metas', descricao: 'Alvos com histórico e gráficos de peso e IMC' },
  { arquivo: '/demo/perfil.png?v=3', titulo: 'Perfil', descricao: 'Saúde, 2FA, dispositivos e privacidade' },
]

const RECURSOS = [
  {
    titulo: 'Catálogo TACO completo',
    texto: '582 alimentos brasileiros com valores por 100 g. Você escolhe a porção e o servidor calcula.',
    icone: 'comida',
  },
  {
    titulo: 'Análise por foto',
    texto: 'A foto do prato é reconhecida e vira nome de alimento; o catálogo devolve os nutrientes.',
    icone: 'foto',
  },
  {
    titulo: 'Rótulo com OCR',
    texto: 'Leitura da tabela nutricional no próprio aparelho, com revisão antes de registrar.',
    icone: 'rotulo',
  },
  {
    titulo: 'Diário com pontuação',
    texto: 'Cada item recebe uma nota de 0 a 10 considerando a sua meta e o seu perfil de saúde.',
    icone: 'diario',
  },
  {
    titulo: 'Metas e evolução',
    texto: 'Calorias, proteína, água e peso. Cada mudança fica no histórico, com gráficos de peso e IMC.',
    icone: 'grafico',
  },
  {
    titulo: 'Hidratação e planos',
    texto: 'Registro de água em um toque e semana alimentar com 7 dias, conclusão de refeição e prescrição por OCR.',
    icone: 'agua',
  },
]

const PASSOS = [
  { numero: '1', titulo: 'Crie sua conta', texto: 'Nome, e-mail, nascimento, peso e altura. IMC, TMB e gasto são calculados no servidor.' },
  { numero: '2', titulo: 'Registre o que come', texto: 'Busque na TACO, fotografe o prato ou leia um rótulo. Confirme a porção.' },
  { numero: '3', titulo: 'Planeje a semana', texto: 'Monte as refeições de cada dia e importe uma prescrição por foto quando tiver uma.' },
  { numero: '4', titulo: 'Acompanhe a evolução', texto: 'Painel com calorias, macros, água, metas e a série histórica de peso e IMC.' },
]

const VALORES = [
  {
    titulo: 'Visão',
    icone: 'olho',
    texto:
      'Democratizar o acesso à informação nutricional de qualidade, combinando tecnologia com dados científicos para tornar o acompanhamento alimentar acessível.',
  },
  {
    titulo: 'Missão',
    icone: 'meta',
    texto:
      'Desenvolver uma ferramenta funcional que permita registrar, analisar e planejar a alimentação de forma intuitiva, com reconhecimento de alimentos.',
  },
  {
    titulo: 'Valores',
    icone: 'coracao',
    texto:
      'Transparência, acessibilidade, inclusão (com VLibras), embasamento científico e melhoria contínua. Tecnologia bem aplicada pode transformar hábitos.',
  },
]

const FEITO = [
  'Cadastro, login, recuperação de senha e verificação em duas etapas',
  'Análise por nome, foto e rótulo com OCR',
  'Diário alimentar com feedback e pontuação por item',
  'Plano alimentar semanal com importação de prescrição',
  'Controle de água com meta e gráfico semanal',
  'Metas com histórico e gráficos de peso e IMC',
  'Perfil de saúde e recomendações adaptadas',
  'Mapa de unidades de saúde com Waze e telefone',
  'Módulo LIBRAS (VLibras) em todas as telas',
  'Tema claro e escuro, aplicativo instalável (PWA)',
]

const PROXIMOS = [
  'Notificações push para refeições e água',
  'Banco de receitas com filtros por objetivo',
  'Integração com wearables',
  'Relatórios exportáveis em PDF',
  'Versão nativa para Android e iOS',
]

const STATUS = [
  ['Cadastro, login, recuperação e 2FA', 'pronto'],
  ['Catálogo TACO (582 alimentos)', 'pronto'],
  ['Análise por nome e rótulo (OCR)', 'pronto'],
  ['Diário, metas, planos e hidratação', 'pronto'],
  ['Perfil de saúde e feedback contextual', 'pronto'],
  ['Mapa de unidades de saúde', 'pronto'],
  ['Libras e acessibilidade', 'pronto'],
  ['Aplicativo instalável (PWA)', 'pronto'],
  ['Reconhecimento por imagem com IA', 'opcional'],
  ['Notificações push', 'próximo'],
]

const DOCS = [
  { titulo: 'Guia do usuário', descricao: 'Passo a passo de cada tela', url: 'https://brazilian-system-of-food-metric.github.io/software-documentation/guia-usuario/introducao.html', icone: 'livro' },
  { titulo: 'Guia do desenvolvedor', descricao: 'Arquitetura, API e banco', url: 'https://brazilian-system-of-food-metric.github.io/software-documentation/guia-desenvolvedor/index.html', icone: 'chave' },
  { titulo: 'Roadmap', descricao: 'O que foi feito e o que vem', url: 'https://brazilian-system-of-food-metric.github.io/software-documentation/roadmap/index.html', icone: 'mapa' },
  { titulo: 'Changelog', descricao: 'Mudanças por versão', url: 'https://brazilian-system-of-food-metric.github.io/software-documentation/changelog/index.html', icone: 'atualizar' },
]

const TomDoStatus = (estado) => (estado === 'pronto' ? 'ok' : estado === 'opcional' ? 'aviso' : 'neutro')

export default function Landing({
  aoEntrar,
  aoCriarConta,
  instalar,
  podeInstalar,
  instalado,
  tema,
  alternarTema,
}) {
  return (
    <div className="landing">
      <header className="landing-topo">
        <a className="landing-marca" href="#topo" aria-label="BSFM — início">
          <div className="marca-simbolo">BS</div>
          <div className="marca-texto">
            <span className="marca-nome">BSFM</span>
            <span className="marca-tag">Brazilian System of Food Metric</span>
          </div>
        </a>

        <nav className="landing-nav" aria-label="Seções">
          <a href="#visao">O projeto</a>
          <a href="#recursos">Recursos</a>
          <a href="#telas">Telas</a>
          <a href="#como-funciona">Como funciona</a>
          <a href="#roadmap">Roadmap</a>
          <a href="#documentacao">Documentação</a>
        </nav>

        <div className="landing-acoes">
          <button className="botao-icone" onClick={alternarTema} aria-label="Alternar tema">
            <Icone nome={tema === 'escuro' ? 'sol' : 'lua'} tamanho={18} />
          </button>
          {!instalado && (
            <button className="btn btn-fantasma btn-mini landing-instalar" onClick={instalar} disabled={!podeInstalar}>
              <Icone nome="instalar" tamanho={15} /> Instalar
            </button>
          )}
          <button className="btn btn-mini" onClick={aoEntrar}>Entrar</button>
        </div>
      </header>

      <main id="topo">
        <section className="heroi">
          <div className="heroi-texto">
            <span className="selo">
              <span className="selo-ponto" /> Projeto acadêmico · PIM
            </span>
            <h1>Comer bem começa por entender o que está no prato.</h1>
            <p>
              O BSFM registra suas refeições, calcula calorias e macros com a tabela TACO e mostra a
              evolução do seu corpo — sem planilha, sem achismo.
            </p>

            <div className="heroi-acoes">
              <button className="btn btn-grande" onClick={aoCriarConta}>
                Criar conta grátis <Icone nome="avancar" tamanho={17} />
              </button>
              {!instalado && (
                <button className="btn btn-fantasma btn-grande" onClick={instalar} disabled={!podeInstalar}>
                  <Icone nome="instalar" tamanho={17} />
                  {podeInstalar ? 'Instalar aplicativo' : 'Instalar pelo navegador'}
                </button>
              )}
            </div>

            <p className="heroi-nota">
              {instalado
                ? 'Aplicativo instalado neste dispositivo.'
                : podeInstalar
                  ? 'Instalável no celular e no computador. Abre em tela cheia e funciona offline.'
                  : 'No iPhone: Compartilhar → Adicionar à Tela de Início.'}
            </p>

            <ul className="heroi-provas">
              <li><Icone nome="check" tamanho={15} /> 582 alimentos TACO</li>
              <li><Icone nome="check" tamanho={15} /> Dados no servidor</li>
              <li><Icone nome="check" tamanho={15} /> LGPD e 2FA</li>
            </ul>
          </div>

          <div className="heroi-visual">
            <div className="heroi-moldura">
              <img src="/demo/painel.png?v=3" alt="Tela do painel do BSFM" width="560" height="394" />
            </div>
            <div className="flutuante flutuante-um">
              <span className="flutuante-icone"><Icone nome="grafico" tamanho={15} /></span>
              <div><strong>IMC 22,4</strong><small>peso adequado</small></div>
            </div>
            <div className="flutuante flutuante-dois">
              <span className="flutuante-icone"><Icone nome="agua" tamanho={15} /></span>
              <div><strong>1.850 ml</strong><small>de 2.100 ml</small></div>
            </div>
            <div className="flutuante flutuante-tres">
              <span className="flutuante-icone"><Icone nome="comida" tamanho={15} /></span>
              <div><strong>582</strong><small>alimentos na base</small></div>
            </div>
          </div>
        </section>

        <section id="visao" className="secao secao-alt">
          <div className="secao-cabeca">
            <span className="olho">Nossa visão</span>
            <h2>Por que o BSFM existe</h2>
            <p className="lead">
              A má nutrição e a falta de informação sobre alimentação saudável são desafios reais no
              Brasil. O BSFM nasceu como projeto acadêmico para explorar como a tecnologia pode ajudar
              as pessoas a entender melhor o que comem.
            </p>
          </div>

          <div className="grade-3">
            {VALORES.map((v) => (
              <article className="cartao cartao-valor" key={v.titulo}>
                <span className="valor-icone"><Icone nome={v.icone} tamanho={20} /></span>
                <h3>{v.titulo}</h3>
                <p>{v.texto}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="recursos" className="secao">
          <div className="secao-cabeca">
            <span className="olho">O que já funciona</span>
            <h2>Um app de nutrição de verdade</h2>
            <p className="lead">
              Nada de tela de demonstração: cada recurso abaixo está ligado à API, ao banco e às
              verificações automatizadas.
            </p>
          </div>

          <div className="grade-recursos">
            {RECURSOS.map((r) => (
              <article className="recurso" key={r.titulo}>
                <span className="recurso-icone"><Icone nome={r.icone} tamanho={21} /></span>
                <h3>{r.titulo}</h3>
                <p>{r.texto}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="telas" className="secao secao-alt">
          <div className="secao-cabeca">
            <span className="olho">Por dentro</span>
            <h2>Veja o BSFM em ação</h2>
            <p className="lead">Capturas reais do aplicativo em produção — não são protótipos.</p>
          </div>

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
            <div className="galeria">
              {CAPTURAS.map((c) => (
                <figure className="tela-demo" key={c.arquivo}>
                  <div className="tela-demo-moldura">
                    <img src={c.arquivo} alt={`Tela de ${c.titulo}`} loading="lazy" width="560" height="394" />
                  </div>
                  <figcaption>
                    <strong>{c.titulo}</strong>
                    <span>{c.descricao}</span>
                  </figcaption>
                </figure>
              ))}
            </div>
          )}
        </section>

        <section id="como-funciona" className="secao">
          <div className="secao-cabeca">
            <span className="olho">Fluxo</span>
            <h2>Como funciona</h2>
            <p className="lead">Em quatro passos você sai do cadastro para o primeiro gráfico.</p>
          </div>

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

        <section id="roadmap" className="secao secao-alt">
          <div className="secao-cabeca">
            <span className="olho">Roadmap</span>
            <h2>Metas e próximos passos</h2>
            <p className="lead">O BSFM é um projeto em evolução. Isto é o que já está no ar e o que vem depois.</p>
          </div>

          <div className="grade-2">
            <article className="cartao cartao-lista">
              <h3><Icone nome="check" tamanho={18} /> Já implementado</h3>
              <ul className="lista-check">
                {FEITO.map((item) => <li key={item}><Icone nome="check" tamanho={14} /> {item}</li>)}
              </ul>
            </article>

            <article className="cartao cartao-lista">
              <h3><Icone nome="raio" tamanho={18} /> Próximos passos</h3>
              <ul className="lista-check lista-check-proxima">
                {PROXIMOS.map((item) => <li key={item}><Icone nome="raio" tamanho={14} /> {item}</li>)}
              </ul>
            </article>
          </div>
        </section>

        <section id="status" className="secao">
          <div className="secao-cabeca">
            <span className="olho">Transparência</span>
            <h2>Status do projeto</h2>
            <p className="lead">
              Protótipo funcional do PIM em Análise e Desenvolvimento de Sistemas. Cada módulo tem um
              estado declarado — sem prometer o que ainda não existe.
            </p>
          </div>

          <div className="cartao cartao-status">
            <table className="tabela">
              <thead>
                <tr><th>Módulo</th><th>Estado</th></tr>
              </thead>
              <tbody>
                {STATUS.map(([modulo, estado]) => (
                  <tr key={modulo}>
                    <td>{modulo}</td>
                    <td><span className={`tag tag-${TomDoStatus(estado)}`}>{estado}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section className="secao secao-inclusao">
          <div className="inclusao">
            <div className="inclusao-texto">
              <span className="olho olho-claro">Inclusão e prioridade</span>
              <h2>Nutrição também é acessibilidade</h2>
              <p>
                O BSFM traz o <strong>VLibras</strong> em todas as telas, o widget oficial do governo
                para tradução em Língua Brasileira de Sinais. A informação nutricional precisa chegar a
                quem precisa dela — sem barreira.
              </p>
              <div className="inclusao-chips">
                <span><Icone nome="acessibilidade" tamanho={15} /> VLibras ativo</span>
                <span><Icone nome="olho" tamanho={15} /> Foco visível</span>
                <span><Icone nome="dispositivo" tamanho={15} /> Mobile e desktop</span>
              </div>
            </div>

            <div className="inclusao-card">
              <h3>Sobre o BSFM</h3>
              <p>
                O <strong>Brazilian System of Food Metric</strong> é um projeto acadêmico do PIM
                (Projeto Integrador Multidisciplinar). O objetivo é demonstrar, na prática, programação,
                banco de dados, inteligência artificial e engenharia de software em uma solução de
                nutrição.
              </p>
              <p className="inclusao-aviso">
                Os valores são estimativas e não substituem consulta com nutricionista ou médico.
                Em emergência, ligue 192 (SAMU).
              </p>
            </div>
          </div>
        </section>

        <section id="instalar" className="secao">
          <div className="instalar-painel">
            <div>
              <span className="olho">Aplicativo</span>
              <h2>Leve o BSFM no bolso</h2>
              <p>
                O aplicativo é instalável direto do navegador — sem loja, sem download de APK. Depois de
                instalado, abre em tela cheia, com ícone próprio, e o shell continua disponível mesmo
                sem internet. As mesmas funções existem no computador.
              </p>
              <ul className="lista-check">
                <li><Icone nome="check" tamanho={14} /> Funciona offline no shell</li>
                <li><Icone nome="check" tamanho={14} /> Atualiza sozinho ao abrir online</li>
                <li><Icone nome="check" tamanho={14} /> Sem cadastro em loja de aplicativos</li>
              </ul>
            </div>

            <div className="instalar-acoes">
              <button className="btn btn-grande" onClick={instalar} disabled={!podeInstalar || instalado}>
                <Icone nome="instalar" tamanho={18} />
                {instalado ? 'Já instalado' : podeInstalar ? 'Instalar agora' : 'Instalar pelo navegador'}
              </button>
              <span className="rodape-cartao">
                {podeInstalar
                  ? 'O navegador confirmou que o app é instalável.'
                  : 'No iPhone: Compartilhar → Adicionar à Tela de Início.'}
              </span>
              <span className="rodape-cartao">Versão web e PWA no mesmo código React.</span>
            </div>
          </div>
        </section>

        <section id="numeros" className="secao secao-alt">
          <div className="secao-cabeca">
            <span className="olho">O projeto em números</span>
            <h2>Base sólida, não maquete</h2>
          </div>
          <div className="grade-numeros">
            <div className="numero"><strong>582</strong><span>alimentos na base TACO</span></div>
            <div className="numero"><strong>300+</strong><span>verificações automatizadas</span></div>
            <div className="numero"><strong>14</strong><span>tabelas no banco</span></div>
            <div className="numero"><strong>100%</strong><span>gratuito e open source</span></div>
          </div>
        </section>

        <section id="documentacao" className="secao">
          <div className="secao-cabeca">
            <span className="olho">Documentação</span>
            <h2>Entenda o projeto por dentro</h2>
            <p className="lead">Guia de uso, arquitetura, roadmap e changelog — tudo publicado.</p>
          </div>

          <div className="grade-docs">
            {DOCS.map((d) => (
              <a className="doc" key={d.titulo} href={d.url} target="_blank" rel="noreferrer">
                <span className="doc-icone"><Icone nome={d.icone} tamanho={18} /></span>
                <span className="doc-texto">
                  <strong>{d.titulo}</strong>
                  <small>{d.descricao}</small>
                </span>
                <Icone nome="avancar" tamanho={16} />
              </a>
            ))}
          </div>
        </section>

        <section className="secao secao-final">
          <div className="final-cartao">
            <span className="olho">Comece agora</span>
            <h2>Quer conhecer o projeto?</h2>
            <p>Crie sua conta em menos de um minuto e registre a primeira refeição ainda hoje.</p>
            <div className="heroi-acoes">
              <button className="btn btn-grande" onClick={aoCriarConta}>Criar conta grátis</button>
              <button className="btn btn-fantasma btn-grande" onClick={aoEntrar}>Já tenho conta</button>
            </div>
          </div>
        </section>
      </main>

      <footer className="landing-rodape">
        <div className="landing-rodape-marca">
          <div className="marca-simbolo">BS</div>
          <div>
            <strong>BSFM</strong>
            <span>Brazilian System of Food Metric</span>
          </div>
        </div>
        <p>
          Dados: TACO 4ª ed. (NEPA/UNICAMP) · Projeto acadêmico PIM · As informações são estimativas e
          não substituem atendimento profissional.
        </p>
        <span>© {new Date().getFullYear()} BSFM · open source</span>
      </footer>
    </div>
  )
}
