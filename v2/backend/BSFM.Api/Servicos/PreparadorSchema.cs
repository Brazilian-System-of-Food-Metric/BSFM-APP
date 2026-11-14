using BSFM.Dados;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Api.Servicos;

/// <summary>
/// Prepara um banco Postgres **que já existe** (schema da v1) para a v2.
///
/// Por que isto existe: a migration inicial foi gerada contra um banco vazio, então
/// ela começa com <c>CREATE TABLE "Usuarios"</c>. No banco real a tabela já existe,
/// a migration falha, e o app ficava servindo consultas que pediam colunas novas
/// (<c>u."AtualizadoEm"</c>) — erro 42703 no login.
///
/// A estratégia é DDL aditivo e idempotente: adiciona colunas que faltam, cria
/// tabelas novas, cria índices, e registra a migration inicial como já aplicada
/// (baseline) para que migrations futuras sejam incrementais de verdade.
///
/// Nada é removido nem renomeado.
/// </summary>
public static class PreparadorSchema
{
    /// <summary>Id da migration inicial, usado no baseline.</summary>
    private const string MigracaoInicial = "20261113233234_Inicial";
    private const string VersaoEfCore = "10.0.15";

    /// <summary>
    /// Executa o preparo aditivo. Devolve <c>true</c> quando o banco já existia e foi
    /// ajustado; <c>false</c> quando é um banco novo (o chamador deve rodar Migrate).
    /// </summary>
    public static bool ExecutarAditivo(BsfmDbContext db, ILogger logger)
    {
        if (!db.Database.IsNpgsql())
            return false;

        if (!TabelaExiste(db, "Usuarios"))
            return false;

        logger.LogInformation("Banco existente detectado: aplicando schema aditivo da v2.");

        var comandos = new List<string>();

        // ---- colunas novas em tabelas existentes --------------------------
        comandos.AddRange(new[]
        {
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"CriadoEm\" timestamp with time zone NOT NULL DEFAULT now();",
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"AtualizadoEm\" timestamp with time zone;",
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"SenhaAtualizadaEm\" timestamp with time zone;",
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"TotpSegredo\" character varying(64);",
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"TotpHabilitado\" boolean NOT NULL DEFAULT false;",
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"Papel\" character varying(40) NOT NULL DEFAULT 'usuario';",
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"TentativasFalhas\" integer NOT NULL DEFAULT 0;",
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"BloqueadoAte\" timestamp with time zone;",
            "ALTER TABLE \"Usuarios\" ADD COLUMN IF NOT EXISTS \"TokenVerificacaoExpiraEm\" timestamp with time zone;",

            "ALTER TABLE \"AnalisesIA\" ADD COLUMN IF NOT EXISTS \"Confianca\" double precision;",
            "ALTER TABLE \"AnalisesIA\" ADD COLUMN IF NOT EXISTS \"ModeloVersao\" character varying(60);",
            "ALTER TABLE \"AnalisesIA\" ADD COLUMN IF NOT EXISTS \"ImagemHash\" character varying(64);",
            "ALTER TABLE \"AnalisesIA\" ADD COLUMN IF NOT EXISTS \"Origem\" character varying(20) NOT NULL DEFAULT 'foto';",

            "ALTER TABLE \"RefeicoesAgendadas\" ADD COLUMN IF NOT EXISTS \"Concluida\" boolean NOT NULL DEFAULT false;",
            "ALTER TABLE \"RefeicoesAgendadas\" ADD COLUMN IF NOT EXISTS \"ConcluidaEm\" timestamp with time zone;",

            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Cnes\" character varying(20);",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"NomeFantasia\" character varying(300);",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"TipoEstabelecimento\" character varying(120);",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Numero\" character varying(20);",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Bairro\" character varying(120);",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Cidade\" character varying(120) NOT NULL DEFAULT 'São Paulo';",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Uf\" character varying(2) NOT NULL DEFAULT 'SP';",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Cep\" character varying(9);",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Latitude\" double precision;",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Longitude\" double precision;",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Especialidades\" character varying(2000);",
            "ALTER TABLE \"Hospitais\" ADD COLUMN IF NOT EXISTS \"Ativo\" boolean NOT NULL DEFAULT true;",
        });

        // ---- tabelas novas -------------------------------------------------
        comandos.AddRange(new[]
        {
            """
            CREATE TABLE IF NOT EXISTS "Sessoes" (
                "Id" uuid NOT NULL, "UsuarioId" integer NOT NULL, "Jti" character varying(64) NOT NULL,
                "CriadaEm" timestamp with time zone NOT NULL, "ExpiraEm" timestamp with time zone NOT NULL,
                "RevogadaEm" timestamp with time zone, "UltimoUsoEm" timestamp with time zone,
                "IpHash" character varying(64), "UserAgentHash" character varying(64),
                "RevogadaMotivo" character varying(120),
                CONSTRAINT "PK_Sessoes" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Sessoes_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("ID") ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS "RefreshTokens" (
                "Id" uuid NOT NULL, "SessaoId" uuid NOT NULL, "TokenHash" character varying(64) NOT NULL,
                "CriadoEm" timestamp with time zone NOT NULL, "ExpiraEm" timestamp with time zone NOT NULL,
                "RevogadoEm" timestamp with time zone, "SubstituidoPorId" uuid, "IpHash" character varying(64),
                CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_RefreshTokens_Sessoes_SessaoId" FOREIGN KEY ("SessaoId") REFERENCES "Sessoes" ("Id") ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS "Auditoria" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY, "UsuarioId" integer,
                "Evento" character varying(80) NOT NULL, "Entidade" character varying(80),
                "EntidadeId" character varying(64), "DadosJson" character varying(4000),
                "IpHash" character varying(64), "CriadoEm" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_Auditoria" PRIMARY KEY ("Id")
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS "ConsentimentosLGPD" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY, "UsuarioId" integer NOT NULL,
                "Finalidade" character varying(60) NOT NULL, "VersaoTermo" character varying(40) NOT NULL,
                "Concedido" boolean NOT NULL, "DataHora" timestamp with time zone NOT NULL,
                "IpHash" character varying(64), "Origem" character varying(60),
                CONSTRAINT "PK_ConsentimentosLGPD" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ConsentimentosLGPD_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("ID") ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS "Metas" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY, "UsuarioId" integer NOT NULL,
                "CaloriasAlvo" double precision NOT NULL, "ProteinasAlvo" double precision NOT NULL,
                "AguaAlvoMl" double precision NOT NULL, "PesoAlvo" double precision NOT NULL,
                "CriadoEm" timestamp with time zone NOT NULL, "Ativa" boolean NOT NULL,
                CONSTRAINT "PK_Metas" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Metas_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("ID") ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS "Alimentos" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY, "Nome" character varying(200) NOT NULL,
                "Grupo" character varying(120) NOT NULL, "Calorias100g" double precision NOT NULL,
                "Proteina100g" double precision NOT NULL, "Carboidrato100g" double precision NOT NULL,
                "Gordura100g" double precision NOT NULL, "Fibra100g" double precision NOT NULL,
                "Fonte" character varying(40) NOT NULL DEFAULT 'TACO',
                CONSTRAINT "PK_Alimentos" PRIMARY KEY ("Id")
            );
            """,
        });

        // ---- índices -------------------------------------------------------
        comandos.AddRange(new[]
        {
            "CREATE INDEX IF NOT EXISTS \"IX_Usuarios_BloqueadoAte\" ON \"Usuarios\" (\"BloqueadoAte\");",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Sessoes_Jti_Unico\" ON \"Sessoes\" (\"Jti\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Sessoes_Usuario_Expira\" ON \"Sessoes\" (\"UsuarioId\", \"ExpiraEm\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Sessoes_RevogadaEm\" ON \"Sessoes\" (\"RevogadaEm\");",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_RefreshTokens_Hash_Unico\" ON \"RefreshTokens\" (\"TokenHash\");",
            "CREATE INDEX IF NOT EXISTS \"IX_RefreshTokens_Sessao_Expira\" ON \"RefreshTokens\" (\"SessaoId\", \"ExpiraEm\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Auditoria_Usuario_Data\" ON \"Auditoria\" (\"UsuarioId\", \"CriadoEm\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Auditoria_Evento_Data\" ON \"Auditoria\" (\"Evento\", \"CriadoEm\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Consentimentos_Usuario_Finalidade\" ON \"ConsentimentosLGPD\" (\"UsuarioId\", \"Finalidade\", \"DataHora\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Metas_Usuario_Ativa\" ON \"Metas\" (\"UsuarioId\", \"Ativa\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Alimentos_Nome\" ON \"Alimentos\" (\"Nome\");",
            "CREATE INDEX IF NOT EXISTS \"IX_AnalisesIA_Usuario_Data\" ON \"AnalisesIA\" (\"UsuarioID\", \"DataAnalise\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Historicos_Usuario_Data\" ON \"Historicos\" (\"UsuarioID\", \"DataRegistro\");",
            "CREATE INDEX IF NOT EXISTS \"IX_ConsumoAgua_Usuario_Data\" ON \"ConsumoAgua\" (\"UsuarioId\", \"DataRegistro\");",
            "CREATE INDEX IF NOT EXISTS \"IX_RefeicoesAgendadas_Usuario_Dia\" ON \"RefeicoesAgendadas\" (\"UsuarioId\", \"DiaSemana\");",
            "CREATE INDEX IF NOT EXISTS \"IX_RefeicoesDiarias_Cronograma\" ON \"RefeicoesDiarias\" (\"CronogramaSemanalId\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Hospitais_Uf_Cidade\" ON \"Hospitais\" (\"Uf\", \"Cidade\");",
            "CREATE INDEX IF NOT EXISTS \"IX_Hospitais_Coordenadas\" ON \"Hospitais\" (\"Latitude\", \"Longitude\");",
        });

        // Índice único de e-mail: só quando não há duplicata. Se houver, criamos um
        // índice comum e registramos aviso — derrubar o app por dado legado é pior.
        comandos.Add("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM (SELECT lower("Email") FROM "Usuarios" GROUP BY 1 HAVING count(*) > 1) d) THEN
                    CREATE INDEX IF NOT EXISTS "IX_Usuarios_Email" ON "Usuarios" ("Email");
                ELSE
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Usuarios_Email_Unico" ON "Usuarios" ("Email");
                END IF;
            END $$;
            """);

        // ---- baseline das migrations --------------------------------------
        comandos.Add("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """);
        comandos.Add(
            $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
            $"VALUES ('{MigracaoInicial}', '{VersaoEfCore}') ON CONFLICT DO NOTHING;");

        var aplicados = 0;
        foreach (var comando in comandos)
        {
            try
            {
                db.Database.ExecuteSqlRaw(comando);
                aplicados++;
            }
            catch (Exception excecao)
            {
                // Um comando isolado não derruba os demais: schema é aditivo.
                logger.LogWarning(excecao, "Comando de preparo ignorado: {Trecho}",
                    comando.Length > 90 ? comando[..90] : comando);
            }
        }

        logger.LogInformation("Schema aditivo concluído: {Aplicados}/{Total} comandos.", aplicados, comandos.Count);
        return true;
    }

    private static bool TabelaExiste(BsfmDbContext db, string tabela)
    {
        try
        {
            var resultado = db.Database
                .SqlQueryRaw<int>(
                    "SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name = {0}",
                    tabela)
                .AsEnumerable()
                .FirstOrDefault();

            return resultado > 0;
        }
        catch
        {
            return false;
        }
    }
}
