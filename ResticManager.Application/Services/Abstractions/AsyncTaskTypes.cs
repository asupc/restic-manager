namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 后台异步任务类型常量（对应 AsyncTask 实体的 Type 字段）。
/// 每种类型对应 AsyncTaskService 执行器中的一个分支。
/// </summary>
public static class AsyncTaskTypes
{
    public const string RepositoryInitialize = "RepositoryInitialize";
    public const string RepositoryCheck = "RepositoryCheck";
    public const string RepositoryStats = "RepositoryStats";
    public const string SnapshotSync = "SnapshotSync";
    public const string SnapshotFileTree = "SnapshotFileTree";
    public const string SnapshotDiff = "SnapshotDiff";
    public const string SnapshotRestore = "SnapshotRestore";
    public const string FileRestore = "FileRestore";
    public const string SnapshotDelete = "SnapshotDelete";
    public const string SnapshotBatchDelete = "SnapshotBatchDelete";
    public const string RepositoryUnlock = "RepositoryUnlock";
    public const string RepositoryPrune = "RepositoryPrune";
    public const string RepositoryRepairIndex = "RepositoryRepairIndex";
    public const string RepositoryRepairSnapshots = "RepositoryRepairSnapshots";
    public const string RepositoryDeepCheck = "RepositoryDeepCheck";
    public const string SnapshotFind = "SnapshotFind";
    public const string SnapshotTagSet = "SnapshotTagSet";
    public const string BackupRun = "BackupRun";

    /// <summary>
    /// 结果只被当次页面消费、历史无价值的任务类型。
    /// 启动清理时会删除这些类型中已完成超过 1 天的记录，防止任务表无限膨胀。
    /// </summary>
    public static readonly string[] EphemeralTypes =
    {
        SnapshotFileTree,
        SnapshotFind,
        SnapshotDiff
    };
}
