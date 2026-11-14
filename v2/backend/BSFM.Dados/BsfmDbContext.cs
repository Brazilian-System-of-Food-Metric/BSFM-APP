using BSFM.Dados.Entidades;
using Microsoft.EntityFrameworkCore;

namespace BSFM.Dados;

/// <summary>
/// Contexto único do BSFM.
///
/// Regra de compatibilidade: nomes de tabela e de coluna das entidades que já
/// existem no banco permanecem exatamente iguais. Só o que é novo é novo — assim
/// a migração pode ser aplicada sobre o Neon/Railway atual sem renomear nada.
/// </summary>
public class BsfmDbContext : DbContext
{
    public BsfmDbContext(DbContextOptions<BsfmDbContext> options) : base(options)
    {
    }

    // --- Domínio -----------------------------------------------------------
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<AnaliseIA> AnalisesIA => Set<AnaliseIA>();
    public DbSet<HistoricoProgresso> Historicos => Set<HistoricoProgresso>();
    public DbSet<ConsumoAgua> ConsumoAgua => Set<ConsumoAgua>();
    public DbSet<RefeicaoAgendada> RefeicoesAgendadas => Set<RefeicaoAgendada>();
    public DbSet<CronogramaSemanal> CronogramasSemanais => Set<CronogramaSemanal>();
    public DbSet<RefeicaoDiaria> RefeicoesDiarias => Set<RefeicaoDiaria>();
    public DbSet<Hospital> Hospitais => Set<Hospital>();
    public DbSet<Meta> Metas => Set<Meta>();
    public DbSet<Alimento> Alimentos => Set<Alimento>();

    // --- Segurança e conformidade -----------------------------------------
    public DbSet<Sessao> Sessoes => Set<Sessao>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();
    public DbSet<ConsentimentoLGPD> ConsentimentosLGPD => Set<ConsentimentoLGPD>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------------------------------------------------------------
        // Usuarios
        // ---------------------------------------------------------------
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");

            e.Property(p => p.Nome).HasMaxLength(200).IsRequired();
            e.Property(p => p.Email).HasMaxLength(320).IsRequired();
            e.Property(p => p.SenhaHash).HasMaxLength(512).IsRequired();
            e.Property(p => p.Sexo).HasMaxLength(40);
            e.Property(p => p.TipoPessoa).HasMaxLength(60);
            e.Property(p => p.Diabetes).HasMaxLength(60);
            e.Property(p => p.Intolerancia).HasMaxLength(400);
            e.Property(p => p.VersaoTermos).HasMaxLength(40);
            e.Property(p => p.Papel).HasMaxLength(40).HasDefaultValue("usuario");
            e.Property(p => p.TotpSegredo).HasMaxLength(64);

            // Um e-mail, uma conta. Sem isto, o cadastro atual aceita duplicatas
            // e o login escolhe arbitrariamente a primeira.
            e.HasIndex(p => p.Email).IsUnique().HasDatabaseName("IX_Usuarios_Email_Unico");

