using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Application.Utils;
using ResticManager.Data;
using ResticManager.Models;
using ResticManager.Models.Dtos;
using System.Text.Json;
using System.Text.RegularExpressions;
// 添加 EFCore.BulkExtensions 引用
// using EFCore.BulkExtensions; // 不再需要这个引用

namespace ResticManager.Application.Services.Implements;

public partial class RepositoryService : IRepositoryService
{
    private readonly ResticManagerDbContext _context;
    private readonly IResticService _resticService;
    private readonly ILogger<RepositoryService> _logger;

    public RepositoryService(ResticManagerDbContext context, IResticService resticService, ILogger<RepositoryService> logger)
    {
        _context = context;
        _resticService = resticService;
        _logger = logger;
    }

    public async Task<List<Repository>> GetAllRepositoriesAsync()
    {
        return await _context.Repositories.ToListAsync();
    }

    public async Task<Repository> GetRepositoryByIdAsync(int id)
    {
        return await _context.Repositories.FindAsync(id);
    }

    public async Task<Repository> CreateRepositoryAsync(Repository repository)
    {
        if (repository == null)
            throw new ArgumentNullException(nameof(repository));

        ValidateRepository(repository);

        _context.Repositories.Add(repository);
        await _context.SaveChangesAsync();
        _logger.LogInformation("仓库已创建：{RepositoryId} {RepositoryName}", repository.Id, repository.Name);
        return repository;
    }

    public async Task<Repository> UpdateRepositoryAsync(int id, Repository repository)
    {
        var existingRepo = await _context.Repositories.FindAsync(id);
        if (existingRepo == null) return null;

        ValidateRepository(repository);

        // 路径变了等于换了一个仓库，旧统计不再有效
        var pathChanged = existingRepo.Path != repository.Path;

        existingRepo.Name = repository.Name;
        existingRepo.Path = repository.Path;
        existingRepo.Password = repository.Password;
        existingRepo.Type = repository.Type;
        existingRepo.CredentialsJson = repository.CredentialsJson;
        existingRepo.IsInitialized = repository.IsInitialized;

        if (pathChanged)
        {
            existingRepo.StatsJson = null;
            existingRepo.StatsUpdatedAt = null;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("仓库已更新：{RepositoryId} {RepositoryName}", existingRepo.Id, existingRepo.Name);
        return existingRepo;
    }

    public async Task<bool> DeleteRepositoryAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
        {
            _logger.LogWarning("删除仓库失败：{RepositoryId} 未找到", id);
            return false;
        }

        _context.Repositories.Remove(repository);
        await _context.SaveChangesAsync();
        _logger.LogInformation("仓库已删除：{RepositoryId} {RepositoryName}", id, repository.Name);
        return true;
    }

    public async Task InitializeRepositoryAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (repository.IsInitialized)
            throw new Exception("仓库已初始化");

        _logger.LogInformation("开始初始化仓库：{RepositoryId} {RepositoryName}", repository.Id, repository.Name);
        await _resticService.InitializeRepositoryAsync(repository);

        repository.IsInitialized = true;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ValidateRepositoryAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");
        await _resticService.ListSnapshotsAsync(repository);
        repository.IsInitialized = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<RepositoryStats> GetRepositoryStatsAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        var stats = await _resticService.GetStatsAsync(repository);

