using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 仓库服务接口
/// </summary>
public interface IRepositoryService
{
    /// <summary>
    /// 获取所有仓库
    /// </summary>
    /// <returns>仓库列表</returns>
    Task<List<Repository>> GetAllRepositoriesAsync();

    /// <summary>
    /// 根据ID获取仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>仓库对象</returns>
    Task<Repository> GetRepositoryByIdAsync(int id);

    /// <summary>
    /// 创建仓库
    /// </summary>
    /// <param name="repository">仓库对象</param>
    /// <returns>创建的仓库</returns>
    Task<Repository> CreateRepositoryAsync(Repository repository);

    /// <summary>
    /// 更新仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="repository">更新后的仓库对象</param>
    /// <returns>更新后的仓库</returns>
    Task<Repository> UpdateRepositoryAsync(int id, Repository repository);

    /// <summary>
    /// 删除仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteRepositoryAsync(int id);

    /// <summary>
    /// 初始化仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>无返回值</returns>
    Task InitializeRepositoryAsync(int id);

    /// <summary>
    /// 验证仓库是否有效
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>验证结果</returns>
    Task<bool> ValidateRepositoryAsync(int id);

    /// <summary>
    /// 获取仓库统计信息
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>统计信息</returns>
    Task<RepositoryStats> GetRepositoryStatsAsync(int id);

    /// <summary>
    /// 列出仓库中的快照
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>快照信息列表</returns>
    Task<List<SnapshotInfo>> ListSnapshotsAsync(int id);

    ///// <summary>
    ///// 列出快照中的文件（树形结构）
    ///// </summary>
    ///// <param name="id">仓库ID</param>
    ///// <param name="snapshotId">快照ID</param>
    ///// <returns>文件信息树形结构列表</returns>
    //Task<List<ResticFileTreeNode>> ListFilesInSnapshotTreeAsync(int id, string snapshotId, string dirPath);

    /// <summary>
    /// 列出快照中指定子目录的文件（树形结构）
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="path">子目录路径</param>
    /// <returns>文件信息树形结构列表</returns>
    Task<List<ResticFileTreeNode>> ListSubdirectoryFilesInSnapshotTreeAsync(int id, string snapshotId, string path);

    /// <summary>
    /// 比较快照差异
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="snapshotId1">第一个快照ID</param>
    /// <param name="snapshotId2">第二个快照ID</param>
    /// <returns>快照差异结果</returns>
    Task<SnapshotDiffResult> DiffSnapshotsAsync(int id, string snapshotId1, string snapshotId2);
    
    /// <summary>
    /// 恢复快照
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="targetPath">目标路径</param>
    /// <returns>无返回值</returns>
    Task RestoreSnapshotAsync(int id, string snapshotId, string targetPath);

    /// <summary>
    /// 恢复文件
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="targetPath">目标路径</param>
    /// <returns>无返回值</returns>
    Task RestoreFileAsync(int id, string snapshotId, string filePath, string targetPath);

    /// <summary>
    /// 删除快照
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="snapshotId">快照ID</param>
    /// <returns>无返回值</returns>
    Task DeleteSnapshotAsync(int id, string snapshotId);
}