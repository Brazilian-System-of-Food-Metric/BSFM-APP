# 🛠️ PASSO A PASSO PARA CORRIGIR O ERRO "column a.AnaliseEmRelacaoAMeta does not exist"

## 📋 Diagnóstico

O erro acontece porque o banco de dados no Neon **não tem as colunas novas** (`PodeConsumir`, `PontuacaoSaude`, `AnaliseEmRelacaoAMeta`, `DicaBSFM`) na tabela `analises_ia`.

---

## 🔧 PASSO 1: Executar a migração manual no Neon

1. **Acesse o Neon:**
   - Abra: https://console.neon.tech
   - Faça login com sua conta

2. **Abra o SQL Editor:**
   - Selecione seu projeto
   - Clique em "SQL Editor" no menu lateral

3. **Cole e execute o script de migração:**
   - Abra o arquivo `script_migracao_unica.sql` (está na pasta BSFM-APP)
   - Copie TODO o conteúdo
   - Cole no SQL Editor do Neon
   - Clique em "Run" (ou Ctrl+Enter)

4. **Verifique se funcionou:**
   - Execute esta consulta para confirmar:
   ```sql
   SELECT column_name, data_type, is_nullable 
   FROM information_schema.columns 
   WHERE table_name = 'analises_ia' 
   ORDER BY ordinal_position;
   ```
   - Você DEVE ver as colunas: `PodeConsumir`, `PontuacaoSaude`, `AnaliseEmRelacaoAMeta`, `DicaBSFM`

---

## 🚀 PASSO 2: Fazer deploy no Render

1. **Acesse o Render:**
   - Abra: https://dashboard.render.com
   - Selecione seu serviço (bsfm-nutri)

2. **Faça um novo deploy:**
   - Clique em "Manual Deploy"
   - Selecione "Clear build cache & deploy"
   - Aguarde o build e deploy (cerca de 2-3 minutos)

3. **Verifique os logs:**
   - Durante o startup, procure por:
     ```
     [MIGRATION] Colunas de feedback IA verificadas/criadas na tabela analises_ia.
     ```
   - Isso confirma que a migração automática rodou

---

## ✅ PASSO 3: Testar

1. **Acesse o site:**
   - https://bsfm-nutri.onrender.com/analisador-rotulo.html

2. **Faça login e teste o analisador de rótulos**

3. **Se ainda der erro, verifique os logs do Render:**
   - No dashboard do Render, clique em "Logs"
   - Procure por `[ROTULO]` para ver o que aconteceu

---

## 🔄 Caso o erro persista (Plano B)

Se mesmo após os passos acima o erro continuar, execute este SQL **diretamente no Neon** (mais agressivo):

```sql
-- Remove e recria a tabela com todas as colunas (cuidado: apaga dados existentes)
DROP TABLE IF EXISTS "analises_ia" CASCADE;

CREATE TABLE "analises_ia" (
    "ID" SERIAL PRIMARY KEY,
    "UsuarioID" INTEGER NOT NULL REFERENCES "Usuarios"("ID"),
    "Alimento" TEXT NOT NULL DEFAULT '',
    "Calorias" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Proteinas" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Carbos" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Gorduras" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "Porcao" TEXT NOT NULL DEFAULT '',
    "DataAnalise" TIMESTAMP NOT NULL DEFAULT NOW(),
    "PodeConsumir" BOOLEAN DEFAULT NULL,
    "PontuacaoSaude" INTEGER NOT NULL DEFAULT 0,
    "AnaliseEmRelacaoAMeta" TEXT NOT NULL DEFAULT '',
    "DicaBSFM" TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS idx_analises_ia_usuario ON "analises_ia"("UsuarioID");
```

Depois faça outro deploy no Render.

---

## 📌 Resumo das correções feitas no código

| Arquivo | O que foi corrigido |
|---------|-------------------|
| `Program.cs` | Migração automática agora busca na tabela `analises_ia` (minúsculo) em vez de `AnalisesIA` (maiúsculo) |
| `PonteDB.cs` | `ToTable("analises_ia")` em vez de `ToTable("AnalisesIA")` |
| `script_migracao_unica.sql` | Nome da tabela corrigido para `AnalisesIA` (maiúsculo, padrão do script completo) |

**Causa raiz:** O banco de produção foi criado com `script_sql_criar_tabelas.sql` que usa `"analises_ia"` (minúsculo), mas o código estava procurando `"AnalisesIA"` (maiúsculo). PostgreSQL diferencia maiúsculas de minúsculas em nomes de tabelas com aspas duplas.
