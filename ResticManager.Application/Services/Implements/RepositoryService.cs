using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Application.Utils;
using ResticManager.Data;
using ResticManager.Models;
using ResticManager.Models.Dtos;
using System.Text.RegularExpressions;
// 添加 EFCore.BulkExtensions 引用
// using EFCore.BulkExtensions; // 不再需要这个引用

namespace ResticManager.Application.Services.Implements;

public partial class RepositoryService : IRepositoryService
{
    private readonly ResticManagerDbContext _context;
    private readonly IResticService _resticService;

    public RepositoryService(ResticManagerDbContext context, IResticService resticService)
    {
        _context = context;
        _resticService = resticService;
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

        _context.Repositories.Add(repository);
        await _context.SaveChangesAsync();
        return repository;
    }

    public async Task<Repository> UpdateRepositoryAsync(int id, Repository repository)
    {
        var existingRepo = await _context.Repositories.FindAsync(id);
        if (existingRepo == null) return null;

        existingRepo.Name = repository.Name;
        existingRepo.Path = repository.Path;
        existingRepo.Password = repository.Password;
        existingRepo.Type = repository.Type;
        existingRepo.IsInitialized = repository.IsInitialized;

        await _context.SaveChangesAsync();
        return existingRepo;
    }

    public async Task<bool> DeleteRepositoryAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            return false;

        _context.Repositories.Remove(repository);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task InitializeRepositoryAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (repository.IsInitialized)
            throw new Exception("仓库已初始化");

        await _resticService.InitializeRepositoryAsync(repository.Path, repository.Password);

        repository.IsInitialized = true;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ValidateRepositoryAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");
        await _resticService.ListSnapshotsAsync(repository.Path, repository.Password);
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

        return await _resticService.GetStatsAsync(repository.Path, repository.Password);
    }

    public async Task<List<SnapshotInfo>> ListSnapshotsAsync(int id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        return await _resticService.ListSnapshotsAsync(repository.Path, repository.Password);
    }

    public async Task<List<ResticFileTreeNode>> ListSubdirectoryFilesInSnapshotTreeAsync(int id, string snapshotId, string path)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        path = PathFormat.Format(path);

        var allFiles = await _resticService.ListFilesInSnapshotAsync(repository.Path, repository.Password, snapshotId, path);

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

    public async Task RestoreSnapshotAsync(int id, string snapshotId, string targetPath)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        await _resticService.RestoreSnapshotAsync(repository.Path, repository.Password, snapshotId, targetPath);
    }

    public async Task RestoreFileAsync(int id, string snapshotId, string filePath, string targetPath)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository == null)
            throw new Exception("仓库未找到");

        if (!repository.IsInitialized)
            throw new Exception("仓库未初始化");

        await _resticService.RestoreFileAsync(repository.Path, repository.Password, snapshotId, filePath, targetPath);
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

        await _resticService.DeleteSnapshotAsync(repository.Path, repository.Password, snapshotId);
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