using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Application.Services.Implements;

/// <summary>
/// 后台异步任务服务（Singleton）。
/// 所有访问 restic 仓库的慢操作在 HTTP 请求内只做参数校验和登记（CreateTaskAsync 落库
/// 一条 Pending 任务后立即返回任务 ID），实际执行由 Task.Run 在后台完成。
///
/// 并发控制：全局信号量限制同时在跑的 restic 进程数；每个仓库一把信号量串行化对同一
/// 仓库的访问（restic 仓库并发读写会产生锁冲突，网盘仓库尤其敏感）。
/// RepositoryUnlock 例外：它是解除仓库死锁的工具，不能被同仓库的其它任务阻塞，
/// 因此不取仓库信号量、也不做防重入拒绝。
///
/// 同时实现 IHostedService：启动时把上次进程退出遗留的 Pending/Running 任务标记为失败，
/// 并复位备份任务侧的状态位（详见 CleanUpOnStartupAsync）。
/// </summary>
public class AsyncTaskService : IAsyncTaskService, IHostedService
{
    private const string StatusPending = "Pending";
    private const string StatusRunning = "Running";
    private const string StatusSucceeded = "Succeeded";
    private const string StatusFailed = "Failed";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AsyncTaskService> _logger;

    /// <summary>全局并发闸：避免多个仓库的任务同时打满网盘带宽/连接数</summary>
    private static readonly SemaphoreSlim GlobalGate = new(3, 3);

    /// <summary>每仓库一把锁：串行化对同一 restic 仓库的访问，防锁冲突。条目不回收（仓库数量有限）</summary>
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> RepoLocks = new();

    public AsyncTaskService(IServiceScopeFactory scopeFactory, ILogger<AsyncTaskService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<int> CreateTaskAsync(string type, int? repositoryId, string title, object parameters, string targetKey = "", string trigger = "Manual")
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ResticManagerDbContext>();

        // 防重入：同 (Type, RepositoryId, TargetKey) 已有在途任务时拒绝。
        // RepositoryUnlock 是解除死锁的工具，用户可能正被某个卡死任务挡住，不做拒绝。
        if (type != AsyncTaskTypes.RepositoryUnlock)
        {
            var duplicated = await context.AsyncTasks.AnyAsync(t =>
                t.Type == type &&
                t.RepositoryId == repositoryId &&
                t.TargetKey == (targetKey ?? "") &&
                (t.Status == StatusPending || t.Status == StatusRunning));
            if (duplicated)
                throw new InvalidOperationException("该操作已有同类任务正在进行中，请稍后再试");
        }

        // BackupRun 额外预检备份任务的状态位，把"Quartz 定时正在执行"的冲突拦截在提交时刻
        if (type == AsyncTaskTypes.BackupRun && int.TryParse(targetKey, out var backupTaskId))
        {
            var running = await context.BackupTasks.AnyAsync(t => t.Id == backupTaskId && t.LastRunStatus == "Running");
            if (running)
                throw new InvalidOperationException("任务正在运行中，无法再次启动");
        }

        var entity = new AsyncTask
        {
            Type = type,
            RepositoryId = repositoryId,
            Title = title,
            TargetKey = targetKey ?? "",
            ParametersJson = JsonSerializer.Serialize(parameters, JsonOptions),
            Status = StatusPending,
            Trigger = trigger,
            CreatedAt = DateTime.UtcNow
        };
        context.AsyncTasks.Add(entity);
        await context.SaveChangesAsync();

        // 后台执行；RunAsync 内部全量捕获异常，不会产生未观察异常
        _ = Task.Run(() => RunAsync(entity.Id));
        return entity.Id;
    }

    public async Task<AsyncTask?> GetTaskAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ResticManagerDbContext>();
        return await context.AsyncTasks.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<AsyncTask>> GetActiveTasksAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ResticManagerDbContext>();
        return await context.AsyncTasks
            .Where(t => t.Status == StatusPending || t.Status == StatusRunning)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AsyncTask>> GetRecentlyCompletedAsync(int withinMinutes = 2)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ResticManagerDbContext>();
        var threshold = DateTime.UtcNow.AddMinutes(-withinMinutes);
        return await context.AsyncTasks
            .Where(t => t.EndTime != null && t.EndTime >= threshold)
            .OrderByDescending(t => t.EndTime)
            .Take(20)
            .ToListAsync();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return CleanUpOnStartupAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 启动清理：把上次进程退出遗留的 Pending/Running 任务标记为失败，复位备份任务侧
    /// 的 Running 状态位（顺带修复旧版本崩溃后 LastRunStatus 永久卡 Running、任务无法
    /// 再次运行的缺陷），并删除临时类任务的过期历史防止任务表无限膨胀。
    /// </summary>
    private async Task CleanUpOnStartupAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ResticManagerDbContext>();

