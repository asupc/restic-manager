using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Application.Services.Implements;

/// <summary>
/// 快照本地持久化服务实现。所有"快照列表 / 版本记录"读取优先走本地 DB，
/// 仅在首次缓存、备份后增量同步、forget/prune 后或手动触发时才调远端。
/// </summary>
public class SnapshotService : ISnapshotService
{
    private readonly ResticManagerDbContext _context;
    private readonly IResticService _resticService;

    public SnapshotService(ResticManagerDbContext context, IResticService resticService)
    {
        _context = context;
        _resticService = resticService;
    }

    public async Task<List<BackupSnapshot>> GetByRepositoryAsync(int repositoryId, bool includeDeleted = false)
    {
        var query = _context.BackupSnapshots
            .Where(s => s.RepositoryId == repositoryId);

        if (!includeDeleted)
        {
            query = query.Where(s => !s.IsDeleted);
        }

        return await query
            .OrderByDescending(s => s.Time)
            .ToListAsync();
    }

    public async Task<BackupSnapshot> UpsertFromSnapshotInfoAsync(int repositoryId, SnapshotInfo info, int? backupRecordId = null)
    {
        if (info == null || string.IsNullOrEmpty(info.Id))
            throw new ArgumentException("快照信息或ID为空", nameof(info));

        var existing = await _context.BackupSnapshots
            .FirstOrDefaultAsync(s => s.RepositoryId == repositoryId && s.SnapshotId == info.Id);

        var now = DateTime.Now;

        if (existing == null)
        {
            var entity = CreateSnapshotEntity(repositoryId, info, backupRecordId, now);
            _context.BackupSnapshots.Add(entity);
            try
            {
                await _context.SaveChangesAsync();
                return entity;
            }
            catch (DbUpdateException)
            {
                // 并发插入同一 (RepositoryId, SnapshotId) 撞唯一索引：
                // 放弃本实例改走更新路径，避免把失败的 Added 实体留在共享
                // DbContext 的变更跟踪里毒化下一次 SaveChanges。
                _context.Entry(entity).State = EntityState.Detached;
                existing = await _context.BackupSnapshots
                    .FirstOrDefaultAsync(s => s.RepositoryId == repositoryId && s.SnapshotId == info.Id);
                if (existing == null)
                    throw;
            }
        }

        ApplySnapshotInfo(existing, info, backupRecordId, now);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<SyncSnapshotsResult> SyncRepositoryAsync(int repositoryId)
    {
        var repository = await _context.Repositories.FindAsync(repositoryId);
        if (repository == null)
            throw new Exception("仓库未找到");
        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        var syncStartedAt = DateTime.Now;

        // 1. 从远端拉全量
        var remoteList = await _resticService.ListSnapshotsAsync(repository.Path, repository.Password);
        var remoteIds = remoteList.Select(s => s.Id).ToHashSet();

        // 2. 一次性加载本地全部行（含已删除），在内存中 diff 后单次提交，
        //    避免逐行查询 + 逐行 SaveChanges 的 N+1 往返。
        var localList = await _context.BackupSnapshots
            .Where(s => s.RepositoryId == repositoryId)
            .ToListAsync();
        var localById = localList.ToDictionary(s => s.SnapshotId, s => s, StringComparer.Ordinal);

        var result = new SyncSnapshotsResult { RemoteTotal = remoteList.Count };

        // 3. 内存 upsert：已存在的行仅在字段真正变化时才标脏并计数
        foreach (var remote in remoteList)
        {
            if (localById.TryGetValue(remote.Id, out var existing))
            {
                if (ApplySnapshotInfo(existing, remote, null, syncStartedAt))
                {
                    result.Updated++;
                }
            }
            else
            {
                var entity = CreateSnapshotEntity(repositoryId, remote, null, syncStartedAt);
                _context.BackupSnapshots.Add(entity);
                localById[remote.Id] = entity;
                result.Added++;
            }
        }

        // 4. 本地有但远端没有 → 标记 IsDeleted。
        //    同步开始之后才被并发写入（如备份刚 ingest 的新快照）的行跳过，
        //    留待下次同步确认，避免把远端列表拍不到的新快照误标为已删除。
        foreach (var snap in localList)
        {
            if (snap.IsDeleted || remoteIds.Contains(snap.SnapshotId))
                continue;
            if (snap.LastSyncedAt.HasValue && snap.LastSyncedAt.Value > syncStartedAt)
                continue;

            snap.IsDeleted = true;
            snap.LastSyncedAt = syncStartedAt;
            result.MarkedDeleted++;
        }

        await _context.SaveChangesAsync();
        return result;
    }

    public async Task<BackupSnapshot> IngestNewSnapshotAsync(int repositoryId, string snapshotId, int? backupRecordId)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
            return null;

        var repository = await _context.Repositories.FindAsync(repositoryId);
        if (repository == null || !repository.IsInitialized)
            return null;

        try
        {
            var snapshots = await _resticService.ListSnapshotsByIdsAsync(
                repository.Path,
                repository.Password,
                new[] { snapshotId });

            var info = snapshots.FirstOrDefault();
            if (info == null)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] IngestNewSnapshotAsync: 远端未找到快照 {snapshotId}");
                return null;
            }

