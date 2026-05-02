-- ============================================================
-- BSFM - Script de Migração do Banco de Dados
-- Versão: 1.3
-- Descrição: Adiciona campo DataNascimento, tabela RefeicoesSemana
--            e 15 receitas saudáveis pré-carregadas
-- ============================================================

-- ====== 1. ADICIONAR COLUNA DATANASCIMENTO ======
ALTER TABLE Usuarios
ADD DataNascimento DATE NULL;

UPDATE Usuarios
SET DataNascimento = DATEADD(YEAR, -Idade, GETDATE())
WHERE DataNascimento IS NULL AND Idade IS NOT NULL AND Idade > 0;

-- ====== 2. CRIAR TABELA REFEICOES_SEMANA ======
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RefeicoesSemana')
BEGIN
    CREATE TABLE RefeicoesSemana (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UsuarioId INT NOT NULL,
        DiaSemana NVARCHAR(20) NOT NULL,
        TipoRefeicao NVARCHAR(20) NOT NULL,
        NomePrato NVARCHAR(200) NOT NULL,
        Ingredientes NVARCHAR(MAX),
        ModoPreparo NVARCHAR(MAX),
        Calorias DECIMAL(10,2) DEFAULT 0,
        Proteinas DECIMAL(10,2) DEFAULT 0,
        Carboidratos DECIMAL(10,2) DEFAULT 0,
        Gorduras DECIMAL(10,2) DEFAULT 0,
        DataCriacao DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
    );
    PRINT '✅ Tabela RefeicoesSemana criada com sucesso!';
END
ELSE
    PRINT 'ℹ️ Tabela RefeicoesSemana já existe.';

