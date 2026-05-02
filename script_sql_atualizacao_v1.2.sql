-- ============================================================
-- SCRIPT DE ATUALIZAÇÃO BSFM v1.2
-- Execute no SQL Editor do Neon
-- ============================================================

-- ============================================================
-- 1. CRIAR USUÁRIO SISTEMA (ID = 0) PARA RECEITAS PADRÃO
-- ============================================================
INSERT INTO "Usuarios" ("ID", "Nome", "Email", "SenhaHash", "Peso", "Altura", "Idade", "Sexo", "TipoPessoa", "EmailVerificado", "DataNascimento")
VALUES (0, 'Sistema BSFM', 'sistema@bsfm.com', 'sistema', 70, 1.70, 30, 'M', 'Sistema', true, NULL)
ON CONFLICT ("ID") DO NOTHING;

-- ============================================================
-- 2. ADICIONAR COLUNA DataNascimento NA TABELA Usuarios
-- ============================================================
ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "DataNascimento" TIMESTAMP;

-- ============================================================
-- 2. CRIAR TABELA: ConsumoAgua
-- ============================================================
CREATE TABLE IF NOT EXISTS "ConsumoAgua" (
    "Id" SERIAL PRIMARY KEY,
    "UsuarioId" INTEGER NOT NULL REFERENCES "Usuarios"("ID"),
    "Ml" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "DataRegistro" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_consumo_agua_usuario ON "ConsumoAgua"("UsuarioId");
CREATE INDEX IF NOT EXISTS idx_consumo_agua_data ON "ConsumoAgua"("DataRegistro");

-- ============================================================
-- 3. CRIAR TABELA: RefeicoesAgendadas
-- ============================================================
CREATE TABLE IF NOT EXISTS "RefeicoesAgendadas" (
    "Id" SERIAL PRIMARY KEY,
    "UsuarioId" INTEGER NOT NULL REFERENCES "Usuarios"("ID"),
    "DiaSemana" TEXT NOT NULL DEFAULT '',
    "TipoRefeicao" TEXT NOT NULL DEFAULT '',
    "NomePrato" TEXT NOT NULL DEFAULT '',
    "Ingredientes" TEXT NOT NULL DEFAULT '',
    "ModoPreparo" TEXT NOT NULL DEFAULT '',
    "Calorias" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Proteinas" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Carboidratos" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Gorduras" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "DataCriacao" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_refeicoes_agendadas_usuario ON "RefeicoesAgendadas"("UsuarioId");

-- ============================================================
-- 4. INSERIR RECEITAS SAUDÁVEIS PRÉ-CARREGADAS
-- ============================================================
INSERT INTO "RefeicoesAgendadas" ("UsuarioId", "DiaSemana", "TipoRefeicao", "NomePrato", "Ingredientes", "ModoPreparo", "Calorias", "Proteinas", "Carboidratos", "Gorduras")
VALUES
(0, 'Segunda', 'Café', 'Omelete de Claras com Aveia', '3 claras, 2 colheres de aveia, sal, cebolinha, azeite', 'Misture as claras com a aveia e temperos. Cozinhe em frigideira antiaderente com um fio de azeite.', 180, 22, 15, 4),
(0, 'Segunda', 'Almoço', 'Peito de Frango Grelhado com Quinoa', '200g peito frango, 100g quinoa, brócolis, cenoura, azeite', 'Grelhe o frango temperado. Cozinhe a quinoa e refogue os legumes. Sirva tudo junto.', 420, 45, 35, 8),
(0, 'Segunda', 'Jantar', 'Sopa de Legumes com Frango Desfiado', '100g frango desfiado, abóbora, cenoura, chuchu, batata doce, salsinha', 'Cozinhe os legumes picados até amolecer. Adicione o frango desfiado e tempere. Bata parcialmente no liquidificador.', 280, 28, 30, 5),
(0, 'Segunda', 'Lanche', 'Iogurte Natural com Granola e Frutas', '1 pote iogurte natural, 2 colheres granola, 1 banana, morangos', 'Misture o iogurte com a granola e as frutas picadas.', 200, 12, 28, 4),

(0, 'Terça', 'Café', 'Pão Integral com Pasta de Abacate', '2 fatias pão integral, 1/2 abacate, limão, sal, tomate cereja', 'Amasse o abacate com limão e sal. Passe no pão e adicione tomate cereja.', 250, 8, 30, 12),
(0, 'Terça', 'Almoço', 'Salmão Grelhado com Batata Doce', '150g salmão, 1 batata doce média, aspargos, azeite, limão', 'Tempere o salmão com limão e grelhe. Cozinhe a batata doce e os aspargos no vapor.', 450, 38, 40, 14),
(0, 'Terça', 'Jantar', 'Salada de Grão de Bico com Atum', '1 lata atum, 200g grão de bico cozido, cebola roxa, tomate, pepino, azeite', 'Misture todos os ingredientes picados. Tempere com azeite, limão e sal.', 320, 30, 28, 10),
(0, 'Terça', 'Lanche', 'Smoothie Verde', '1 copo leite vegetal, 1 banana, espinafre, 1 colher pasta de amendoim', 'Bata todos os ingredientes no liquidificador até ficar homogêneo.', 180, 8, 25, 6),

(0, 'Quarta', 'Café', 'Vitamina de Banana com Aveia', '1 banana, 200ml leite desnatado, 2 colheres aveia, canela', 'Bata tudo no liquidificador. Sirva com canela polvilhada.', 220, 10, 38, 3),
(0, 'Quarta', 'Almoço', 'Carne Moída com Abóbora e Arroz Integral', '150g carne moída magra, 200g abóbora, 100g arroz integral, cebola, alho', 'Refogue a carne com cebola e alho. Cozinhe a abóbora no vapor. Sirva com arroz integral.', 480, 35, 50, 12),
(0, 'Quarta', 'Jantar', 'Wrap de Frango com Vegetais', '1 wrap integral, 100g frango desfiado, alface, tomate, cenoura ralada, iogurte natural', 'Recheie o wrap com frango e vegetais. Enrole e sirva.', 300, 28, 30, 8),
(0, 'Quarta', 'Lanche', 'Mix de Castanhas e Fruta', '30g castanhas mistas, 1 maçã', 'Sirva as castanhas com a maçã picada.', 180, 5, 18, 12),

(0, 'Quinta', 'Café', 'Crepioca de Frango', '2 colheres tapioca, 1 ovo, 50g frango desfiado, requeijão light', 'Misture tapioca e ovo. Cozinhe na frigideira. Recheie com frango e requeijão.', 280, 20, 22, 10),
(0, 'Quinta', 'Almoço', 'Filé de Peixe com Purê de Batata Doce', '150g filé de tilápia, 1 batata doce, couve refogada, azeite', 'Grelhe o peixe temperado. Cozinhe a batata doce e amasse com azeite. Refogue a couve.', 400, 35, 38, 10),
(0, 'Quinta', 'Jantar', 'Panqueca Integral de Espinafre', 'Massa: 1 ovo, 2 colheres farinha integral, espinafre. Recheio: ricota, tomate seco', 'Bata os ingredientes da massa. Cozinhe as panquecas. Recheie com ricota e tomate seco.', 320, 22, 28, 12),
(0, 'Quinta', 'Lanche', 'Pasta de Grão de Bico (Homus) com Palitos de Legumes', 'Homus: grão de bico, tahine, limão. Palitos: cenoura, pepino, salsão', 'Bata o homus no processador. Corte os legumes em palitos. Sirva juntos.', 150, 8, 15, 7),

(0, 'Sexta', 'Café', 'Tapioca com Ovo e Queijo', '2 colheres tapioca, 1 ovo, 1 fatia queijo minas, orégano', 'Hidrate a tapioca na frigideira. Adicione o ovo e o queijo. Tempere com orégano.', 300, 18, 30, 12),
(0, 'Sexta', 'Almoço', 'Strogonoff de Frango com Arroz e Brócolis', '150g frango, 2 colheres creme de leite, mostarda, ketchup, arroz integral, brócolis', 'Cozinhe o frango em cubos. Adicione molho. Sirva com arroz e brócolis no vapor.', 450, 38, 42, 12),
(0, 'Sexta', 'Jantar', 'Pizza de Couve-flor', '1 couve-flor processada, 1 ovo, queijo, molho tomate, manjericão', 'Processe a couve-flor crua. Misture com ovo e asse como base. Cubra com molho e queijo.', 250, 18, 15, 12),
(0, 'Sexta', 'Lanche', 'Frutas Vermelhas com Chantilly de Coco', '1 xícara frutas vermelhas, 1 lata leite de coco gelado', 'Bata o leite de coco gelado até virar chantilly. Sirva com as frutas.', 160, 3, 20, 8),

(0, 'Sábado', 'Café', 'Panqueca de Banana com Mel', '1 banana amassada, 2 ovos, 1 colher mel, canela', 'Misture banana e ovos. Cozinhe na frigideira. Regue com mel e canela.', 250, 12, 30, 8),
(0, 'Sábado', 'Almoço', 'Escondidinho de Carne Seca com Purê de Mandioca', '150g carne seca dessalgada, 300g mandioca, queijo coalho, manteiga', 'Cozinhe e desfie a carne seca. Faça purê de mandioca. Monte camadas e gratine com queijo.', 520, 35, 45, 18),
(0, 'Sábado', 'Jantar', 'Salada Caesar Light', 'Alface romana, 100g frango grelhado, croutons integrais, molho caesar light', 'Monte a salada com frango grelhado fatiado, croutons e molho.', 280, 30, 12, 12),
(0, 'Sábado', 'Lanche', 'Sorvete de Banana com Cacau', '2 bananas congeladas, 1 colher cacau em pó, 1 colher pasta de amendoim', 'Bata as bananas congeladas com cacau e pasta de amendoim no processador.', 200, 6, 35, 6),

(0, 'Domingo', 'Café', 'Ovos Mexidos com Tomate e Manjericão', '2 ovos, 1 tomate picado, manjericão, azeite, pão integral', 'Refogue o tomate no azeite. Adicione os ovos mexidos e manjericão. Sirva com pão.', 280, 18, 20, 12),
(0, 'Domingo', 'Almoço', 'Feijoada Light', '100g feijão preto, 100g carne magra (patinho), couve refogada, arroz integral, laranja', 'Cozinhe o feijão com a carne. Sirva com arroz, couve refogada e rodelas de laranja.', 480, 35, 55, 10),
(0, 'Domingo', 'Jantar', 'Sopa Cremosa de Abóbora com Gengibre', '300g abóbora, 1 batata doce, gengibre, cebola, creme de leite light', 'Cozinhe abóbora e batata doce. Bata com gengibre e creme de leite. Aqueça e sirva.', 220, 6, 35, 8),
(0, 'Domingo', 'Lanche', 'Chá Gelado com Limão e Hortelã', '1 xícara chá mate, suco 1 limão, hortelã, gelo, adoçante', 'Prepare o chá e deixe esfriar. Adicione limão, hortelã e gelo.', 10, 0, 2, 0);

-- ============================================================
-- FIM DO SCRIPT
-- ============================================================