            e.HasIndex(p => p.BloqueadoAte).HasDatabaseName("IX_Usuarios_BloqueadoAte");
        });

        // ---------------------------------------------------------------
        // AnalisesIA
        // ---------------------------------------------------------------
        modelBuilder.Entity<AnaliseIA>(e =>
        {
            e.ToTable("AnalisesIA");
            e.Property(p => p.Alimento).HasMaxLength(200);
            e.Property(p => p.Porcao).HasMaxLength(60);
            e.Property(p => p.ModeloVersao).HasMaxLength(60);
            e.Property(p => p.ImagemHash).HasMaxLength(64);
            e.Property(p => p.Origem).HasMaxLength(20).HasDefaultValue("foto");

            e.HasOne(p => p.Usuario)
             .WithMany()
             .HasForeignKey(p => p.UsuarioID)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => new { p.UsuarioID, p.DataAnalise })
             .HasDatabaseName("IX_AnalisesIA_Usuario_Data");
        });

        // ---------------------------------------------------------------
        // Historicos
        // ---------------------------------------------------------------
        modelBuilder.Entity<HistoricoProgresso>(e =>
        {
            e.ToTable("Historicos");

            e.HasOne(p => p.Usuario)
             .WithMany()
             .HasForeignKey(p => p.UsuarioID)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => new { p.UsuarioID, p.DataRegistro })
             .HasDatabaseName("IX_Historicos_Usuario_Data");
        });

        // ---------------------------------------------------------------
        // ConsumoAgua
        // ---------------------------------------------------------------
        modelBuilder.Entity<ConsumoAgua>(e =>
        {
            e.ToTable("ConsumoAgua");

            e.HasOne(p => p.Usuario)
             .WithMany()
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => new { p.UsuarioId, p.DataRegistro })
             .HasDatabaseName("IX_ConsumoAgua_Usuario_Data");
        });

        // ---------------------------------------------------------------
        // RefeicoesAgendadas
        // ---------------------------------------------------------------
        modelBuilder.Entity<RefeicaoAgendada>(e =>
        {
            e.ToTable("RefeicoesAgendadas");
            e.Property(p => p.DiaSemana).HasMaxLength(20);
            e.Property(p => p.TipoRefeicao).HasMaxLength(40);
            e.Property(p => p.NomePrato).HasMaxLength(200);
            e.Property(p => p.Ingredientes).HasMaxLength(2000);
            e.Property(p => p.ModoPreparo).HasMaxLength(4000);

            e.HasOne(p => p.Usuario)
             .WithMany()
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => new { p.UsuarioId, p.DiaSemana })
             .HasDatabaseName("IX_RefeicoesAgendadas_Usuario_Dia");
        });

        // ---------------------------------------------------------------
        // CronogramasSemanais + RefeicoesDiarias
        // ---------------------------------------------------------------
        modelBuilder.Entity<CronogramaSemanal>(e =>
        {
            e.ToTable("CronogramasSemanais");
            e.Property(p => p.Nome).HasMaxLength(200);

            e.HasOne(p => p.Usuario)
             .WithMany()
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(p => p.RefeicoesDiarias)
             .WithOne(r => r.CronogramaSemanal!)
             .HasForeignKey(r => r.CronogramaSemanalId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefeicaoDiaria>(e =>
        {
            e.ToTable("RefeicoesDiarias");
            e.Property(p => p.DiaSemana).HasMaxLength(20);
            e.Property(p => p.TipoRefeicao).HasMaxLength(40);
            e.Property(p => p.Descricao).HasMaxLength(2000);

            e.HasIndex(p => p.CronogramaSemanalId)
             .HasDatabaseName("IX_RefeicoesDiarias_Cronograma");
        });

        // ---------------------------------------------------------------
        // Hospitais
        // ---------------------------------------------------------------
        modelBuilder.Entity<Hospital>(e =>
        {
            e.ToTable("Hospitais");
            e.Property(p => p.NomeHospital).HasMaxLength(300).IsRequired();
            e.Property(p => p.NomeFantasia).HasMaxLength(300);
            e.Property(p => p.TipoEstabelecimento).HasMaxLength(120);
            e.Property(p => p.Endereco).HasMaxLength(300);
            e.Property(p => p.Numero).HasMaxLength(20);
            e.Property(p => p.Bairro).HasMaxLength(120);
            e.Property(p => p.Cidade).HasMaxLength(120).HasDefaultValue("São Paulo");
            e.Property(p => p.Uf).HasMaxLength(2).HasDefaultValue("SP");
            e.Property(p => p.Cep).HasMaxLength(9);
            e.Property(p => p.Telefone).HasMaxLength(40);
            e.Property(p => p.Especialidades).HasMaxLength(2000);
            e.Property(p => p.Cnes).HasMaxLength(20);

            // Chave natural do DATASUS: impede importar o mesmo estabelecimento duas vezes.
            e.HasIndex(p => p.Cnes).IsUnique().HasDatabaseName("IX_Hospitais_Cnes_Unico");
            e.HasIndex(p => new { p.Uf, p.Cidade }).HasDatabaseName("IX_Hospitais_Uf_Cidade");
            e.HasIndex(p => new { p.Latitude, p.Longitude }).HasDatabaseName("IX_Hospitais_Coordenadas");
        });

        // ---------------------------------------------------------------
        // Metas
        // ---------------------------------------------------------------
        modelBuilder.Entity<Meta>(e =>
        {
            e.ToTable("Metas");

            e.HasOne(p => p.Usuario)
             .WithMany()
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            // Apenas uma meta ativa por usuário.
            e.HasIndex(p => new { p.UsuarioId, p.Ativa })
             .HasDatabaseName("IX_Metas_Usuario_Ativa");
        });

        // ---------------------------------------------------------------
        // Alimentos (TACO)
        // ---------------------------------------------------------------
        modelBuilder.Entity<Alimento>(e =>
        {
            e.ToTable("Alimentos");
            e.Property(p => p.Nome).HasMaxLength(200).IsRequired();
            e.Property(p => p.Grupo).HasMaxLength(120);
            e.Property(p => p.Fonte).HasMaxLength(40).HasDefaultValue("TACO");

            e.HasIndex(p => p.Nome).HasDatabaseName("IX_Alimentos_Nome");
        });

        // ---------------------------------------------------------------
        // Sessoes
        // ---------------------------------------------------------------
        modelBuilder.Entity<Sessao>(e =>
        {
            e.ToTable("Sessoes");
            e.Property(p => p.Jti).HasMaxLength(64).IsRequired();
            e.Property(p => p.IpHash).HasMaxLength(64);
            e.Property(p => p.UserAgentHash).HasMaxLength(64);
            e.Property(p => p.RevogadaMotivo).HasMaxLength(120);

            e.HasOne(p => p.Usuario)
             .WithMany()
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => p.Jti).IsUnique().HasDatabaseName("IX_Sessoes_Jti_Unico");
            e.HasIndex(p => new { p.UsuarioId, p.ExpiraEm })
             .HasDatabaseName("IX_Sessoes_Usuario_Expira");
            e.HasIndex(p => p.RevogadaEm).HasDatabaseName("IX_Sessoes_RevogadaEm");
        });

        // ---------------------------------------------------------------
        // RefreshTokens
        // ---------------------------------------------------------------
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.Property(p => p.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(p => p.IpHash).HasMaxLength(64);

            e.HasOne(p => p.Sessao)
             .WithMany(s => s.RefreshTokens)
             .HasForeignKey(p => p.SessaoId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => p.TokenHash).IsUnique().HasDatabaseName("IX_RefreshTokens_Hash_Unico");
            e.HasIndex(p => new { p.SessaoId, p.ExpiraEm })
             .HasDatabaseName("IX_RefreshTokens_Sessao_Expira");
        });

        // ---------------------------------------------------------------
        // Auditoria
        // ---------------------------------------------------------------
        modelBuilder.Entity<Auditoria>(e =>
        {
            e.ToTable("Auditoria");
            e.Property(p => p.Evento).HasMaxLength(80).IsRequired();
            e.Property(p => p.Entidade).HasMaxLength(80);
            e.Property(p => p.EntidadeId).HasMaxLength(64);
            e.Property(p => p.IpHash).HasMaxLength(64);
            e.Property(p => p.DadosJson).HasMaxLength(4000);

            e.HasIndex(p => new { p.UsuarioId, p.CriadoEm })
             .HasDatabaseName("IX_Auditoria_Usuario_Data");
            e.HasIndex(p => new { p.Evento, p.CriadoEm })
             .HasDatabaseName("IX_Auditoria_Evento_Data");
        });

        // ---------------------------------------------------------------
        // ConsentimentosLGPD
        // ---------------------------------------------------------------
        modelBuilder.Entity<ConsentimentoLGPD>(e =>
        {
            e.ToTable("ConsentimentosLGPD");
            e.Property(p => p.Finalidade).HasMaxLength(60).IsRequired();
            e.Property(p => p.VersaoTermo).HasMaxLength(40).IsRequired();
            e.Property(p => p.Origem).HasMaxLength(60);
            e.Property(p => p.IpHash).HasMaxLength(64);

            e.HasOne(p => p.Usuario)
             .WithMany()
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => new { p.UsuarioId, p.Finalidade, p.DataHora })
             .HasDatabaseName("IX_Consentimentos_Usuario_Finalidade");
        });
    }
}
