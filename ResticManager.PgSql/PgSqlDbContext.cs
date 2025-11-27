using Microsoft.EntityFrameworkCore;
using ResticManager.Data;
using ResticManager.Models;
namespace ResticManager.PgSql;

public class PgSqlDbContext : ResticManagerDbContext
{

    public PgSqlDbContext(DbContextOptions<ResticManagerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureForPostgreSql(modelBuilder);
    }

    private void ConfigureForPostgreSql(ModelBuilder modelBuilder)
    {
        // 配置User实体
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired();
        });

        // 配置Repository实体
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Path).IsRequired();
            entity.Property(e => e.Password).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
        });

        // 配置BackupRule实体
        modelBuilder.Entity<BackupRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IncludePatterns).HasMaxLength(2000);
            entity.Property(e => e.ExcludePatterns).HasMaxLength(2000);
        });

        // 配置BackupTask实体
        modelBuilder.Entity<BackupTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SourcePath).IsRequired();
            entity.Property(e => e.RepositoryType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.LastRunStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CronExpression).HasMaxLength(50);
        });

        // 配置BackupRecord实体
        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Error).HasMaxLength(2000);
        });
    }
}