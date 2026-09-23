using Microsoft.EntityFrameworkCore;
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

    public BackupTaskService(ResticManagerDbContext context,
        IResticService resticService,
        IBackupRecordService recordService,
        ISnapshotService snapshotService)
    {
        _context = context;
        _resticService = resticService;
        _recordService = recordService;
        _snapshotService = snapshotService;
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
    }

    public async Task DeleteTaskAsync(int id)
    {
        var task = await _context.BackupTasks.FindAsync(id);
        if (task != null)
        {
            _context.BackupTasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> RunTaskAsync(int taskId)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RunTaskAsync 开始执行任务 ID: {taskId}");
        
        var task = await _context.BackupTasks
            .Include(t => t.Repository)
            .Include(t => t.Rule)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 未找到");
            throw new Exception("任务未找到");
        }

        // 检查任务是否已经在运行
        if (task.LastRunStatus == "Running") {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 正在运行中，无法再次启动");
            throw new Exception("任务正在运行中，无法再次启动");
        }

        if (!task.Repository.IsInitialized) {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 关联的仓库未初始化");
            throw new Exception("关联的仓库未初始化");
        }
        
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始执行备份任务：{task.Name}");
        
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

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调用 ResticService 执行备份");
            var result = await _resticService.CreateBackupAsync(
                task.SourcePath,
                task.Repository.Path,
                task.Repository.Password,
                includePatterns,
                excludePatterns);

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 备份执行完成");

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
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 保留策略后同步快照缓存失败（备份仍然成功）: {ex.Message}");
                }
            }

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 执行成功");
            return record.Id;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 执行失败: {ex}");
            
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

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始按保留策略清理快照: " +
                $"daily={rule.KeepDailyCount}, weekly={rule.KeepWeeklyCount}, monthly={rule.KeepMonthlyCount}, yearly={(keepYearly?.ToString() ?? "off")}");

            await _resticService.PruneSnapshotsAsync(
                task.Repository.Path,
                task.Repository.Password,
                rule.KeepDailyCount,
                rule.KeepWeeklyCount,
                rule.KeepMonthlyCount,
                keepYearly);
        }
        catch (Exception ex)
        {
            // 清理失败不影响备份成功状态
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 保留策略清理失败（备份仍然成功）: {ex.Message}");
        }

        return true;
    }
}