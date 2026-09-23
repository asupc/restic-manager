using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RepositoryController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly IRepositoryHealthCheckService _healthCheckService;
    private readonly ISnapshotService _snapshotService;
    private readonly IAsyncTaskService _asyncTaskService;

    public RepositoryController(IRepositoryService repositoryService,
        IRepositoryHealthCheckService healthCheckService,
        ISnapshotService snapshotService,
        IAsyncTaskService asyncTaskService)
    {
        _repositoryService = repositoryService;
        _healthCheckService = healthCheckService;
        _snapshotService = snapshotService;
        _asyncTaskService = asyncTaskService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Repository>>> GetRepositories()
    {
        var repositories = await _repositoryService.GetAllRepositoriesAsync();
        return Ok(repositories);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Repository>> GetRepository(int id)
    {
        var repository = await _repositoryService.GetRepositoryByIdAsync(id);
        if (repository == null)
        {
            return NotFound(new { message = "未找到指定的仓库" });
        }
        return Ok(repository);
    }

    [HttpPost]
    public async Task<ActionResult<Repository>> CreateRepository([FromBody] Repository repository)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var createdRepository = await _repositoryService.CreateRepositoryAsync(repository);
        return CreatedAtAction(nameof(GetRepository), new { id = createdRepository.Id }, createdRepository);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRepository(int id, [FromBody] Repository repository)
    {
        if (id != repository.Id)
        {
            return BadRequest(new { message = "仓库ID不匹配" });
        }

        try
        {
            var updatedRepository = await _repositoryService.UpdateRepositoryAsync(id, repository);
            return Ok(updatedRepository);
        }
        catch (Exception)
        {
            return NotFound(new { message = "未找到指定的仓库" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRepository(int id)
    {
        var result = await _repositoryService.DeleteRepositoryAsync(id);
        if (!result)
        {
            return NotFound(new { message = "未找到指定的仓库" });
        }

        return NoContent();
    }

    /// <summary>
    /// 校验仓库存在后登记后台任务并立即返回任务 ID。
    /// titleFactory 用仓库名生成任务标题（如"同步仓库 NAS-备份 的快照"）。
    /// </summary>
    private async Task<IActionResult> SubmitRepositoryTaskAsync(int id, string type,
        Func<string, string> titleFactory, object parameters, string targetKey = "", string message = "任务已提交")
    {
        var repository = await _repositoryService.GetRepositoryByIdAsync(id);
        if (repository == null)
        {
            return BadRequest(new { message = "仓库未找到" });
        }

        try
        {
            var taskId = await _asyncTaskService.CreateTaskAsync(type, id, titleFactory(repository.Name), parameters, targetKey);
            return Ok(new { taskId, message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/initialize")]
    public Task<IActionResult> InitializeRepository(int id)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryInitialize,
            name => $"初始化仓库 {name}", new { }, message: "初始化任务已提交");
    }

    [HttpPost("{id}/validate")]
    public Task<IActionResult> ValidateRepository(int id)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryCheck,
            name => $"校验仓库 {name}", new { }, message: "校验任务已提交");
    }

    [HttpPost("{id}/check")]
    public Task<IActionResult> RunHealthCheck(int id)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryCheck,
            name => $"校验仓库 {name}", new { }, message: "健康检查任务已提交");
    }

    [HttpGet("{id}/health-records")]
    public async Task<ActionResult<List<RepositoryCheckRecord>>> GetHealthRecords(int id, [FromQuery] int limit = 20)
    {
        try
        {
            var records = await _healthCheckService.GetRecordsByRepositoryAsync(id, Math.Clamp(limit, 1, 200));
            return Ok(records);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("health-overview")]
    public async Task<ActionResult<RepositoryHealthOverview>> GetHealthOverview()
    {
        try
        {
            var overview = await _healthCheckService.GetOverviewAsync();
            return Ok(overview);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/stats")]
    public Task<IActionResult> GetRepositoryStats(int id)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryStats,
            name => $"统计仓库 {name}", new { }, message: "统计任务已提交");
    }

    [HttpGet("{id}/snapshots")]
    public async Task<ActionResult<List<BackupSnapshotDto>>> ListSnapshots(int id)
    {
        try
        {
            // 保持与旧实现（远端实时拉取）一致的仓库校验
            var repository = await _repositoryService.GetRepositoryByIdAsync(id);
            if (repository == null)
                return BadRequest(new { message = "仓库未找到" });
            if (!repository.IsInitialized)
                return BadRequest(new { message = "仓库未初始化" });

            // 只读本地 DB（避开网盘远端调用）。
            // 本地缓存为空（首次部署、升级、跨设备导入）时由前端引导用户点"同步远端"，
            // 以异步任务方式填充缓存，不再在 GET 内阻塞式同步。
            var snapshots = await _snapshotService.GetByRepositoryAsync(id);

            var dtos = snapshots.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 把 BackupSnapshot 实体映射为前端消费的 DTO。
    /// </summary>
    private static BackupSnapshotDto MapToDto(BackupSnapshot s) => new()
    {
        LocalId = s.Id,
        Id = s.SnapshotId,
        ShortId = s.ShortId,
        Time = s.Time,
        Tree = s.Tree,
        Paths = s.Paths ?? new List<string>(),
        Tags = s.Tags ?? new List<string>(),
        Hostname = s.Hostname,
        Username = s.Username,
        Uid = s.Uid,
        Gid = s.Gid,
        ProgramVersion = s.ProgramVersion,
        Parent = s.ParentSnapshotId,
        Summary = s.Summary,
        BackupRecordId = s.BackupRecordId,
        LastSyncedAt = s.LastSyncedAt,
        IsDeleted = s.IsDeleted
    };

    /// <summary>
    /// 手动从远端仓库全量同步快照元数据到本地 DB（后台异步任务）。
    /// 适用于：首次部署、本地缓存为空、跨设备导入仓库等场景。
    /// </summary>
    [HttpPost("{id}/snapshots/sync")]
    public Task<IActionResult> SyncSnapshots(int id)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.SnapshotSync,
            name => $"同步仓库 {name} 的快照", new { }, message: "同步任务已提交");
    }

    [HttpGet("{id}/snapshots/{snapshotId}/subdirectory-files-tree")]
    public Task<IActionResult> ListSubdirectoryFilesInSnapshotTree(int id, string snapshotId, [FromQuery] string path = "")
    {
        // 文件树展开会被连续触发，TargetKey 含 path：同目录连点被拒，不同目录互不影响
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.SnapshotFileTree,
            name => $"浏览仓库 {name} 快照 {snapshotId} 的文件",
            new { snapshotId, path },
            targetKey: $"{snapshotId}:{path}");
    }

    [HttpGet("{id}/snapshots/{snapshotId1}/diff/{snapshotId2}")]
    public Task<IActionResult> DiffSnapshots(int id, string snapshotId1, string snapshotId2)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.SnapshotDiff,
            name => $"比较仓库 {name} 的快照差异",
            new { snapshotId1, snapshotId2 },
            targetKey: $"{snapshotId1}:{snapshotId2}",
            message: "比较任务已提交");
    }

    [HttpPost("{id}/snapshots/{snapshotId}/restore")]
    public Task<IActionResult> RestoreSnapshot(int id, string snapshotId, [FromBody] RestoreRequest request)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.SnapshotRestore,
            name => $"恢复仓库 {name} 的快照 {snapshotId}",
            new { snapshotId, targetPath = request.TargetPath, verify = request.Verify },
            targetKey: snapshotId,
            message: "恢复任务已提交");
    }

    [HttpPost("{id}/files/restore")]
    public Task<IActionResult> RestoreFile(int id, [FromBody] RestoreFileRequest request)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.FileRestore,
            name => $"恢复仓库 {name} 快照 {request.SnapshotId} 中的文件",
            new { snapshotId = request.SnapshotId, filePath = request.FilePath, targetPath = request.TargetPath, verify = request.Verify },
            targetKey: $"{request.SnapshotId}:{request.FilePath}",
            message: "文件恢复任务已提交");
    }

    [HttpDelete("{id}/snapshots/{snapshotId}")]
    public Task<IActionResult> Delete(int id, string snapshotId)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.SnapshotDelete,
            name => $"删除仓库 {name} 的快照 {snapshotId}",
            new { snapshotId },
            targetKey: snapshotId,
            message: "删除任务已提交");
    }

    /// <summary>
    /// 批量删除快照（一次 restic forget 传入多个 ID，后台异步任务）
    /// </summary>
    [HttpPost("{id}/snapshots/batch-delete")]
    public Task<IActionResult> BatchDeleteSnapshots(int id, [FromBody] BatchDeleteSnapshotsRequest request)
    {
        var snapshotIds = (request?.SnapshotIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (snapshotIds.Count == 0)
        {
            return Task.FromResult<IActionResult>(BadRequest(new { message = "请选择要删除的快照" }));
        }

        // TargetKey 列限 100 字符：ID 多时截断（防重入只在进程内比对，截断不影响正确性）
        var targetKey = string.Join(",", snapshotIds);
        if (targetKey.Length > 100)
        {
            targetKey = targetKey[..100];
        }

        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.SnapshotBatchDelete,
            name => $"批量删除仓库 {name} 的 {snapshotIds.Count} 个快照",
            new { snapshotIds },
            targetKey: targetKey,
            message: "批量删除任务已提交");
    }

    /// <summary>
    /// 解锁仓库，清理陈旧的锁（后台异步任务；解锁不排队等其它仓库任务）
    /// </summary>
    [HttpPost("{id}/unlock")]
    public Task<IActionResult> Unlock(int id, [FromBody] UnlockRequest request = null)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryUnlock,
            name => $"解锁仓库 {name}",
            new { removeAll = request?.RemoveAll ?? false },
            message: "解锁任务已提交");
    }

    /// <summary>
    /// 清理仓库中未被引用的数据（prune，后台异步任务）
    /// </summary>
    [HttpPost("{id}/prune")]
    public Task<IActionResult> Prune(int id)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryPrune,
            name => $"清理仓库 {name}", new { }, message: "清理任务已提交");
    }

    /// <summary>
    /// 修复仓库索引（后台异步任务）
    /// </summary>
    [HttpPost("{id}/repair-index")]
    public Task<IActionResult> RepairIndex(int id)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryRepairIndex,
            name => $"修复仓库 {name} 的索引", new { }, message: "索引修复任务已提交");
    }

    /// <summary>
    /// 修复损坏的快照（后台异步任务）
    /// </summary>
    [HttpPost("{id}/repair-snapshots")]
    public Task<IActionResult> RepairSnapshots(int id)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryRepairSnapshots,
            name => $"修复仓库 {name} 的快照", new { }, message: "快照修复任务已提交");
    }

    /// <summary>
    /// 深度校验仓库，读取部分实际数据包（check --read-data-subset，后台异步任务）
    /// </summary>
    [HttpPost("{id}/deep-check")]
    public Task<IActionResult> DeepCheck(int id, [FromQuery] string subset = "5%")
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.RepositoryDeepCheck,
            name => $"深度校验仓库 {name}",
            new { subset },
            message: "深度校验任务已提交");
    }

    /// <summary>
    /// 跨快照按模式搜索文件（后台异步任务）
    /// </summary>
    [HttpGet("{id}/find")]
    public Task<IActionResult> Find(int id, [FromQuery] string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Task.FromResult<IActionResult>(BadRequest(new { message = "搜索关键词不能为空" }));
        }

        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.SnapshotFind,
            name => $"在仓库 {name} 中搜索文件",
            new { pattern },
            message: "搜索任务已提交");
    }

    /// <summary>
    /// 下载快照中的单个文件（restic dump 流式输出）
    /// </summary>
    [HttpGet("{id}/snapshots/{snapshotId}/download")]
    public async Task<IActionResult> DownloadFile(int id, string snapshotId, [FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { message = "文件路径不能为空" });
        }

        try
        {
            var fileName = System.IO.Path.GetFileName(path.TrimEnd('/'));
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "download";
            }

            Response.ContentType = "application/octet-stream";
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{Uri.EscapeDataString(fileName)}\"");

            await _repositoryService.DumpFileAsync(id, snapshotId, path, Response.Body);
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            if (!Response.HasStarted)
            {
                return BadRequest(new { message = ex.Message });
            }
            throw;
        }
    }

    /// <summary>
    /// 设置（覆盖）快照标签（后台异步任务）
    /// </summary>
    [HttpPut("{id}/snapshots/{snapshotId}/tags")]
    public Task<IActionResult> SetSnapshotTags(int id, string snapshotId, [FromBody] SetTagsRequest request)
    {
        return SubmitRepositoryTaskAsync(id, AsyncTaskTypes.SnapshotTagSet,
            name => $"更新仓库 {name} 快照 {snapshotId} 的标签",
            new { snapshotId, tags = request?.Tags ?? new List<string>() },
            targetKey: snapshotId,
            message: "标签更新任务已提交");
    }
}

public class RestoreRequest
{
    public string TargetPath { get; set; }
    public bool Verify { get; set; }
}

public class UnlockRequest
{
    public bool RemoveAll { get; set; }
}

public class SetTagsRequest
{
    public List<string> Tags { get; set; } = new();
}

public class BatchDeleteSnapshotsRequest
{
    public List<string> SnapshotIds { get; set; } = new();
}
