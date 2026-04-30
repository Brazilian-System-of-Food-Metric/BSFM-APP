-- ============================================================
-- SCRIPT DE MIGRAÇÃO: Adicionar campo Diabetes na tabela Usuario
-- 
-- Este script adiciona a coluna "Diabetes" na tabela "Usuarios"
-- para bancos PostgreSQL que já estão em produção.
--
-- Valores possíveis: "Não", "Tipo 1", "Tipo 2", "Pré-diabetes", 
--                    "Gestacional", "Não informado"
-- ============================================================

-- Adiciona a coluna Diabetes (se não existir)
ALTER TABLE "Usuarios" 
ADD COLUMN IF NOT EXISTS "Diabetes" VARCHAR(50) NOT NULL DEFAULT 'Não informado';

-- Adiciona a coluna Intolerancia (se não existir)
ALTER TABLE "Usuarios" 
ADD COLUMN IF NOT EXISTS "Intolerancia" TEXT NOT NULL DEFAULT '';

-- ============================================================
-- VERIFICAÇÃO: Listar as colunas da tabela após migração
-- ============================================================
-- SELECT column_name, data_type, is_nullable, column_default
-- FROM information_schema.columns 
-- WHERE table_name = 'Usuarios'
-- ORDER BY ordinal_position;
