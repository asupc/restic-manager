using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 快照本地持久化服务接口。把 restic 远端仓库的快照元数据缓存到本地 DB，
/// 让"快照列表 / 版本记录"页面从本地读，避开网盘远端调用。
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// 从本地 DB 读取某个仓库的快照列表（不走远端）。
    /// </summary>
    /// <param name="repositoryId">仓库 ID</param>
    /// <param name="includeDeleted">是否包含远端已 forget 但本地尚未清理的快照</param>
    /// <returns>快照列表（按时间倒序）</returns>
    Task<List<BackupSnapshot>> GetByRepositoryAsync(int repositoryId, bool includeDeleted = false);

    /// <summary>
    /// 将一条 SnapshotInfo 写入或更新本地 DB（不调远端）。
    /// </summary>
    /// <param name="repositoryId">仓库 ID</param>
    /// <param name="info">远端快照信息</param>
    /// <param name="backupRecordId">关联的本地执行记录 ID（可空）</param>
    /// <returns>写入或更新后的本地记录</returns>
    Task<BackupSnapshot> UpsertFromSnapshotInfoAsync(int repositoryId, SnapshotInfo info, int? backupRecordId = null);

    /// <summary>
    /// 全量同步：从远端拉 snapshots --json，逐条 upsert，远端不存在但本地
    /// 存在且未删除的快照标记为 IsDeleted。常用于 forget/prune 后或手动触发。
    /// </summary>
    /// <param name="repositoryId">仓库 ID</param>
    /// <returns>同步结果统计</returns>
    Task<SyncSnapshotsResult> SyncRepositoryAsync(int repositoryId);

    /// <summary>
    /// 备份成功后调远端拉单条快照元数据写入本地 DB。
    /// 远端失败不应抛出——让上层决定如何处理。
    /// </summary>
    /// <param name="repositoryId">仓库 ID</param>
    /// <param name="snapshotId">新快照 ID</param>
    /// <param name="backupRecordId">关联的本地执行记录 ID</param>
    /// <returns>写入后的本地记录，失败返回 null</returns>
    Task<BackupSnapshot> IngestNewSnapshotAsync(int repositoryId, string snapshotId, int? backupRecordId);

    /// <summary>
    /// forget 后把本地对应快照标记为 IsDeleted。
    /// </summary>
    /// <param name="repositoryId">仓库 ID</param>
    /// <param name="snapshotId">快照 ID</param>
    Task MarkDeletedAsync(int repositoryId, string snapshotId);

    /// <summary>
    /// 批量 forget 后把本地对应快照批量标记为 IsDeleted。
    /// </summary>
    /// <param name="repositoryId">仓库 ID</param>
    /// <param name="snapshotIds">快照 ID 集合</param>
    /// <returns>实际标记的条数</returns>
    Task<int> MarkDeletedBatchAsync(int repositoryId, IEnumerable<string> snapshotIds);
}