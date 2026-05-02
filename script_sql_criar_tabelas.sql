-- ============================================================
-- SCRIPT PARA CRIAR TODAS AS TABELAS DO BSFM NO NEON (PostgreSQL)
-- Execute este script no SQL Editor do Neon
-- ============================================================

-- 1. TABELA: Usuarios
CREATE TABLE IF NOT EXISTS "Usuarios" (
    "ID" SERIAL PRIMARY KEY,
    "Nome" TEXT NOT NULL DEFAULT '',
    "Idade" INTEGER NOT NULL DEFAULT 0,
    "Email" TEXT NOT NULL DEFAULT '',
    "TokenVerificacao" TEXT,
    "EmailVerificado" BOOLEAN NOT NULL DEFAULT FALSE,
    "SenhaHash" TEXT NOT NULL DEFAULT '',
    "AceitouTermos" BOOLEAN NOT NULL DEFAULT FALSE,
    "DataAceite" TIMESTAMP NOT NULL DEFAULT NOW(),
    "VersaoTermos" TEXT NOT NULL DEFAULT '',
    "Sexo" TEXT NOT NULL DEFAULT 'Não Informado',
    "Peso" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Altura" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "TipoPessoa" TEXT NOT NULL DEFAULT 'Sedentário',
    "Intolerancia" TEXT NOT NULL DEFAULT '',
    "IMC" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "TMB" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "GastoTotal" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "PesoMeta" DOUBLE PRECISION NOT NULL DEFAULT 0
);

-- 2. TABELA: Refeicoes (antiga Refeição)
CREATE TABLE IF NOT EXISTS "Refeicoes" (
    "ID" SERIAL PRIMARY KEY,
    "NomeRefeição" TEXT NOT NULL DEFAULT '',
    "Categoria" TEXT NOT NULL DEFAULT '',
    "Ingredientes" TEXT NOT NULL DEFAULT '',
    "Calorias" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Proteínas" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Carboidratos" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Gorduras" DOUBLE PRECISION NOT NULL DEFAULT 0
);

-- 3. TABELA: Comidas
CREATE TABLE IF NOT EXISTS "Comidas" (
    "ID" SERIAL PRIMARY KEY,
    "NomeComida" TEXT NOT NULL DEFAULT '',
    "Categoria" TEXT NOT NULL DEFAULT '',
    "Calorias" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Proteínas" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Carboidratos" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Gorduras" DOUBLE PRECISION NOT NULL DEFAULT 0
);

-- 4. TABELA: Cronogramas (antiga CronogramaAlimentar)
CREATE TABLE IF NOT EXISTS "Cronogramas" (
    "ID" SERIAL PRIMARY KEY,
    "UsuarioID" INTEGER REFERENCES "Usuarios"("ID"),
    "Refeições" TEXT NOT NULL DEFAULT '',
    "Planos" TEXT NOT NULL DEFAULT ''
);

-- 5. TABELA: Hospitais
CREATE TABLE IF NOT EXISTS "Hospitais" (
    "ID" SERIAL PRIMARY KEY,
    "NomeHospital" TEXT NOT NULL DEFAULT '',
    "Endereço" TEXT NOT NULL DEFAULT '',
    "Telefone" TEXT NOT NULL DEFAULT ''
);

-- 6. TABELA: analises_ia
CREATE TABLE IF NOT EXISTS "analises_ia" (
    "ID" SERIAL PRIMARY KEY,
    "UsuarioID" INTEGER NOT NULL REFERENCES "Usuarios"("ID"),
    "Alimento" TEXT NOT NULL DEFAULT '',
    "Calorias" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Proteinas" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Carbos" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Gorduras" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Porcao" TEXT NOT NULL DEFAULT '',
    "DataAnalise" TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 7. TABELA: Historicos (antiga HistoricoProgresso)
CREATE TABLE IF NOT EXISTS "Historicos" (
    "ID" SERIAL PRIMARY KEY,
    "UsuarioID" INTEGER NOT NULL REFERENCES "Usuarios"("ID"),
    "Peso" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Altura" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "IMC" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "DataRegistro" TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 8. TABELA: CronogramasSemanais
CREATE TABLE IF NOT EXISTS "CronogramasSemanais" (
    "Id" SERIAL PRIMARY KEY,
    "UsuarioId" INTEGER NOT NULL REFERENCES "Usuarios"("ID"),
    "DataInicio" TIMESTAMP NOT NULL,
    "DataFim" TIMESTAMP NOT NULL,
    "NomePlano" VARCHAR(100) NOT NULL DEFAULT '',
    "Observacoes" VARCHAR(500) NOT NULL DEFAULT '',
    "DataCriacao" TIMESTAMP NOT NULL DEFAULT NOW(),
    "DataUltimaAtualizacao" TIMESTAMP
);

-- 9. TABELA: RefeicoesDiarias
CREATE TABLE IF NOT EXISTS "RefeicoesDiarias" (
    "Id" SERIAL PRIMARY KEY,
    "CronogramaSemanalId" INTEGER NOT NULL REFERENCES "CronogramasSemanais"("Id") ON DELETE CASCADE,
    "DiaSemana" INTEGER NOT NULL,
    "NomeRefeicao" VARCHAR(100) NOT NULL DEFAULT '',
    "Horario" TIME NOT NULL,
    "Descricao" VARCHAR(500) NOT NULL DEFAULT '',
    "Calorias" INTEGER,
    "Proteinas" DECIMAL,
    "Carboidratos" DECIMAL,
    "Gorduras" DECIMAL,
    "Fibra" DECIMAL,
    "Ingredientes" VARCHAR(500) NOT NULL DEFAULT '',
    "InstrucoesPreparo" VARCHAR(1000) NOT NULL DEFAULT '',
    "EstaConcluida" BOOLEAN NOT NULL DEFAULT FALSE,
    "DataConclusao" TIMESTAMP,
    "DataCriacao" TIMESTAMP NOT NULL DEFAULT NOW(),
    "DataUltimaAtualizacao" TIMESTAMP
);

-- ============================================================
-- ÍNDICES PARA MELHOR PERFORMANCE
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_analises_ia_usuario ON "analises_ia"("UsuarioID");
CREATE INDEX IF NOT EXISTS idx_historicos_usuario ON "Historicos"("UsuarioID");
CREATE INDEX IF NOT EXISTS idx_cronogramas_semanais_usuario ON "CronogramasSemanais"("UsuarioId");
CREATE INDEX IF NOT EXISTS idx_refeicoes_diarias_cronograma ON "RefeicoesDiarias"("CronogramaSemanalId");

-- ============================================================
-- FIM DO SCRIPT
-- ============================================================