            // 进程没了 restic 子进程也随之中断，遗留的在途任务统一标失败
            var stale = await context.AsyncTasks
                .Where(t => t.Status == StatusPending || t.Status == StatusRunning)
                .ToListAsync(ct);
            foreach (var t in stale)
            {
                t.Status = StatusFailed;
                t.Error = "应用重启导致任务中断";
                t.EndTime = DateTime.UtcNow;
            }
            if (stale.Count > 0)
                await context.SaveChangesAsync(ct);

            // 复位备份任务的状态位
            var runningTasks = await context.BackupTasks
                .Where(t => t.LastRunStatus == "Running")
                .ToListAsync(ct);
            foreach (var t in runningTasks)
                t.LastRunStatus = StatusFailed;

            var runningRecords = await context.BackupRecords
                .Where(r => r.Status == StatusRunning)
                .ToListAsync(ct);
            foreach (var r in runningRecords)
            {
                r.Status = StatusFailed;
                r.EndTime = DateTime.Now;
                r.Error = "应用重启中断";
            }
            if (runningTasks.Count > 0 || runningRecords.Count > 0)
                await context.SaveChangesAsync(ct);

            // 临时类任务（文件树/搜索/diff）结果只被当次页面消费，清理 1 天前的已完成记录
            var cutoff = DateTime.UtcNow.AddDays(-1);
            await context.AsyncTasks
                .Where(t => AsyncTaskTypes.EphemeralTypes.Contains(t.Type)
                            && (t.Status == StatusSucceeded || t.Status == StatusFailed)
                            && t.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            // 被中断的任务意味着 restic 子进程是被强杀的：锁文件会留在仓库里（仓库是挂载卷，
            // 重启不丢），挡住后续独占操作（删除/prune/修复索引）——这是"重启后一直删不掉快照"
            // 的根因。这里对涉及的仓库补一次 unlock（restic 默认只清陈旧锁，不动活跃进程的锁）。
            // 网盘仓库下 unlock 可能较慢，放后台执行，不阻塞应用启动。
            var interruptedRepoIds = stale
                .Where(t => t.RepositoryId.HasValue)
                .Select(t => t.RepositoryId!.Value)
                .Distinct()
                .ToList();
            if (interruptedRepoIds.Count > 0)
                _ = Task.Run(() => UnlockRepositoriesAsync(interruptedRepoIds));

            if (stale.Count > 0)
                _logger.LogWarning("启动清理：{Count} 个上次遗留的后台任务已标记为失败", stale.Count);
        }
        catch (Exception ex)
        {
            // 清理失败不阻塞应用启动
            _logger.LogError(ex, "启动清理后台任务残留状态失败");
        }
    }

    /// <summary>
    /// 启动清理的收尾：对中断任务涉及的仓库执行 restic unlock，清掉被强杀的 restic 进程残留的锁。
    /// 只清陈旧锁（restic 默认行为），不影响活跃进程持有的锁。best-effort，失败只记日志。
    /// </summary>
    private async Task UnlockRepositoriesAsync(List<int> repositoryIds)
    {
        foreach (var repositoryId in repositoryIds)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repositoryService = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
                var output = await repositoryService.UnlockRepositoryAsync(repositoryId);
                _logger.LogInformation("启动清理：仓库 {RepositoryId} 自动解锁完成：{Output}", repositoryId, output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "启动清理：仓库 {RepositoryId} 自动解锁失败（不影响启动）", repositoryId);
            }
        }
    }

    /// <summary>
    /// 后台执行单个任务：排队（全局闸）→ 置 Running → 取仓库锁 → 执行 → 落结果。
    /// 任何异常都只落到任务行上，绝不外抛。
    /// </summary>
    private async Task RunAsync(int taskId)
    {
        try
        {
            AsyncTask? task;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ResticManagerDbContext>();
                task = await context.AsyncTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            }
            if (task == null)
                return;

            // 先取全局并发闸再翻转状态：排队中的任务保持 Pending，前端可区分"排队中/执行中"
            await GlobalGate.WaitAsync();
            try
            {
                await SetStatusRunningAsync(taskId);

                var repoLock = GetRepoLock(task.Type, task.RepositoryId);
                if (repoLock != null)
                    await repoLock.WaitAsync();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    object? result;
                    try
                    {
                        result = await ExecuteAsync(task);
                    }
                    finally
                    {
                        stopwatch.Stop();
                    }

                    await MarkFinishedAsync(taskId, StatusSucceeded, null,
                        result == null ? null : JsonSerializer.Serialize(result, JsonOptions),
                        stopwatch.ElapsedMilliseconds);
                }
                finally
                {
                    repoLock?.Release();
                }
            }
            finally
            {
                GlobalGate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "后台任务 {TaskId} 执行失败", taskId);
            try
            {
                await MarkFinishedAsync(taskId, StatusFailed, ex.Message, null, null);
            }
            catch (Exception markEx)
            {
                _logger.LogError(markEx, "后台任务 {TaskId} 失败状态落库失败", taskId);
            }
        }
    }

    /// <summary>
    /// 解锁仓库是恢复手段，不能被同仓库其它任务阻塞，因此不取仓库锁。
    /// 无仓库关联的任务也无需仓库锁。
    /// </summary>
    private static SemaphoreSlim? GetRepoLock(string type, int? repositoryId)
    {
        if (type == AsyncTaskTypes.RepositoryUnlock || repositoryId == null)
            return null;
        return RepoLocks.GetOrAdd(repositoryId.Value, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// 在独立 DI scope 中按任务类型执行对应操作，返回要序列化为 ResultJson 的结果对象。
    /// forget/tag/repair 等操作之后对本地快照缓存的善后逻辑也在这里完成（与原 Controller 内
    /// 的后置处理一致：本地缓存失败只记日志，不影响主流程结果）。
    /// </summary>
    private async Task<object?> ExecuteAsync(AsyncTask task)
    {
        using var scope = _scopeFactory.CreateScope();
        var repositoryService = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
        var snapshotService = scope.ServiceProvider.GetRequiredService<ISnapshotService>();
        var repositoryId = task.RepositoryId!.Value;

        switch (task.Type)
        {
            case AsyncTaskTypes.RepositoryInitialize:
                await repositoryService.InitializeRepositoryAsync(repositoryId);
                return null;

            case AsyncTaskTypes.RepositoryCheck:
            {
                // 服务内部自建 RepositoryCheckRecord 并入库，行为与手动校验保持不变
                var healthCheckService = scope.ServiceProvider.GetRequiredService<IRepositoryHealthCheckService>();
                var record = await healthCheckService.CheckRepositoryAsync(repositoryId, "Manual");
                return new { status = record.Status, durationMs = record.DurationMs, errorMessage = record.ErrorMessage, checkedAt = record.CheckedAt };
            }

            case AsyncTaskTypes.RepositoryStats:
                return await repositoryService.GetRepositoryStatsAsync(repositoryId);

            case AsyncTaskTypes.SnapshotSync:
                return await snapshotService.SyncRepositoryAsync(repositoryId);

            case AsyncTaskTypes.SnapshotFileTree:
            {
                var p = Deserialize<FileTreeParams>(task.ParametersJson);
                return await repositoryService.ListSubdirectoryFilesInSnapshotTreeAsync(repositoryId, p.SnapshotId, p.Path ?? "");
            }

            case AsyncTaskTypes.SnapshotDiff:
            {
                var p = Deserialize<DiffParams>(task.ParametersJson);
                return await repositoryService.DiffSnapshotsAsync(repositoryId, p.SnapshotId1, p.SnapshotId2);
            }

            case AsyncTaskTypes.SnapshotRestore:
            {
                var p = Deserialize<RestoreParams>(task.ParametersJson);
                await repositoryService.RestoreSnapshotAsync(repositoryId, p.SnapshotId, p.TargetPath, p.Verify);
                return null;
            }

            case AsyncTaskTypes.FileRestore:
            {
                var p = Deserialize<FileRestoreParams>(task.ParametersJson);
                await repositoryService.RestoreFileAsync(repositoryId, p.SnapshotId, p.FilePath, p.TargetPath, p.Verify);
                return null;
            }

            case AsyncTaskTypes.SnapshotDelete:
            {
                var p = Deserialize<SnapshotIdParams>(task.ParametersJson);
                await repositoryService.DeleteSnapshotAsync(repositoryId, p.SnapshotId);
                // forget 成功后把本地对应快照标记为 IsDeleted；本地标记失败只记日志，
                // 并通过 resultJson 告知前端（否则任务显示成功但列表不刷新，用户无从判断）
                string? warning = null;
                try
                {
                    await snapshotService.MarkDeletedAsync(repositoryId, p.SnapshotId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除快照后更新本地缓存失败");
                    warning = "更新本地缓存失败，请手动执行\"同步远端\"刷新列表";
                }
                return warning == null ? null : new { localCacheWarning = warning };
            }

            case AsyncTaskTypes.SnapshotBatchDelete:
            {
                var p = Deserialize<BatchDeleteParams>(task.ParametersJson);
                var ids = (p.SnapshotIds ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
                await repositoryService.DeleteSnapshotsAsync(repositoryId, ids);
                var marked = 0;
                string? warning = null;
                try
                {
                    marked = await snapshotService.MarkDeletedBatchAsync(repositoryId, ids);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "批量删除快照后更新本地缓存失败");
                    warning = "更新本地缓存失败，请手动执行\"同步远端\"刷新列表";
                }
                return new { deleted = ids.Count, markedDeleted = marked, localCacheWarning = warning };
            }

            case AsyncTaskTypes.RepositoryUnlock:
            {
                var p = Deserialize<UnlockParams>(task.ParametersJson);
                var output = await repositoryService.UnlockRepositoryAsync(repositoryId, p.RemoveAll);
                return new { output };
            }

            case AsyncTaskTypes.RepositoryPrune:
                return new { output = await repositoryService.PruneRepositoryAsync(repositoryId) };

            case AsyncTaskTypes.RepositoryRepairIndex:
                return new { output = await repositoryService.RepairIndexAsync(repositoryId) };

            case AsyncTaskTypes.RepositoryRepairSnapshots:
            {
                var output = await repositoryService.RepairSnapshotsAsync(repositoryId);
                // repair 会删除损坏快照并以新 ID 重建（repair --forget），
                // 同步一次本地缓存，避免快照列表展示已被替换的旧 ID（失败不影响主流程）
                try
                {
                    await snapshotService.SyncRepositoryAsync(repositoryId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "快照修复后同步缓存失败");
                }
                return new { output };
            }

            case AsyncTaskTypes.RepositoryDeepCheck:
            {
                var p = Deserialize<DeepCheckParams>(task.ParametersJson);
                var subset = p.Subset ?? "5%";
                var output = await repositoryService.DeepCheckRepositoryAsync(repositoryId, subset);
                return new { subset, output };
            }

            case AsyncTaskTypes.SnapshotFind:
            {
                var p = Deserialize<FindParams>(task.ParametersJson);
                return await repositoryService.FindInSnapshotsAsync(repositoryId, p.Pattern);
            }

            case AsyncTaskTypes.SnapshotTagSet:
            {
                var p = Deserialize<TagParams>(task.ParametersJson);
                await repositoryService.SetSnapshotTagsAsync(repositoryId, p.SnapshotId, p.Tags ?? new List<string>());
                // restic tag 会以新 ID 重写快照并移除旧 ID，只能全量同步一次让本地缓存
                // 反映新 ID 与新标签（失败不影响主流程）
                try
                {
                    await snapshotService.SyncRepositoryAsync(repositoryId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "标签更新后同步快照缓存失败");
                }
                return null;
            }

            case AsyncTaskTypes.BackupRun:
            {
                var p = Deserialize<BackupRunParams>(task.ParametersJson);
                // RunTaskAsync 含 LastRunStatus 防重入守卫、BackupRecord 生命周期与保留策略，
                // 与 Quartz BackupTaskJob 是同一条执行路径
                var backupTaskService = scope.ServiceProvider.GetRequiredService<IBackupTaskService>();
                var recordId = await backupTaskService.RunTaskAsync(p.BackupTaskId);
                return new { backupRecordId = recordId };
            }

            default:
                throw new InvalidOperationException($"未知的任务类型: {task.Type}");
        }
    }

    private async Task SetStatusRunningAsync(int taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ResticManagerDbContext>();
        var task = await context.AsyncTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
            return;
        task.Status = StatusRunning;
        task.StartTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private async Task MarkFinishedAsync(int taskId, string status, string? error, string? resultJson, long? durationMs)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ResticManagerDbContext>();
        var task = await context.AsyncTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
            return;
        task.Status = status;
        task.Error = error;
        task.ResultJson = resultJson;
        task.EndTime = DateTime.UtcNow;
        task.DurationMs = durationMs;
        await context.SaveChangesAsync();
    }

    private static T Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("任务参数缺失");
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidOperationException("任务参数解析失败");
    }

    // —— 任务参数 DTO（JsonSerializerDefaults.Web：camelCase 序列化、大小写不敏感反序列化）——

    private sealed record SnapshotIdParams(string SnapshotId);
    private sealed record FileTreeParams(string SnapshotId, string? Path);
    private sealed record DiffParams(string SnapshotId1, string SnapshotId2);
    private sealed record RestoreParams(string SnapshotId, string TargetPath, bool Verify);
    private sealed record FileRestoreParams(string SnapshotId, string FilePath, string TargetPath, bool Verify);
    private sealed record BatchDeleteParams(List<string>? SnapshotIds);
    private sealed record UnlockParams(bool RemoveAll);
    private sealed record DeepCheckParams(string? Subset);
    private sealed record FindParams(string Pattern);
    private sealed record TagParams(string SnapshotId, List<string>? Tags);
    private sealed record BackupRunParams(int BackupTaskId);
}
