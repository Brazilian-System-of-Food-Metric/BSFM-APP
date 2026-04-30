# Engenharia de Software Ágil Aplicada
**Aula 04 - Normas e Padrões da Qualidade**

*   **Instituição:** UNIP – Universidade Paulista
*   **Professor:** Edson Quedas Moreno
*   **Referência:** Unidade I – Capítulo 2

---

## Objetivos da Aula
*   Compreender o papel das normas e padrões na garantia da qualidade de software.
*   Conhecer a família ISO 9000 e sua aplicação no desenvolvimento de software.
*   Dominar o modelo ISO/IEC 12207 — ciclo de vida do software.
*   Entender a ISO/IEC 9126 e a evolução para a família ISO/IEC 25000 (SQuaRE).
*   Conhecer o modelo CMMI — maturidade e capacidade de processos.
*   Distinguir normas de processo vs. normas de produto de software.
*   Aplicar critérios de conformidade em contextos de desenvolvimento ágil.

---

## Por que Normas e Padrões são Essenciais?

*   **Previsibilidade:** Processos padronizados tornam o desenvolvimento mais previsível, reduzindo riscos e surpresas tardias.
*   **Comunicação:** Linguagem comum entre clientes, fornecedores e equipes de diferentes países e organizações.
*   **Qualidade:** Critérios objetivos e mensuráveis substituem avaliações subjetivas do que é 'bom o suficiente'.
*   **Conformidade:** Requisitos legais, contratuais e regulatórios exigem aderência a normas específicas (LGPD, PCI-DSS, etc.).

---

## Normas de Processo × Normas de Produto
Duas perspectivas complementares e igualmente necessárias:

### Normas de Processo
Definem **COMO** desenvolver — as atividades, papéis e artefatos do ciclo de vida.
*   **ISO/IEC 12207** — Ciclo de vida de software
*   **CMMI** — Maturidade de processos
*   **ISO/IEC 15504 (SPICE)** — Avaliação de processos
*   **MPS.BR** — Modelo brasileiro de melhoria

### Normas de Produto
Definem **O QUE** medir — as características de qualidade do software entregue.
*   **ISO/IEC 9126** — Qualidade de produto
*   **ISO/IEC 25000 (SQuaRE)** — Evolução do 9126
*   **ISO/IEC 25010** — Modelo de qualidade
*   **IEEE Std 730** — Planos de SQA

---

## A Família ISO 9000: Fundamentos da Qualidade
*   Estabelece princípios universais de gestão da qualidade aplicáveis a qualquer setor.
*   **Principais normas:**
    *   **ISO 9000:2015:** Vocabulário e fundamentos.
    *   **ISO 9001:2015:** Requisitos para certificação de Sistemas de Gestão da Qualidade (SGQ).
    *   **ISO 9004:2018:** Gestão para o sucesso sustentado.
*   **Os 7 Princípios:** Foco no cliente, Liderança, Engajamento das pessoas, Abordagem de processo, Melhoria contínua, Tomada de decisão baseada em evidências, Gestão de relacionamentos.
*   **Para software:** Exige que processos de desenvolvimento sejam documentados, controlados, medidos e continuamente melhorados.

---

## ISO/IEC 12207 — Ciclo de Vida de Software
Define QUEM faz O QUÊ e QUANDO.

*   **Processos Fundamentais:** Aquisição, Fornecimento, Desenvolvimento, Operação, Manutenção.
*   **Processos de Apoio:** Documentação, Gerência de Configuração, Garantia da Qualidade, Verificação, Validação, Revisão, Auditoria, Resolução de Problemas.
*   **Processos Organizacionais:** Gestão, Infraestrutura, Melhoria, Treinamento.
*   **Adaptação (Tailoring):** A norma permite que cada organização selecione e adapte os processos ao seu contexto e porte.

### No Contexto Ágil
| Processo ISO 12207 | Prática Ágil Correspondente |
| :--- | :--- |
| Desenvolvimento | User stories, sprints, backlog refinement, Definition of Done |
| Garantia da Qualidade | Code review, testes automatizados, CI/CD, critérios de aceite |
| Gestão de Configuração | Git, versionamento, branching strategy, controle de releases |
| Verificação | Testes unitários, de integração e de sistema executados a cada sprint |
| Validação | Sprint Review com cliente, testes de aceite, feedback contínuo |
| Gestão | Scrum Master, Planning, Daily, Retrospectiva, Burndown chart |

---

## ISO/IEC 9126 — Qualidade de Produto de Software
Organiza a qualidade em três perspectivas:

1.  **Qualidade Interna:** Avaliada por desenvolvedores/arquitetos no código-fonte, estrutura, complexidade e coesão (ex: SonarQube).
2.  **Qualidade Externa:** Avaliada por Testadores/QA no comportamento observável (desempenho, segurança, funcionalidade em execução - ex: JMeter, OWASP ZAP).
3.  **Qualidade em Uso:** Avaliada por usuários finais quanto à eficácia, eficiência e satisfação no contexto real (ex: Pesquisas SUS, NPS).

### As 6 Características de Qualidade:
*   **F**uncionalidade (adequação, acurácia)
*   **C**onfiabilidade (tolerância a falhas, recuperação)
*   **U**sabilidade (inteligibilidade, aprendizagem)
*   **E**ficiência (comportamento em relação ao tempo/recursos)
*   **M**anutenibilidade (analisabilidade, modificabilidade)
*   **P**ortabilidade (adaptabilidade, instalabilidade)

---

## ISO/IEC 25000 — SQuaRE: A Evolução do 9126
Unifica e substitui as séries 9126 e 14598.

