using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// Restic工具服务接口，提供与restic命令行工具交互的功能
/// </summary>
public interface IResticService
{
    /// <summary>
    /// 初始化仓库
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>无返回值</returns>
    Task InitializeRepositoryAsync(string repoPath, string password);

    /// <summary>
    /// 创建备份
    /// </summary>
    /// <param name="sourcePath">源路径</param>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="includePatterns">包含模式</param>
    /// <param name="excludePatterns">排除模式</param>
    /// <param name="options">备份高级选项（标签、排除缓存、试运行等），可为 null</param>
    /// <returns>备份结果</returns>
    Task<BackupResult> CreateBackupAsync(string sourcePath, string repoPath, string password, string includePatterns = null, string excludePatterns = null, BackupOptions options = null);

    /// <summary>
    /// 列出仓库中的快照
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>快照信息列表</returns>
    Task<List<SnapshotInfo>> ListSnapshotsAsync(string repoPath, string password);

    /// <summary>
    /// 列出仓库中指定 ID 的快照（restic snapshots --json &lt;id1&gt; &lt;id2&gt; …）。
    /// 用于备份后仅获取单条快照的元数据写入本地缓存，避免全量拉取。
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="ids">快照 ID 列表，为空时退化为全量列表</param>
    /// <returns>快照信息列表</returns>
    Task<List<SnapshotInfo>> ListSnapshotsByIdsAsync(string repoPath, string password, IEnumerable<string> ids);

    /// <summary>
    /// 列出快照中的文件
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <returns>文件信息列表</returns>
    Task<List<ResticFileInfo>> ListFilesInSnapshotAsync(string repoPath, string password, string snapshotId, string dirPath);

    /// <summary>
    /// 比较快照之间的差异
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId1">第一个快照ID</param>
    /// <param name="snapshotId2">第二个快照ID</param>
    /// <returns>差异信息</returns>
    Task<SnapshotDiffResult> DiffSnapshotsAsync(string repoPath, string password, string snapshotId1, string snapshotId2);

    /// <summary>
    /// 恢复快照
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="targetPath">目标路径</param>
    /// <param name="verify">恢复后校验文件内容，对应 --verify</param>
    /// <returns>无返回值</returns>
    Task RestoreSnapshotAsync(string repoPath, string password, string snapshotId, string targetPath, bool verify = false);

    /// <summary>
    /// 恢复文件
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="restorePath">恢复路径</param>
    /// <param name="verify">恢复后校验文件内容，对应 --verify</param>
    /// <returns>无返回值</returns>
    Task RestoreFileAsync(string repoPath, string password, string snapshotId, string filePath, string restorePath, bool verify = false);

    /// <summary>
    /// 删除快照
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <returns>无返回值</returns>
    Task DeleteSnapshotAsync(string repoPath, string password, string snapshotId);

    /// <summary>
    /// 批量删除快照（一次 restic forget 传入多个 ID）
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotIds">快照ID集合</param>
    /// <returns>无返回值</returns>
    Task DeleteSnapshotsAsync(string repoPath, string password, IEnumerable<string> snapshotIds);

    /// <summary>
    /// 清理快照
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="keepDaily">保留每日快照数</param>
    /// <param name="keepWeekly">保留每周快照数</param>
    /// <param name="keepMonthly">保留每月快照数</param>
    /// <param name="keepYearly">保留每年快照数，传 null 时不启用年度保留</param>
    /// <returns>无返回值</returns>
    Task PruneSnapshotsAsync(string repoPath, string password, int keepDaily, int keepWeekly, int keepMonthly, int? keepYearly);

    /// <summary>
    /// 检查仓库
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="readDataSubset">深度校验时读取的数据子集，如 "5%"、"1G"；为 null 时仅校验结构，对应 --read-data-subset</param>
    /// <returns>检查结果</returns>
    Task<string> CheckRepositoryAsync(string repoPath, string password, string readDataSubset = null);

    /// <summary>
    /// 获取仓库统计信息
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>仓库统计信息</returns>
    Task<RepositoryStats> GetStatsAsync(string repoPath, string password);

    /// <summary>
    /// 检查仓库是否已初始化
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>是否已初始化</returns>
    Task<bool> IsRepositoryInitializedAsync(string repoPath, string password);

    /// <summary>
    /// 解锁仓库，清理陈旧的锁（restic unlock）
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="removeAll">是否移除所有锁（包括其他进程持有的），对应 --remove-all</param>
    /// <returns>命令输出</returns>
    Task<string> UnlockRepositoryAsync(string repoPath, string password, bool removeAll = false);

    /// <summary>
    /// 清理仓库中未被引用的数据（restic prune）
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>命令输出</returns>
    Task<string> PruneRepositoryAsync(string repoPath, string password);

    /// <summary>
    /// 修复仓库索引（restic repair index）
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>命令输出</returns>
    Task<string> RepairIndexAsync(string repoPath, string password);

    /// <summary>
    /// 修复损坏的快照，移除无法读取的数据（restic repair snapshots --forget）
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>命令输出</returns>
    Task<string> RepairSnapshotsAsync(string repoPath, string password);

    /// <summary>
    /// 将快照中的单个文件流式输出到指定流（restic dump），用于直接下载而无需整体恢复
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="filePath">快照内文件路径</param>
    /// <param name="output">写入目标流</param>
    /// <returns>无返回值</returns>
    Task DumpFileAsync(string repoPath, string password, string snapshotId, string filePath, Stream output);

    /// <summary>
    /// 跨快照按模式搜索文件（restic find --json）
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="pattern">搜索模式，支持通配符，如 *.docx</param>
    /// <returns>各快照的命中结果</returns>
    Task<List<FindResult>> FindInSnapshotsAsync(string repoPath, string password, string pattern);

    /// <summary>
    /// 设置（覆盖）快照的标签（restic tag --set）
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="tags">新的标签集合，传空集合表示清除全部标签</param>
    /// <returns>无返回值</returns>
    Task SetSnapshotTagsAsync(string repoPath, string password, string snapshotId, List<string> tags);
}