-- ====== 3. CRIAR TABELA RECEITAS_PADRAO (receitas pré-carregadas) ======
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ReceitasPadrao')
BEGIN
    CREATE TABLE ReceitasPadrao (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        NomePrato NVARCHAR(200) NOT NULL,
        Categoria NVARCHAR(50) NOT NULL, -- Cafe, Almoco, Jantar, Lanche
        Ingredientes NVARCHAR(MAX),
        ModoPreparo NVARCHAR(MAX),
        Calorias DECIMAL(10,2) DEFAULT 0,
        Proteinas DECIMAL(10,2) DEFAULT 0,
        Carboidratos DECIMAL(10,2) DEFAULT 0,
        Gorduras DECIMAL(10,2) DEFAULT 0,
        DataCriacao DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ Tabela ReceitasPadrao criada com sucesso!';
END
ELSE
    PRINT 'ℹ️ Tabela ReceitasPadrao já existe.';

-- ====== 4. INSERIR 15 RECEITAS SAUDÁVEIS PRÉ-CARREGADAS ======
IF NOT EXISTS (SELECT TOP 1 * FROM ReceitasPadrao)
BEGIN
    -- CAFÉS
    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Omelete de Claras com Espinafre', 'Cafe',
     '3 claras de ovo, 1 xícara de espinafre, 1 colher de chá de azeite, sal e pimenta a gosto',
     'Refogue o espinafre no azeite. Bata as claras e despeje sobre o espinafre. Cozinhe até dourar dos dois lados. Tempere a gosto.',
     180, 22, 4, 8);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Panqueca de Banana e Aveia', 'Cafe',
     '1 banana madura, 2 colheres de aveia, 1 ovo, 1 colher de chá de canela',
     'Amasse a banana e misture com aveia, ovo e canela. Cozinhe em frigideira antiaderente até dourar dos dois lados.',
     250, 12, 35, 8);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Iogurte com Granola e Frutas', 'Cafe',
     '1 pote de iogurte natural, 2 colheres de granola, 1/2 xícara de morangos picados, 1 colher de mel',
     'Misture o iogurte com a granola. Adicione as frutas picadas e regue com mel.',
     220, 10, 35, 5);

    -- ALMOÇOS
    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Peito de Frango Grelhado com Quinoa', 'Almoco',
     '200g peito de frango, 1/2 xícara de quinoa, 1 xícara de brócolis, 2 colheres de azeite, alho e limão',
     'Tempere o frango com alho e limão. Grelhe até dourar. Cozinhe a quinoa. Refogue o brócolis no azeite. Sirva tudo junto.',
     420, 45, 30, 12);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Salmão ao Forno com Batata Doce', 'Almoco',
     '200g filé de salmão, 1 batata doce média, 1 xícara de vagem, azeite, alecrim, sal',
     'Tempere o salmão com alecrim e sal. Asse a 200°C por 20min. Cozinhe a batata doce e a vagem. Sirva com azeite.',
     480, 38, 35, 18);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Strogonoff de Frango Fit', 'Almoco',
     '200g peito de frango, 1/2 xícara de iogurte natural, 1 colher de mostarda, 1/2 xícara de cogumelos, arroz integral',
     'Corte o frango em cubos e refogue. Adicione cogumelos, mostarda e iogurte. Cozinhe até engrossar. Sirva com arroz integral.',
     380, 40, 28, 10);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Salada de Atum com Grão-de-Bico', 'Almoco',
     '1 lata de atum, 1/2 xícara de grão-de-bico cozido, alface, tomate, cebola roxa, azeite, limão',
     'Misture o atum com o grão-de-bico. Adicione alface, tomate e cebola picados. Tempere com azeite e limão.',
     320, 28, 22, 12);

    -- JANTARES
    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Sopa de Legumes com Frango', 'Jantar',
     '200g peito de frango, 2 cenouras, 1 abobrinha, 1 batata, 1 cebola, salsinha, sal',
     'Cozinhe o frango e desfie. Em outra panela, cozinhe os legumes picados. Bata no liquidificador. Volte ao fogo com o frango desfiado.',
     280, 30, 25, 5);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Wrap Integral de Frango', 'Jantar',
     '1 wrap integral, 150g frango desfiado, alface, tomate, 2 colheres de cream cheese light',
     'Aqueça o wrap. Espalhe o cream cheese. Adicione o frango desfiado, alface e tomate. Enrole e sirva.',
     320, 28, 30, 8);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Omelete de Forno com Legumes', 'Jantar',
     '3 ovos, 1/2 xícara de leite desnatado, 1/2 xícara de cenoura ralada, 1/2 xícara de abobrinha, queijo ralado',
     'Bata os ovos com o leite. Misture os legumes. Despeje em forma untada. Cubra com queijo. Asse a 180°C por 20min.',
     290, 22, 10, 16);

    -- LANCHES
    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Smoothie Verde Energético', 'Lanche',
     '1 banana, 1 xícara de espinafre, 200ml leite de amêndoas, 1 colher de pasta de amendoim',
     'Bata todos os ingredientes no liquidificador até ficar homogêneo. Sirva gelado.',
     200, 8, 30, 7);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Bolinho de Aveia e Banana', 'Lanche',
     '2 bananas, 1 xícara de aveia, 1 ovo, 1 colher de fermento, canela',
     'Amasse as bananas e misture com aveia, ovo e fermento. Modele bolinhos. Asse a 180°C por 15min.',
     180, 6, 32, 3);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Pasta de Atum com Torrada Integral', 'Lanche',
     '1 lata de atum, 2 colheres de iogurte natural, 1 colher de mostarda, 2 fatias de pão integral',
     'Misture o atum com iogurte e mostarda. Passe nas torradas integrais. Finalize com salsinha.',
     250, 22, 18, 8);

    INSERT INTO ReceitasPadrao (NomePrato, Categoria, Ingredientes, ModoPreparo, Calorias, Proteinas, Carboidratos, Gorduras)
    VALUES 
    ('Mix de Castanhas e Frutas Secas', 'Lanche',
     '30g castanhas do Pará, 30g amêndoas, 30g damasco seco, 30g uva passa',
     'Misture todos os ingredientes. Armazene em pote hermético. Porção ideal: 30g.',
     180, 5, 15, 12);

    PRINT '✅ 15 receitas saudáveis inseridas com sucesso!';
END
ELSE
    PRINT 'ℹ️ Receitas já existem na base.';

-- ====== 5. VERIFICAR ESTRUTURA ======
-- SELECT * FROM ReceitasPadrao;
-- SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Usuarios';

PRINT '============================================';
PRINT '✅ Migração v1.3 concluída com sucesso!';
PRINT '============================================';
