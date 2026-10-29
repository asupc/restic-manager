using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Application.Services.Implements;

/// <summary>
/// 仓库健康检查服务：对所有已初始化仓库执行 restic check 并把结果入库。
/// </summary>
public class RepositoryHealthCheckService : IRepositoryHealthCheckService
{
    private readonly ResticManagerDbContext _context;
    private readonly IResticService _resticService;
    private readonly IRepositoryLockManager _repositoryLockManager;
    private readonly ILogger<RepositoryHealthCheckService> _logger;

    public RepositoryHealthCheckService(
        ResticManagerDbContext context,
        IResticService resticService,
        IRepositoryLockManager repositoryLockManager,
        ILogger<RepositoryHealthCheckService> logger)
    {
        _context = context;
        _resticService = resticService;
        _repositoryLockManager = repositoryLockManager;
        _logger = logger;
    }

    public async Task<RepositoryCheckRecord> CheckRepositoryAsync(int repositoryId, string trigger = "Manual")
    {
        var repository = await _context.Repositories.FindAsync(repositoryId);
        if (repository == null)
            throw new Exception($"仓库 ID {repositoryId} 不存在");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        var record = new RepositoryCheckRecord
        {
            RepositoryId = repositoryId,
            CheckedAt = DateTime.UtcNow,
            Trigger = trigger,
            Status = "Failed"
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // 每仓库互斥：restic check 创建排它锁，与定时备份（默认 Cron 同为 03:00）、
            // AsyncTaskService 后台任务共用仓库锁排队执行，避免双方竞争后端锁必有一败
            await using var repoLock = await _repositoryLockManager.AcquireAsync(repositoryId);
            var output = await _resticService.CheckRepositoryAsync(repository);
            stopwatch.Stop();

            record.Status = "Passed";
            record.DurationMs = stopwatch.ElapsedMilliseconds;
            record.Output = output;
            record.ErrorMessage = null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            record.Status = "Failed";
            record.DurationMs = stopwatch.ElapsedMilliseconds;
            record.ErrorMessage = ex.Message;
            _logger.LogWarning("仓库 {RepositoryId} 健康检查失败: {Error}", repositoryId, ex.Message);
        }

        _context.RepositoryCheckRecords.Add(record);
        await _context.SaveChangesAsync();
        return record;
    }

    public async Task<List<RepositoryCheckRecord>> CheckAllRepositoriesAsync(string trigger = "Scheduled")
    {
        var repositoryIds = await _context.Repositories
            .Where(r => r.IsInitialized)
            .Select(r => r.Id)
            .ToListAsync();

        var records = new List<RepositoryCheckRecord>();
        foreach (var id in repositoryIds)
        {
            try
            {
                var record = await CheckRepositoryAsync(id, trigger);
                records.Add(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行仓库 {RepositoryId} 健康检查时发生未预期异常", id);
            }
        }

        return records;
    }

    public async Task<List<RepositoryCheckRecord>> GetRecordsByRepositoryAsync(int repositoryId, int limit = 20)
    {
        return await _context.RepositoryCheckRecords
            .Where(r => r.RepositoryId == repositoryId)
            .OrderByDescending(r => r.CheckedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<RepositoryCheckRecord?> GetLatestByRepositoryAsync(int repositoryId)
    {
        return await _context.RepositoryCheckRecords
            .Where(r => r.RepositoryId == repositoryId)
            .OrderByDescending(r => r.CheckedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<RepositoryHealthOverview> GetOverviewAsync()
    {
        var overview = new RepositoryHealthOverview();

        var repositories = await _context.Repositories
            .Select(r => new { r.Id, r.Name, r.IsInitialized })
            .ToListAsync();

        overview.TotalRepositories = repositories.Count;
        overview.InitializedRepositories = repositories.Count(r => r.IsInitialized);

        if (overview.InitializedRepositories == 0)
        {
            return overview;
        }

        // 取每个已初始化仓库的最新一条检查记录
        var initializedIds = repositories.Where(r => r.IsInitialized).Select(r => r.Id).ToList();
        var latestByRepo = await _context.RepositoryCheckRecords
            .Where(r => initializedIds.Contains(r.RepositoryId))
            .GroupBy(r => r.RepositoryId)
            .Select(g => new
            {
                RepositoryId = g.Key,
                Latest = g.OrderByDescending(x => x.CheckedAt).First()
            })
            .ToListAsync();

        var latestDict = latestByRepo.ToDictionary(x => x.RepositoryId, x => x.Latest);

        var passed = 0;
        var failed = 0;
        var never = 0;
        var attention = new List<RepositoryHealthSummary>();

        foreach (var repo in repositories.Where(r => r.IsInitialized))
        {
            if (!latestDict.TryGetValue(repo.Id, out var latest))
            {
                never++;
                attention.Add(new RepositoryHealthSummary
                {
                    RepositoryId = repo.Id,
                    RepositoryName = repo.Name,
                    Status = "NeverChecked"
                });
                continue;
            }

            if (latest.Status == "Passed") passed++;
            else
            {
                failed++;
                var hours = (DateTime.UtcNow - latest.CheckedAt).TotalHours;
                attention.Add(new RepositoryHealthSummary
                {
                    RepositoryId = repo.Id,
                    RepositoryName = repo.Name,
                    Status = "Failed",
                    LastCheckedAt = latest.CheckedAt,
                    LastError = latest.ErrorMessage,
                    HoursSinceLastCheck = Math.Round(hours, 1)
                });
            }
        }

        overview.PassedRepositories = passed;
        overview.FailedRepositories = failed;
        overview.NeverCheckedRepositories = never;

        // 关注项按失败优先，再按从未检查，最后按最后检查时间升序，最多 10 条
        overview.AttentionItems = attention
            .OrderByDescending(a => a.Status == "Failed")
            .ThenByDescending(a => a.Status == "NeverChecked")
            .ThenBy(a => a.LastCheckedAt ?? DateTime.MaxValue)
            .Take(10)
            .ToList();

        return overview;
    }
}