*   **ISO 25000-25099:** Gestão da Qualidade (Guia mestre).
*   **ISO 25010:** Modelo de Qualidade (Define 8 características e 31 subcaracterísticas).
*   **ISO 25020-25029:** Medição da Qualidade (Métricas).
*   **ISO 25040-25049:** Avaliação da Qualidade (Processo de avaliação).
*   **ISO 25050-25099:** Requisitos de Qualidade (Como especificar RNFs).

### Mudanças (9126 -> 25010):
A ISO 25010 adicionou **Compatibilidade** e expandiu **Segurança** (que era subcaracterística na 9126) para o nível principal.

---

## CMMI — Capability Maturity Model Integration
Avalia e melhora a maturidade dos processos:

*   **Nível 5 - Otimizado:** Melhoria contínua e inovação.
*   **Nível 4 - Gerenciado Qtv:** Processos controlados estatisticamente.
*   **Nível 3 - Definido:** Processos padronizados em toda a organização.
*   **Nível 2 - Gerenciado:** Projetos planejados e monitorados.
*   **Nível 1 - Inicial:** Processos imprevisíveis e reativos.

**Nota:** CMMI v2.0 (2018) é explicitamente compatível com **Scrum, SAFe e DevOps**.

---

## MPS.BR — Melhoria de Processo do Software Brasileiro
Modelo nacional adequado ao porte das empresas brasileiras (ideal para MPEs/Startups).
Possui 7 níveis de maturidade (de A a G):
*   **A (Em Otimização) -> G (Parcialmente Gerenciado).**
*   **Equivalência:** Níveis G-F ≈ CMMI 2 | D-C ≈ CMMI 3 | B-A ≈ CMMI 4-5.

---

## Comparativo das Principais Normas
| Norma | Tipo | Foco | Quando Usar |
| :--- | :--- | :--- | :--- |
| **ISO 9001** | Processo / Org. | Gestão da qualidade | Certificação organizacional e contratos gov. |
| **ISO 12207** | Processo | Ciclo de vida | Definir papéis e fases do desenvolvimento |
| **ISO 9126** | Produto | Qualidade interna/externa | Avaliar características do software |
| **ISO 25010** | Produto | Qualidade (8 carac.) | Especificar e medir RNFs de qualidade |
| **CMMI** | Processo / Org. | Maturidade de processos | Melhorar capacidade (grandes empresas) |
| **MPS.BR** | Processo / Org. | Maturidade (7 níveis) | Alternativa nacional para MPEs brasileiras |

---

## Aplicação Prática no Scrum (Integração)
*   **Sprint Planning:** Definir critérios de aceite mensuráveis (ISO 12207 / CMMI).
*   **Daily Scrum:** Identificar impedimentos de qualidade (ISO 9001).
*   **Execução:** Cobertura de testes ≥ 80%, análise estática, code review (ISO 25010).
*   **Sprint Review:** Demonstração com evidências e validação formal (ISO 12207/9126).
*   **Retrospectiva:** Análise de causa-raiz e ajuste de práticas (CMMI / MPS.BR).
*   **Definition of Done (DoD):** Critérios derivados de todas as normas.

---

## Consolidação / Resumo
1.  **Processo** (Como fazer) vs **Produto** (O que medir).
2.  **ISO 9001** é a base universal da gestão.
3.  **ISO 9126/25010** define os atributos de qualidade (funcionalidade, etc).
4.  **CMMI/MPS.BR** medem o quão madura a empresa é.
5.  Normas não substituem engenharia bem feita; elas a complementam.

---

## Exercícios de Fixação (13 questões)

**Q 1: Diferença entre normas de PROCESSO e PRODUTO?**
*   A) Processo avalia produto; Produto define desenvolvimento.
*   **B) Processo define COMO desenvolver; Produto define O QUE medir. (Correta)**
*   C) Processo é nacional; Produto é internacional.

**Q 2: A ISO/IEC 12207 é classificada como norma de:**
*   A) Produto
*   B) Segurança
*   **C) Processo — ciclo de vida. (Correta)**

**Q 3: Grupos de processos da ISO 12207?**
*   **C) Fundamentais, de Apoio e Organizacionais. (Correta)**

**Q 4: ISO/IEC 9126 possui quantas características?**
*   **B) 6 características: Func., Confiab., Usab., Efic., Manut. e Portab. (Correta)**

**Q 5: Evolução da ISO 25010 em relação à 9126?**
*   **C) Expandiu para 8 características, adicionando Compatibilidade e ampliando Segurança. (Correta)**

**Q 6: CMMI - processos padronizados em TODA a organização?**
*   **C) Nível 3 — Definido. (Correta)**

**Q 7: Empresa ágil com backlog, velocity e monitoramento sistemático está mais próxima de:**
*   **B) Nível 2 — Gerenciado. (Correta)**

**Q 8: Objetivo do MPS.BR?**
*   **B) Modelo de maturidade mais acessível para MPEs brasileiras. (Correta)**

**Q 9: Princípio ISO 9001 relacionado a retrospectivas?**
*   **C) Melhoria contínua. (Correta)**

**Q 10: DoD com 'cobertura de testes ≥ 80%' aplica qual norma?**
*   **B) ISO/IEC 25010 — métricas de qualidade. (Correta)**

**Q 11: Perspectivas da ISO 9126?**
*   **B) Interna, Externa e em Uso. (Correta)**

**Q 12: Empresa que processa cartões de crédito segue:**
*   **C) PCI-DSS. (Correta)**

**Q 13: Principal inovação do CMMI v2.0 (2018)?**
*   **B) Compatibilidade explícita com métodos ágeis e DevOps. (Correta)**