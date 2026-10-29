using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Application.Services.Implements;

public class BackupTaskService : IBackupTaskService
{
    private readonly ResticManagerDbContext _context;
    private readonly IResticService _resticService;
    private readonly IBackupRecordService _recordService;
    private readonly ISnapshotService _snapshotService;
    private readonly IRepositoryLockManager _repositoryLockManager;
    private readonly ILogger<BackupTaskService> _logger;

    public BackupTaskService(ResticManagerDbContext context,
        IResticService resticService,
        IBackupRecordService recordService,
        ISnapshotService snapshotService,
        IRepositoryLockManager repositoryLockManager,
        ILogger<BackupTaskService> logger)
    {
        _context = context;
        _resticService = resticService;
        _recordService = recordService;
        _snapshotService = snapshotService;
        _repositoryLockManager = repositoryLockManager;
        _logger = logger;
    }

    public async Task<List<BackupTask>> GetAllTasksAsync()
    {
        return await _context.BackupTasks
            .Include(t => t.Repository)
            .Include(t => t.Rule)
            .ToListAsync();
    }

    public async Task<BackupTask> GetTaskByIdAsync(int id)
    {
        return await _context.BackupTasks
            .Include(t => t.Repository)
            .Include(t => t.Rule)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<BackupTask> CreateTaskAsync(BackupTask task)
    {
        _context.BackupTasks.Add(task);
        await _context.SaveChangesAsync();
        _logger.LogInformation("备份任务已创建：{TaskId} {TaskName}", task.Id, task.Name);
        return task;
    }

    public async Task UpdateTaskAsync(BackupTask task)
    {
        var t = await _context.BackupTasks.FindAsync(task.Id);
        t.Name = task.Name;
        t.Description = task.Description;
        t.SourcePath = task.SourcePath;
        t.RepositoryId = task.RepositoryId;
        t.IsEnabled = task.IsEnabled;
        t.CronExpression = task.CronExpression;
        t.BackupRuleId = task.BackupRuleId;
        await _context.SaveChangesAsync();
        _logger.LogInformation("备份任务已更新：{TaskId} {TaskName}", t.Id, t.Name);
    }

    public async Task DeleteTaskAsync(int id)
    {
        var task = await _context.BackupTasks.FindAsync(id);
        if (task != null)
        {
            _context.BackupTasks.Remove(task);
            await _context.SaveChangesAsync();
            _logger.LogInformation("备份任务已删除：{TaskId} {TaskName}", id, task.Name);
        }
    }

    public async Task<int> RunTaskAsync(int taskId)
    {
        _logger.LogInformation("开始执行备份任务，ID：{TaskId}", taskId);
        
        var task = await _context.BackupTasks
            .Include(t => t.Repository)
            .Include(t => t.Rule)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) {
            _logger.LogWarning("备份任务 {TaskId} 未找到", taskId);
            throw new Exception("任务未找到");
        }

        // 检查任务是否已经在运行
        if (task.LastRunStatus == "Running") {
            _logger.LogWarning("备份任务 {TaskId} 正在运行中，拒绝重复启动", taskId);
            throw new Exception("任务正在运行中，无法再次启动");
        }

        if (!task.Repository.IsInitialized) {
            _logger.LogWarning("备份任务 {TaskId} 关联的仓库未初始化", taskId);
            throw new Exception("关联的仓库未初始化");
        }

        _logger.LogInformation("开始执行备份：{TaskName}，源路径 {SourcePath}", task.Name, task.SourcePath);
        
        // 更新任务状态为运行中
        task.LastRunAt = DateTime.Now;
        task.LastRunStatus = "Running";
        _context.Entry(task).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        // 创建备份记录
        var record = new BackupRecord
        {
            BackupTaskId = task.Id,
            StartTime = DateTime.Now,
            Status = "Running"
        };

        _context.BackupRecords.Add(record);
        await _context.SaveChangesAsync();

        // 每仓库互斥：备份全程（含保留策略与快照同步）持锁，与 AsyncTaskService 的
        // 后台任务、定时健康检查共用同一把锁。Quartz 定时与本方法直连（不经任务队列），
        // 若不在此取锁，定时备份会与同时刻的 restic check 竞争后端锁直接失败。
        await using var repoLock = await _repositoryLockManager.AcquireAsync(task.RepositoryId);

        try
        {
            // 执行备份
            string includePatterns = null;
            string excludePatterns = null;

            if (task.Rule != null)
            {
                includePatterns = task.Rule.IncludePatterns;
                excludePatterns = task.Rule.ExcludePatterns;
            }

            _logger.LogInformation("调用 restic 执行备份（任务 {TaskName}）", task.Name);
            var result = await _resticService.CreateBackupAsync(
                task.SourcePath,
                task.Repository,
                includePatterns,
                excludePatterns);

            _logger.LogInformation("备份命令执行完成（任务 {TaskName}）", task.Name);

            // CreateBackupAsync 失败时不抛异常、只返回 Success=false（如 restic 锁冲突、
            // 仓库不可达）。必须在此显式判定并走失败路径，否则失败会被当成成功落库，
            // 且后续保留策略/快照同步仍会执行，对已锁定的仓库产生连锁失败。
            if (!result.Success)
            {
                throw new Exception(result.Error ?? "备份失败");
            }

            // 更新备份记录
            record.EndTime = DateTime.Now;
            record.Status = "Succeeded";
            record.FilesProcessed = result.Summary?.TotalFilesProcessed ?? 0;
            record.DataTransferred = result.Summary?.TotalBytesProcessed ?? 0;
            record.Summary = result.Summary;
            record.Output = result.RawOutput;

            await _context.SaveChangesAsync();

            // 备份成功后按规则执行保留策略（失败仅记录日志，不影响备份成功状态）
            var retentionApplied = await ApplyRetentionAsync(task);

            // 更新任务状态。先落库再去做可能耗时的快照缓存同步，
            // 避免同步期间任务一直停留在 Running，被防重入守卫拒绝补跑。
            task.LastRunAt = DateTime.Now;
            task.LastRunStatus = "Succeeded";
            _context.Entry(task).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // 定向拉取本次快照的元数据写入本地 DB，并带上 record.Id 关联，
            // 让"快照列表"页面下次直接读本地，避开网盘远端。
            // 后面的全量同步只补全空关联、不覆盖已有值，因此必须在同步之前执行。
            // 失败仅记录日志，不影响备份成功状态。
            if (!string.IsNullOrWhiteSpace(result.Summary?.SnapshotId))
            {
                await _snapshotService.IngestNewSnapshotAsync(
                    task.RepositoryId,
                    result.Summary.SnapshotId,
                    record.Id);
            }

            // forget --prune 后把远端当前快照状态同步到本地缓存（失败仅记录日志，不影响备份成功状态）
            if (retentionApplied)
            {
                try
                {
                    await _snapshotService.SyncRepositoryAsync(task.RepositoryId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "保留策略执行后同步快照缓存失败（备份仍然成功）");
                }
            }

            _logger.LogInformation("备份任务 {TaskId} 执行成功，记录 ID：{RecordId}", taskId, record.Id);
            return record.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "备份任务 {TaskId} 执行失败", taskId);
            
            // 更新备份记录错误信息
            record.EndTime = DateTime.Now;
            record.Status = "Failed";
            record.Error = ex.Message;

            await _context.SaveChangesAsync();

            // 更新任务状态
            task.LastRunAt = DateTime.Now;
            task.LastRunStatus = "Failed";
            _context.Entry(task).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            throw;
        }
    }

    public async Task<List<BackupTask>> GetEnabledTasksAsync()
    {
        return await _context.BackupTasks
            .Where(t => t.IsEnabled)
            .Include(t => t.Rule)
            .Include(t => t.Repository)
            .ToListAsync();
    }

    /// <summary>
    /// 按关联 BackupRule 的保留策略执行 forget --prune，失败仅记录日志。
    /// 返回是否应用了保留策略（供调用方决定是否做快照缓存全量同步）。
    /// </summary>
    private async Task<bool> ApplyRetentionAsync(BackupTask task)
    {
        var rule = task.Rule;
        if (rule == null) return false;

        var hasAny = rule.KeepDaily || rule.KeepWeekly || rule.KeepMonthly || rule.KeepYearly;
        if (!hasAny) return false;

        try
        {
            var keepYearly = rule.KeepYearly && rule.KeepYearlyCount > 0
                ? (int?)rule.KeepYearlyCount
                : null;

            _logger.LogInformation("开始按保留策略清理快照：daily={Daily}，weekly={Weekly}，monthly={Monthly}，yearly={Yearly}",
                rule.KeepDailyCount, rule.KeepWeeklyCount, rule.KeepMonthlyCount, keepYearly?.ToString() ?? "off");

            await _resticService.PruneSnapshotsAsync(
                task.Repository,
                rule.KeepDailyCount,
                rule.KeepWeeklyCount,
                rule.KeepMonthlyCount,
                keepYearly);
        }
        catch (Exception ex)
        {
            // 清理失败不影响备份成功状态
            _logger.LogWarning(ex, "保留策略清理失败（备份仍然成功）");
        }

        return true;
    }
}