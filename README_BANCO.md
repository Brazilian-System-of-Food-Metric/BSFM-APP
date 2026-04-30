# 🗄️ BSFM - Guia de Banco de Dados

## 📋 Scripts Disponíveis

| Arquivo | Quando usar | Descrição |
|---------|-------------|-----------|
| `script_sql_completo.sql` | **NOVO banco** (Neon vazio) | Cria TODAS as tabelas com TODAS as colunas |
| `script_migracao_unica.sql` | **Banco EXISTENTE** (já tem dados) | Adiciona colunas que faltam sem perder dados |

---

## 🆕 Para um NOVO banco (Neon vazio)

Execute **APENAS** o `script_sql_completo.sql`:

```sql
-- 1. Abra o SQL Editor do Neon
-- 2. Cole todo o conteúdo de script_sql_completo.sql
-- 3. Execute (Ctrl+Enter)
```

Este script já contém:
- ✅ Tabela `Usuarios` com colunas `Diabetes` e `Intolerancia`
- ✅ Tabela `AnalisesIA` com colunas `PodeConsumir`, `PontuacaoSaude`, `AnaliseEmRelacaoAMeta`, `DicaBSFM`
- ✅ Todas as outras tabelas (Refeicoes, Comidas, Cronogramas, etc.)
- ✅ Índices de performance
- ✅ Receitas saudáveis pré-carregadas

---

## 🔄 Para um banco EXISTENTE (já tem dados)

Execute o `script_migracao_unica.sql` para adicionar as colunas que faltam:

```sql
-- 1. Abra o SQL Editor do Neon
-- 2. Cole todo o conteúdo de script_migracao_unica.sql
-- 3. Execute (Ctrl+Enter)
```

Este script adiciona:
- ✅ Coluna `Diabetes` na tabela `Usuarios` (se não existir)
- ✅ Coluna `Intolerancia` na tabela `Usuarios` (se não existir)
- ✅ Coluna `PodeConsumir` na tabela `analises_ia` (se não existir)
- ✅ Coluna `PontuacaoSaude` na tabela `analises_ia` (se não existir)
- ✅ Coluna `AnaliseEmRelacaoAMeta` na tabela `analises_ia` (se não existir)
- ✅ Coluna `DicaBSFM` na tabela `analises_ia` (se não existir)

> ⚠️ **Seguro:** O script usa `IF NOT EXISTS`, então pode ser executado múltiplas vezes sem causar erros.

---

## 🚀 Após executar a migração

1. **Reinicie o servidor no Render:**
   - Acesse: https://dashboard.render.com
   - Selecione seu serviço
   - Clique em "Manual Deploy" > "Clear build cache & deploy"

2. **Verifique se o login funciona:**
   - Acesse: https://bsfm-nutri.onrender.com/login
   - Faça login com suas credenciais

3. **Se ainda der erro `column u.Diabetes does not exist`:**
   - A migração automática no `Program.cs` também tenta criar as colunas no startup
   - Verifique os logs do Render para confirmar se a migração rodou
   - Se necessário, execute o `script_migracao_unica.sql` manualmente no Neon

---

## 🔍 Verificar se a migração funcionou

No SQL Editor do Neon, execute:

```sql
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'Usuarios' 
ORDER BY ordinal_position;
```

Você deve ver as colunas `Diabetes` e `Intolerancia` na lista.

```sql
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'analises_ia' 
ORDER BY ordinal_position;
```

Você deve ver as colunas `PodeConsumir`, `PontuacaoSaude`, `AnaliseEmRelacaoAMeta` e `DicaBSFM` na lista.
