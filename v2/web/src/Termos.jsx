import { useEffect } from 'react'
import { Icone } from './Icones.jsx'

/* Termos de uso e política de privacidade — o modal que a v1 tinha e a v2
   havia perdido. O texto é o mesmo contrato, com a parte de LGPD explícita. */

export const TERMOS = [
  {
    titulo: '1. O que é o BSFM',
    texto:
      'O BSFM (Brazilian System of Food Metric) é um projeto acadêmico do PIM — Projeto Integrador Multidisciplinar. Ele calcula indicadores nutricionais e estima valores de refeições com base na tabela TACO e em regras próprias. Não é consulta médica nem prescrição dietética e não substitui nutricionista ou médico.',
  },
  {
    titulo: '2. Dados que você informa',
    texto:
      'Nome, e-mail e senha (a senha é guardada apenas como hash), peso, altura, sexo, nível de atividade, data de nascimento, meta de peso, consumo de água, refeições registradas e perfil de saúde (diabetes e intolerâncias). Os dados de saúde são categoria especial na LGPD e só são tratados com o seu consentimento explícito, registrado no cadastro.',
  },
  {
    titulo: '3. Para que usamos',
    texto:
      'Calcular IMC, taxa metabólica basal e gasto energético; montar seu histórico; gerar o feedback nutricional; e enviar o código de verificação da conta por e-mail. Nenhum dado é vendido e não há publicidade comportamental.',
  },
  {
    titulo: '4. Com quem compartilhamos',
    texto:
      'Com o provedor de e-mail (Brevo), apenas para enviar códigos de verificação e recuperação de senha. Se o reconhecimento de alimento por imagem estiver habilitado no servidor, a foto é enviada ao provedor de visão configurado apenas para identificar o alimento. O OCR de rótulo e de prescrição roda no seu aparelho: nesse caso a imagem não sai do dispositivo.',
  },
  {
    titulo: '5. Por quanto tempo guardamos',
    texto:
      'Enquanto sua conta existir. Registros de análise e de água podem ser apagados por você a qualquer momento. A exclusão da conta remove os dados pessoais; registros de auditoria de segurança são mantidos pelo prazo necessário para investigação de incidentes.',
  },
  {
    titulo: '6. Seus direitos',
    texto:
      'Você pode acessar, corrigir, exportar e excluir seus dados. A tela de Perfil permite exportar tudo em JSON e apagar registros individualmente. Para exclusão da conta, use o contato do projeto. Também é possível revogar o consentimento de dados de saúde, deixando o perfil como "não informado".',
  },
  {
    titulo: '7. Segurança',
    texto:
      'A senha usa hash com salt. A sessão é assinada e pode ser encerrada por dispositivo. A verificação em duas etapas (TOTP) é opcional e pode ser ativada no Perfil. O token de acesso fica apenas na memória da aba (sessionStorage) e expira; a renovação é rotacionada no servidor.',
  },
  {
    titulo: '8. Aviso importante',
    texto:
      'Os valores nutricionais são estimativas. O BSFM é um protótipo acadêmico em evolução: em caso de dúvida sobre alimentação, alergia ou condição clínica, procure um profissional de saúde. Em emergência, ligue 192 (SAMU).',
  },
]

export function ModalTermos({ aberto, aoFechar }) {
  useEffect(() => {
    if (!aberto) return
    const aoTeclar = (evento) => {
      if (evento.key === 'Escape') aoFechar()
    }
    document.addEventListener('keydown', aoTeclar)
    const anterior = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', aoTeclar)
      document.body.style.overflow = anterior
    }
  }, [aberto, aoFechar])

  if (!aberto) return null

  return (
    <div
      className="modal-fundo"
      role="presentation"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) aoFechar()
      }}
    >
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby="titulo-termos">
        <header className="modal-topo">
          <div>
            <span className="olho">Transparência</span>
            <h2 id="titulo-termos">Termos de uso e privacidade</h2>
            <p className="modal-sub">Versão 1.0 · LGPD (Lei 13.709/2018)</p>
          </div>
          <button className="botao-icone" onClick={aoFechar} aria-label="Fechar termos">
            <Icone nome="x" tamanho={18} />
          </button>
        </header>

        <div className="modal-corpo">
          {TERMOS.map((bloco) => (
            <section key={bloco.titulo}>
              <h3>{bloco.titulo}</h3>
              <p>{bloco.texto}</p>
            </section>
          ))}
        </div>

        <footer className="modal-rodape">
          <span className="rodape-cartao">Projeto acadêmico · Brazilian System of Food Metric</span>
          <button className="btn" onClick={aoFechar}>Entendi</button>
        </footer>
      </div>
    </div>
  )
}
