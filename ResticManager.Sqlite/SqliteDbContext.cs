using Microsoft.EntityFrameworkCore;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Sqlite;

public class SqliteDbContext : ResticManagerDbContext
{
    public SqliteDbContext(DbContextOptions<ResticManagerDbContext> options) : base(options)
    {
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureForSqlite(modelBuilder);
    }

    private void ConfigureForSqlite(ModelBuilder modelBuilder)
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
            // 后端凭据 JSON 不限长
            entity.Property(e => e.CredentialsJson);
            // 统计结果 JSON 不限长
            entity.Property(e => e.StatsJson);
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

        // 配置BackupSnapshot实体：远端快照元数据的本地缓存
        modelBuilder.Entity<BackupSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SnapshotId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ShortId).HasMaxLength(20);
            entity.Property(e => e.Tree).HasMaxLength(200);
            entity.Property(e => e.Hostname).HasMaxLength(100);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.ProgramVersion).HasMaxLength(50);
            entity.Property(e => e.ParentSnapshotId).HasMaxLength(64);
            // 路径/标签/summary 的 JSON 列不限长：快照可能带很多条长路径，
            // 超长会导致 PgSql 截断报错、同步整仓失败
            entity.Property(e => e.PathsJson);
            entity.Property(e => e.TagsJson);
            entity.Property(e => e.SummaryJson);
            // SQLite TEXT 往返会丢失 DateTimeKind；统一按 UTC 存储、以 UTC 读回，
            // 保证序列化输出与直连远端时一致（Npgsql timestamptz 也要求 UTC Kind）
            entity.Property(e => e.Time).HasConversion(
                v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(e => e.LastSyncedAt).HasConversion(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
            // 记录被级联删除时快照缓存行保留，仅清空引用
            entity.HasOne(e => e.BackupRecord)
                .WithMany()
                .HasForeignKey(e => e.BackupRecordId)
                .OnDelete(DeleteBehavior.SetNull);
            // (RepositoryId, SnapshotId) 复合唯一索引，避免重复插入同一仓库同一快照
            entity.HasIndex(e => new { e.RepositoryId, e.SnapshotId }).IsUnique();
            // 按仓库+时间倒序查时走索引
            entity.HasIndex(e => new { e.RepositoryId, e.Time });
        });

        // 配置RepositoryCheckRecord实体
        modelBuilder.Entity<RepositoryCheckRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Trigger).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasMaxLength(4000);
            entity.HasIndex(e => new { e.RepositoryId, e.CheckedAt });
        });

        // 配置AsyncTask实体：后台异步任务（仓库类操作的执行记录与结果）
        modelBuilder.Entity<AsyncTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(40);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TargetKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Trigger).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Error).HasMaxLength(4000);
            // ParametersJson / ResultJson 不限长：文件树、find 等结果可能很大
            entity.Property(e => e.ParametersJson);
            entity.Property(e => e.ResultJson);
            // SQLite TEXT 往返会丢失 DateTimeKind；时间字段统一按 UTC 存储、以 UTC 读回
            entity.Property(e => e.CreatedAt).HasConversion(
                v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(e => e.StartTime).HasConversion(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
            entity.Property(e => e.EndTime).HasConversion(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
            // 仓库被删除时任务历史保留，仅清空引用
            entity.HasOne(e => e.Repository)
                .WithMany()
                .HasForeignKey(e => e.RepositoryId)
                .OnDelete(DeleteBehavior.SetNull);
            // 活动任务查询 / 按仓库恢复轮询 / 最近完成查询
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.RepositoryId, e.Status });
            entity.HasIndex(e => e.EndTime);
        });

        // 配置MaintenanceSetting实体：定时维护任务的开关与执行计划
        modelBuilder.Entity<MaintenanceSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(40);
            entity.Property(e => e.CronExpression).IsRequired().HasMaxLength(50);
            // SQLite TEXT 往返会丢失 DateTimeKind；与 AsyncTask 时间列同模式按 UTC 存储
            entity.Property(e => e.UpdatedAt).HasConversion(
                v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            // 每种维护类型全局仅一行配置
            entity.HasIndex(e => e.Type).IsUnique();
        });
    }
}