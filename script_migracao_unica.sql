-- ============================================================
-- SCRIPT DE MIGRAÇÃO ÚNICA - BSFM
-- Execute APENAS UMA VEZ no SQL Editor do Neon (PostgreSQL)
-- 
-- Este script adiciona TODAS as colunas que faltam nas tabelas
-- existentes, sem recriar as tabelas.
-- ============================================================

-- ============================================================
-- 1. MIGRAÇÃO: Colunas Diabetes e Intolerancia na tabela Usuarios
-- ============================================================
DO $$ 
BEGIN 
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Usuarios' AND column_name = 'Diabetes'
    ) THEN
        ALTER TABLE "Usuarios" ADD COLUMN "Diabetes" VARCHAR(50) NOT NULL DEFAULT 'Não informado';
        RAISE NOTICE 'Coluna Diabetes adicionada em Usuarios';
    ELSE
        RAISE NOTICE 'Coluna Diabetes já existe em Usuarios';
    END IF;
END $$;

DO $$ 
BEGIN 
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Usuarios' AND column_name = 'Intolerancia'
    ) THEN
        ALTER TABLE "Usuarios" ADD COLUMN "Intolerancia" TEXT NOT NULL DEFAULT '';
        RAISE NOTICE 'Coluna Intolerancia adicionada em Usuarios';
    ELSE
        RAISE NOTICE 'Coluna Intolerancia já existe em Usuarios';
    END IF;
END $$;

-- ============================================================
-- 2. MIGRAÇÃO: Colunas de Feedback IA na tabela AnalisesIA
-- ============================================================
DO $$ 
BEGIN 
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'AnalisesIA' AND column_name = 'PodeConsumir'
    ) THEN
        ALTER TABLE "AnalisesIA" ADD COLUMN "PodeConsumir" BOOLEAN DEFAULT NULL;
        RAISE NOTICE 'Coluna PodeConsumir adicionada em AnalisesIA';
    ELSE
        RAISE NOTICE 'Coluna PodeConsumir já existe em AnalisesIA';
    END IF;
END $$;

DO $$ 
BEGIN 
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'AnalisesIA' AND column_name = 'PontuacaoSaude'
    ) THEN
        ALTER TABLE "AnalisesIA" ADD COLUMN "PontuacaoSaude" INTEGER NOT NULL DEFAULT 0;
        RAISE NOTICE 'Coluna PontuacaoSaude adicionada em AnalisesIA';
    ELSE
        RAISE NOTICE 'Coluna PontuacaoSaude já existe em AnalisesIA';
    END IF;
END $$;

DO $$ 
BEGIN 
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'AnalisesIA' AND column_name = 'AnaliseEmRelacaoAMeta'
    ) THEN
        ALTER TABLE "AnalisesIA" ADD COLUMN "AnaliseEmRelacaoAMeta" TEXT NOT NULL DEFAULT '';
        RAISE NOTICE 'Coluna AnaliseEmRelacaoAMeta adicionada em AnalisesIA';
    ELSE
        RAISE NOTICE 'Coluna AnaliseEmRelacaoAMeta já existe em AnalisesIA';
    END IF;
END $$;

DO $$ 
BEGIN 
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'AnalisesIA' AND column_name = 'DicaBSFM'
    ) THEN
        ALTER TABLE "AnalisesIA" ADD COLUMN "DicaBSFM" TEXT NOT NULL DEFAULT '';
        RAISE NOTICE 'Coluna DicaBSFM adicionada em AnalisesIA';
    ELSE
        RAISE NOTICE 'Coluna DicaBSFM já existe em AnalisesIA';
    END IF;
END $$;

-- ============================================================
-- VERIFICAÇÃO FINAL: Listar todas as colunas das tabelas
-- ============================================================
SELECT 'Usuarios' as tabela, column_name, data_type, is_nullable
FROM information_schema.columns 
WHERE table_name = 'Usuarios'
ORDER BY ordinal_position;

SELECT 'analises_ia' as tabela, column_name, data_type, is_nullable
FROM information_schema.columns 
WHERE table_name = 'analises_ia'
ORDER BY ordinal_position;

-- ============================================================
-- FIM DA MIGRAÇÃO
-- ============================================================
