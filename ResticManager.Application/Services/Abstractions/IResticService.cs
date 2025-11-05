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
    /// <returns>备份结果</returns>
    Task<BackupResult> CreateBackupAsync(string sourcePath, string repoPath, string password, string includePatterns = null, string excludePatterns = null);

    /// <summary>
    /// 列出仓库中的快照
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>快照信息列表</returns>
    Task<List<SnapshotInfo>> ListSnapshotsAsync(string repoPath, string password);

    /// <summary>
    /// 列出快照中的文件
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <returns>文件信息列表</returns>
    Task<List<ResticFileInfo>> ListFilesInSnapshotAsync(string repoPath, string password, string snapshotId, string dirPath);

    /// <summary>
    /// 恢复快照
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="targetPath">目标路径</param>
    /// <returns>无返回值</returns>
    Task RestoreSnapshotAsync(string repoPath, string password, string snapshotId, string targetPath);

    /// <summary>
    /// 恢复文件
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="restorePath">恢复路径</param>
    /// <returns>无返回值</returns>
    Task RestoreFileAsync(string repoPath, string password, string snapshotId, string filePath, string restorePath);

    /// <summary>
    /// 删除快照
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="snapshotId">快照ID</param>
    /// <returns>无返回值</returns>
    Task DeleteSnapshotAsync(string repoPath, string password, string snapshotId);

    /// <summary>
    /// 清理快照
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <param name="keepDaily">保留每日快照数</param>
    /// <param name="keepWeekly">保留每周快照数</param>
    /// <param name="keepMonthly">保留每月快照数</param>
    /// <returns>无返回值</returns>
    Task PruneSnapshotsAsync(string repoPath, string password, int keepDaily, int keepWeekly, int keepMonthly);

    /// <summary>
    /// 检查仓库
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>检查结果</returns>
    Task<string> CheckRepositoryAsync(string repoPath, string password);

    /// <summary>
    /// 获取仓库统计信息
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>统计信息</returns>
    Task<RepositoryStats> GetStatsAsync(string repoPath, string password);

    /// <summary>
    /// 检查仓库是否已初始化
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="password">仓库密码</param>
    /// <returns>是否已初始化</returns>
    Task<bool> IsRepositoryInitializedAsync(string repoPath, string password);
}
