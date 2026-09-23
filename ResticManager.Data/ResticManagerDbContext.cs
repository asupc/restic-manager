using Microsoft.EntityFrameworkCore;
using ResticManager.Models;

namespace ResticManager.Data
{
    public class ResticManagerDbContext : DbContext
    {

        public ResticManagerDbContext(DbContextOptions<ResticManagerDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Repository> Repositories { get; set; }
        public DbSet<BackupRule> BackupRules { get; set; }
        public DbSet<BackupTask> BackupTasks { get; set; }
        public DbSet<BackupRecord> BackupRecords { get; set; }
        public DbSet<BackupSnapshot> BackupSnapshots { get; set; }
        public DbSet<RepositoryCheckRecord> RepositoryCheckRecords { get; set; }
        public DbSet<AsyncTask> AsyncTasks { get; set; }
        public DbSet<MaintenanceSetting> MaintenanceSettings { get; set; }
        //public DbSet<BackupFile> BackupFiles { get; set; }
        // 移除SnapshotFiles实体，因为我们不再缓存快照文件信息到数据库
        // public DbSet<SnapshotFile> SnapshotFiles { get; set; }
    }
}