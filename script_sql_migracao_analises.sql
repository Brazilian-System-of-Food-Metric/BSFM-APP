-- ============================================================
-- SCRIPT DE MIGRAÇÃO: Adicionar campos de Feedback IA
-- Tabela: AnalisesIA
-- Descrição: Adiciona colunas para o feedback da IA Nutricional
--            (Groq Llama 3) nas análises de pratos
-- ============================================================

-- Coluna: PodeConsumir (true = pode, false = evitar, null = moderado)
ALTER TABLE "AnalisesIA" 
ADD COLUMN IF NOT EXISTS "PodeConsumir" BOOLEAN DEFAULT NULL;

-- Coluna: PontuacaoSaude (0-10)
ALTER TABLE "AnalisesIA" 
ADD COLUMN IF NOT EXISTS "PontuacaoSaude" INTEGER NOT NULL DEFAULT 0;

-- Coluna: AnaliseEmRelacaoAMeta (texto explicativo personalizado)
ALTER TABLE "AnalisesIA" 
ADD COLUMN IF NOT EXISTS "AnaliseEmRelacaoAMeta" TEXT NOT NULL DEFAULT '';

-- Coluna: DicaBSFM (dica prática do nutricionista)
ALTER TABLE "AnalisesIA" 
ADD COLUMN IF NOT EXISTS "DicaBSFM" TEXT NOT NULL DEFAULT '';

-- ============================================================
-- VERIFICAÇÃO: Listar as colunas da tabela após migração
-- ============================================================
-- SELECT column_name, data_type 
-- FROM information_schema.columns 
-- WHERE table_name = 'AnalisesIA'
-- ORDER BY ordinal_position;