        // 统计结果持久化到仓库行，页面刷新后直接展示，无需重新统计
        repository.StatsJson = JsonSerializer.Serialize(stats);
        repository.StatsUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return stats;
    }

    public async Task<List<SnapshotInfo>> ListSnapshotsAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        return await _resticService.ListSnapshotsAsync(repository);
    }

    public async Task<List<ResticFileTreeNode>> ListSubdirectoryFilesInSnapshotTreeAsync(int id, string snapshotId, string path)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        path = PathFormat.Format(path);

        var allFiles = await _resticService.ListFilesInSnapshotAsync(repository, snapshotId, path);

        var subdirectoryFiles = allFiles
            .Where(f => IsDirectChildOf(f.Path, path))
            .Select(f => new ResticFileTreeNode
            {
                Name = f.Name,
                Type = f.Type,
                Size = f.Size,
                Path = f.Path,
                Permissions = f.Permissions,
                Mtime = f.Mtime,
                IsLeaf = f.Type == "file" || !allFiles.Any(child => IsDirectChildOf(child.Path, f.Path))
            })
            .ToList();

        // 对节点按名称排序
        SortTreeNodes(subdirectoryFiles);

        return subdirectoryFiles;
    }

    /// <summary>
    /// 比较快照差异
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="snapshotId1">第一个快照ID</param>
    /// <param name="snapshotId2">第二个快照ID</param>
    /// <returns>快照差异结果</returns>
    public async Task<SnapshotDiffResult> DiffSnapshotsAsync(int id, string snapshotId1, string snapshotId2)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        return await _resticService.DiffSnapshotsAsync(repository, snapshotId1, snapshotId2);
    }

    public async Task RestoreSnapshotAsync(int id, string snapshotId, string targetPath, bool verify = false)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        await _resticService.RestoreSnapshotAsync(repository, snapshotId, targetPath, verify);
    }

    public async Task RestoreFileAsync(int id, string snapshotId, string filePath, string targetPath, bool verify = false)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        await _resticService.RestoreFileAsync(repository, snapshotId, filePath, targetPath, verify);
    }

    public async Task DeleteSnapshotAsync(int id, string snapshotId)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        if (string.IsNullOrWhiteSpace(snapshotId))
            throw new ArgumentException("快照ID不能为空", nameof(snapshotId));

        await _resticService.DeleteSnapshotAsync(repository, snapshotId);
    }

    public async Task DeleteSnapshotsAsync(int id, IEnumerable<string> snapshotIds)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        await _resticService.DeleteSnapshotsAsync(repository, snapshotIds);
    }

    public async Task<string> UnlockRepositoryAsync(int id, bool removeAll = false)
    {
        var repository = await GetInitializedRepositoryAsync(id);
        return await _resticService.UnlockRepositoryAsync(repository, removeAll);
    }

    public async Task<string> PruneRepositoryAsync(int id)
    {
        var repository = await GetInitializedRepositoryAsync(id);
        return await _resticService.PruneRepositoryAsync(repository);
    }

    public async Task<string> RepairIndexAsync(int id)
    {
        var repository = await GetInitializedRepositoryAsync(id);
        return await _resticService.RepairIndexAsync(repository);
    }

    public async Task<string> RepairSnapshotsAsync(int id)
    {
        var repository = await GetInitializedRepositoryAsync(id);
        return await _resticService.RepairSnapshotsAsync(repository);
    }

    public async Task<string> DeepCheckRepositoryAsync(int id, string readDataSubset)
    {
        var repository = await GetInitializedRepositoryAsync(id);
        return await _resticService.CheckRepositoryAsync(repository, readDataSubset);
    }

    public async Task DumpFileAsync(int id, string snapshotId, string filePath, Stream output)
    {
        var repository = await GetInitializedRepositoryAsync(id);
        await _resticService.DumpFileAsync(repository, snapshotId, filePath, output);
    }

    public async Task<List<FindResult>> FindInSnapshotsAsync(int id, string pattern)
    {
        var repository = await GetInitializedRepositoryAsync(id);
        return await _resticService.FindInSnapshotsAsync(repository, pattern);
    }

    public async Task SetSnapshotTagsAsync(int id, string snapshotId, List<string> tags)
    {
        var repository = await GetInitializedRepositoryAsync(id);
        await _resticService.SetSnapshotTagsAsync(repository, snapshotId, tags);
    }

    /// <summary>
    /// 创建/更新时的仓库配置校验：类型白名单 + 按类型的 location 前缀 + 凭据 JSON 合法性
    /// </summary>
    private static void ValidateRepository(Repository repository)
    {
        if (string.IsNullOrWhiteSpace(repository.Type) || !RepositoryTypes.All.Contains(repository.Type))
            throw new ArgumentException($"不支持的仓库类型: {repository.Type ?? "(空)"}，支持 local/sftp/rest/s3/b2/azure/gs/rclone");

        // restic 远程后端的 location 都以 "类型:" 开头（如 s3:endpoint/bucket），local 除外
        if (!RepositoryTypes.Local.Equals(repository.Type, StringComparison.OrdinalIgnoreCase)
            && !repository.Path.StartsWith(repository.Type + ":", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"仓库地址必须以 {repository.Type}: 开头");

        if (!string.IsNullOrWhiteSpace(repository.CredentialsJson))
        {
            try
            {
                using (JsonDocument.Parse(repository.CredentialsJson)) { }
            }
            catch (JsonException ex)
            {
                // JsonException 不是 ArgumentException 子类，包装后才能被 Controller 转 400
                throw new ArgumentException("凭据 JSON 格式不合法", ex);
            }
        }
    }

    /// <summary>
    /// 获取已初始化的仓库，未找到或未初始化时抛出异常
    /// </summary>
    private async Task<Repository> GetInitializedRepositoryAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        return repository;
    }

    /// <summary>
    /// 判断一个路径是否为指定父路径的直接子项
    /// </summary>
    /// <param name="childPath">子路径</param>
    /// <param name="parentPath">父路径</param>
    /// <returns>是否为直接子项</returns>
    private bool IsDirectChildOf(string childPath, string parentPath)
    {
        // 如果父路径为空，则查找根目录下的文件
        if (string.IsNullOrEmpty(parentPath))
        {
            // 根目录下的文件路径应该只包含一个斜杠（开头的斜杠）
            return childPath.StartsWith("/") && childPath.Substring(1).IndexOf('/') == -1;
        }

        // 确保父路径以斜杠结尾
        if (!parentPath.EndsWith("/"))
        {
            parentPath += "/";
        }

        // 检查childPath是否以parentPath开头
        if (!childPath.StartsWith(parentPath))
        {
            return false;
        }

        // 获取相对路径部分
        var relativePath = childPath.Substring(parentPath.Length);

        // 检查相对路径中是否还包含斜杠（表示更深层的子目录）
        return relativePath.IndexOf('/') == -1;
    }

    /// <summary>
    /// 对树节点进行排序（只排序当前层级）
    /// </summary>
    /// <param name="nodes">节点列表</param>
    private void SortTreeNodes(List<ResticFileTreeNode> nodes)
    {
        // 先对当前层节点按名称排序（目录优先，然后是文件）
        nodes.Sort((a, b) =>
        {
            // 目录优先于文件
            if (a.Type == "dir" && b.Type != "dir")
                return -1;
            if (a.Type != "dir" && b.Type == "dir")
                return 1;

            // 相同类型按名称排序
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
    }
}