            return await UpsertFromSnapshotInfoAsync(repositoryId, info, backupRecordId);
        }
        catch (Exception ex)
        {
            // 远端调用失败仅记录，不抛出
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] IngestNewSnapshotAsync 失败（不影响备份成功状态）: {ex.Message}");
            return null;
        }
    }

    public async Task MarkDeletedAsync(int repositoryId, string snapshotId)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
            return;

        var candidates = await _context.BackupSnapshots
            .Where(s => s.RepositoryId == repositoryId)
            .ToListAsync();
        var snap = MatchSnapshot(candidates, snapshotId);
        if (snap == null)
            return;

        snap.IsDeleted = true;
        snap.LastSyncedAt = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task<int> MarkDeletedBatchAsync(int repositoryId, IEnumerable<string> snapshotIds)
    {
        if (snapshotIds == null)
            return 0;

        var idList = snapshotIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (idList.Count == 0)
            return 0;

        // restic 按 ID 前缀解析快照，这里按前缀兼容匹配，一次加载候选后批量标记
        var candidates = await _context.BackupSnapshots
            .Where(s => s.RepositoryId == repositoryId)
            .ToListAsync();
        var now = DateTime.Now;
        var marked = 0;
        foreach (var id in idList)
        {
            var snap = MatchSnapshot(candidates, id);
            if (snap == null || snap.IsDeleted)
                continue;
            snap.IsDeleted = true;
            snap.LastSyncedAt = now;
            marked++;
        }

        if (marked > 0)
            await _context.SaveChangesAsync();
        return marked;
    }

    /// <summary>
    /// 按完整 ID / 双向前缀兼容匹配本地快照缓存（与 restic 按 ID 前缀解析的行为保持一致）。
    /// </summary>
    private static BackupSnapshot MatchSnapshot(IEnumerable<BackupSnapshot> candidates, string snapshotId)
    {
        return candidates.FirstOrDefault(s => s.SnapshotId == snapshotId)
            ?? candidates.FirstOrDefault(s => s.SnapshotId.StartsWith(snapshotId, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(s => snapshotId.StartsWith(s.SnapshotId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 由远端快照信息创建本地实体。
    /// </summary>
    private static BackupSnapshot CreateSnapshotEntity(int repositoryId, SnapshotInfo info, int? backupRecordId, DateTime syncedAt)
    {
        var entity = new BackupSnapshot
        {
            SnapshotId = info.Id,
            RepositoryId = repositoryId,
            BackupRecordId = backupRecordId,
            LastSyncedAt = syncedAt,
            IsDeleted = false
        };
        ApplySnapshotInfo(entity, info, backupRecordId, syncedAt);
        return entity;
    }

    /// <summary>
    /// 把远端快照信息套用到本地实体，仅在字段真正变化时赋值（返回 true），
    /// 避免每次同步把全部行判脏导致全表重写。
    /// </summary>
    private static bool ApplySnapshotInfo(BackupSnapshot target, SnapshotInfo info, int? backupRecordId, DateTime syncedAt)
    {
        var changed = false;

        if (target.ShortId != (info.ShortId ?? target.ShortId))
        {
            target.ShortId = info.ShortId ?? target.ShortId;
            changed = true;
        }
        if (target.Time != info.Time)
        {
            target.Time = info.Time;
            changed = true;
        }
        if (target.Tree != (info.Tree ?? target.Tree))
        {
            target.Tree = info.Tree ?? target.Tree;
            changed = true;
        }
        if (target.Hostname != (info.Hostname ?? target.Hostname))
        {
            target.Hostname = info.Hostname ?? target.Hostname;
            changed = true;
        }
        if (target.Username != (info.Username ?? target.Username))
        {
            target.Username = info.Username ?? target.Username;
            changed = true;
        }
        if (target.Uid != info.Uid)
        {
            target.Uid = info.Uid;
            changed = true;
        }
        if (target.Gid != info.Gid)
        {
            target.Gid = info.Gid;
            changed = true;
        }
        if (target.ProgramVersion != (info.ProgramVersion ?? target.ProgramVersion))
        {
            target.ProgramVersion = info.ProgramVersion ?? target.ProgramVersion;
            changed = true;
        }
        if (target.ParentSnapshotId != (info.Parent ?? target.ParentSnapshotId))
        {
            target.ParentSnapshotId = info.Parent ?? target.ParentSnapshotId;
            changed = true;
        }

        // 直接复用实体的 JSON 计算属性 setter，避免手写序列化
        var pathsJson = JsonSerializer.Serialize(info.Paths ?? new List<string>());
        if (target.PathsJson != pathsJson)
        {
            target.PathsJson = pathsJson;
            changed = true;
        }
        var tagsJson = JsonSerializer.Serialize(info.Tags ?? new List<string>());
        if (target.TagsJson != tagsJson)
        {
            target.TagsJson = tagsJson;
            changed = true;
        }
        var summaryJson = info.Summary == null ? string.Empty : JsonSerializer.Serialize(info.Summary);
        if (target.SummaryJson != summaryJson)
        {
            target.SummaryJson = summaryJson;
            changed = true;
        }

        // 远端存在 → 解除已删除标记（可能由 forget→再 create 造成）
        if (target.IsDeleted)
        {
            target.IsDeleted = false;
            changed = true;
        }

        // 第一次为该快照补全 BackupRecordId 关联（避免覆盖历史映射）
        if (!target.BackupRecordId.HasValue && backupRecordId.HasValue)
        {
            target.BackupRecordId = backupRecordId;
            changed = true;
        }

        // 仅在内容真正变化时刷新同步时间，避免每次全量同步把所有行判脏重写
        if (changed)
        {
            target.LastSyncedAt = syncedAt;
        }

        return changed;
    }
}
