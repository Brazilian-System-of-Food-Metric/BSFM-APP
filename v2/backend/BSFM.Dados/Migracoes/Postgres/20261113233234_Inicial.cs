using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BSFM.Dados.Migracoes.Postgres
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alimentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Grupo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Calorias100g = table.Column<double>(type: "double precision", nullable: false),
                    Proteina100g = table.Column<double>(type: "double precision", nullable: false),
                    Carboidrato100g = table.Column<double>(type: "double precision", nullable: false),
                    Gordura100g = table.Column<double>(type: "double precision", nullable: false),
                    Fibra100g = table.Column<double>(type: "double precision", nullable: false),
                    Fonte = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "TACO")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alimentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    Evento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Entidade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EntidadeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DadosJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hospitais",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cnes = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NomeHospital = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NomeFantasia = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TipoEstabelecimento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Endereco = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, defaultValue: "São Paulo"),
                    Uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "SP"),
                    Cep = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    Telefone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Especialidades = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hospitais", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Idade = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    EmailVerificado = table.Column<bool>(type: "boolean", nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SenhaAtualizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AceitouTermos = table.Column<bool>(type: "boolean", nullable: false),
                    DataAceite = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VersaoTermos = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Sexo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Peso = table.Column<double>(type: "double precision", nullable: false),
                    Altura = table.Column<double>(type: "double precision", nullable: false),
                    PesoMeta = table.Column<double>(type: "double precision", nullable: false),
                    DataNascimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TipoPessoa = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Intolerancia = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Diabetes = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IMC = table.Column<double>(type: "double precision", nullable: false),
                    TMB = table.Column<double>(type: "double precision", nullable: false),
                    GastoTotal = table.Column<double>(type: "double precision", nullable: false),
                    TotpSegredo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TotpHabilitado = table.Column<bool>(type: "boolean", nullable: false),
                    Papel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "usuario"),
                    TentativasFalhas = table.Column<int>(type: "integer", nullable: false),
                    BloqueadoAte = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TokenVerificacao = table.Column<string>(type: "text", nullable: true),
                    TokenVerificacaoExpiraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AnalisesIA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioID = table.Column<int>(type: "integer", nullable: false),
                    Alimento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Calorias = table.Column<double>(type: "double precision", nullable: false),
                    Proteinas = table.Column<double>(type: "double precision", nullable: false),
                    Carbos = table.Column<double>(type: "double precision", nullable: false),
                    Gorduras = table.Column<double>(type: "double precision", nullable: false),
                    Porcao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DataAnalise = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PodeConsumir = table.Column<bool>(type: "boolean", nullable: true),
                    PontuacaoSaude = table.Column<int>(type: "integer", nullable: false),
                    AnaliseEmRelacaoAMeta = table.Column<string>(type: "text", nullable: false),
                    DicaBSFM = table.Column<string>(type: "text", nullable: false),
                    Confianca = table.Column<double>(type: "double precision", nullable: true),
                    ModeloVersao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ImagemHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "foto")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisesIA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AnalisesIA_Usuarios_UsuarioID",
                        column: x => x.UsuarioID,
                        principalTable: "Usuarios",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsentimentosLGPD",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Finalidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    VersaoTermo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Concedido = table.Column<bool>(type: "boolean", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Origem = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentimentosLGPD", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentimentosLGPD_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsumoAgua",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Ml = table.Column<double>(type: "double precision", nullable: false),
                    DataRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumoAgua", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumoAgua_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CronogramasSemanais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CronogramasSemanais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CronogramasSemanais_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Historicos",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioID = table.Column<int>(type: "integer", nullable: false),
                    Peso = table.Column<double>(type: "double precision", nullable: false),
                    Altura = table.Column<double>(type: "double precision", nullable: false),
                    IMC = table.Column<double>(type: "double precision", nullable: false),
                    DataRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Historicos", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Historicos_Usuarios_UsuarioID",
                        column: x => x.UsuarioID,
                        principalTable: "Usuarios",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Metas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    CaloriasAlvo = table.Column<double>(type: "double precision", nullable: false),
                    ProteinasAlvo = table.Column<double>(type: "double precision", nullable: false),
                    AguaAlvoMl = table.Column<double>(type: "double precision", nullable: false),
                    PesoAlvo = table.Column<double>(type: "double precision", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Metas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefeicoesAgendadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoRefeicao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NomePrato = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ingredientes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ModoPreparo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Calorias = table.Column<double>(type: "double precision", nullable: false),
                    Proteinas = table.Column<double>(type: "double precision", nullable: false),
                    Carboidratos = table.Column<double>(type: "double precision", nullable: false),
                    Gorduras = table.Column<double>(type: "double precision", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefeicoesAgendadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefeicoesAgendadas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Jti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevogadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimoUsoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RevogadaMotivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessoes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefeicoesDiarias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CronogramaSemanalId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoRefeicao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Calorias = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefeicoesDiarias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefeicoesDiarias_CronogramasSemanais_CronogramaSemanalId",
                        column: x => x.CronogramaSemanalId,
                        principalTable: "CronogramasSemanais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevogadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubstituidoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    IpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Sessoes_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "Sessoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alimentos_Nome",
                table: "Alimentos",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_AnalisesIA_Usuario_Data",
                table: "AnalisesIA",
                columns: new[] { "UsuarioID", "DataAnalise" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Evento_Data",
                table: "Auditoria",
                columns: new[] { "Evento", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Usuario_Data",
                table: "Auditoria",
                columns: new[] { "UsuarioId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Consentimentos_Usuario_Finalidade",
                table: "ConsentimentosLGPD",
                columns: new[] { "UsuarioId", "Finalidade", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumoAgua_Usuario_Data",
                table: "ConsumoAgua",
                columns: new[] { "UsuarioId", "DataRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_CronogramasSemanais_UsuarioId",
                table: "CronogramasSemanais",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Historicos_Usuario_Data",
                table: "Historicos",
                columns: new[] { "UsuarioID", "DataRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_Hospitais_Cnes_Unico",
                table: "Hospitais",
                column: "Cnes",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hospitais_Coordenadas",
                table: "Hospitais",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Hospitais_Uf_Cidade",
                table: "Hospitais",
                columns: new[] { "Uf", "Cidade" });

            migrationBuilder.CreateIndex(
                name: "IX_Metas_Usuario_Ativa",
                table: "Metas",
                columns: new[] { "UsuarioId", "Ativa" });

            migrationBuilder.CreateIndex(
                name: "IX_RefeicoesAgendadas_Usuario_Dia",
                table: "RefeicoesAgendadas",
                columns: new[] { "UsuarioId", "DiaSemana" });

            migrationBuilder.CreateIndex(
                name: "IX_RefeicoesDiarias_Cronograma",
                table: "RefeicoesDiarias",
                column: "CronogramaSemanalId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Hash_Unico",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Sessao_Expira",
                table: "RefreshTokens",
                columns: new[] { "SessaoId", "ExpiraEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_Jti_Unico",
                table: "Sessoes",
                column: "Jti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_RevogadaEm",
                table: "Sessoes",
                column: "RevogadaEm");

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_Usuario_Expira",
                table: "Sessoes",
                columns: new[] { "UsuarioId", "ExpiraEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_BloqueadoAte",
                table: "Usuarios",
                column: "BloqueadoAte");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email_Unico",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alimentos");

            migrationBuilder.DropTable(
                name: "AnalisesIA");

            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "ConsentimentosLGPD");

            migrationBuilder.DropTable(
                name: "ConsumoAgua");

            migrationBuilder.DropTable(
                name: "Historicos");

            migrationBuilder.DropTable(
                name: "Hospitais");

            migrationBuilder.DropTable(
                name: "Metas");

            migrationBuilder.DropTable(
                name: "RefeicoesAgendadas");

            migrationBuilder.DropTable(
                name: "RefeicoesDiarias");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "CronogramasSemanais");

            migrationBuilder.DropTable(
                name: "Sessoes");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
