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

    public RepositoryController(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
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

    [HttpPost("{id}/initialize")]
    public async Task<IActionResult> InitializeRepository(int id)
    {
        try
        {
            await _repositoryService.InitializeRepositoryAsync(id);
            return Ok(new { message = "仓库初始化成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/validate")]
    public async Task<IActionResult> ValidateRepository(int id)
    {
        try
        {
            var isValid = await _repositoryService.ValidateRepositoryAsync(id);
            return Ok(new { valid = isValid });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/stats")]
    public async Task<ActionResult<RepositoryStats>> GetRepositoryStats(int id)
    {
        try
        {
            var stats = await _repositoryService.GetRepositoryStatsAsync(id);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/snapshots")]
    public async Task<ActionResult<List<SnapshotInfo>>> ListSnapshots(int id)
    {
        try
        {
            var snapshots = await _repositoryService.ListSnapshotsAsync(id);
            return Ok(snapshots);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    //[HttpGet("{id}/snapshots/{snapshotId}/files-tree")]
    //public async Task<ActionResult<List<ResticFileTreeNode>>> ListFilesInSnapshotTree(int id, string snapshotId)
    //{
    //    try
    //    {
    //        var treeNodes = await _repositoryService.ListFilesInSnapshotTreeAsync(id, snapshotId, "/");
    //        return Ok(treeNodes);
    //    }
    //    catch (Exception ex)
    //    {
    //        return BadRequest(new { Error = ex.Message });
    //    }
    //}

    [HttpGet("{id}/snapshots/{snapshotId}/subdirectory-files-tree")]
    public async Task<ActionResult<List<ResticFileTreeNode>>> ListSubdirectoryFilesInSnapshotTree(int id, string snapshotId, [FromQuery] string path = "")
    {
        try
        {
            var subTreeNodes = await _repositoryService.ListSubdirectoryFilesInSnapshotTreeAsync(id, snapshotId, path);
            return Ok(subTreeNodes);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("{id}/snapshots/{snapshotId}/restore")]
    public async Task<IActionResult> RestoreSnapshot(int id, string snapshotId, [FromBody] RestoreRequest request)
    {
        try
        {
            await _repositoryService.RestoreSnapshotAsync(id, snapshotId, request.TargetPath);
            return Ok(new { message = "快照恢复成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("{id}/files/restore")]
    public async Task<IActionResult> RestoreFile(int id, [FromBody] RestoreFileRequest request)
    {
        try
        {
            await _repositoryService.RestoreFileAsync(id, request.SnapshotId, request.FilePath, request.TargetPath);
            return Ok(new { Message = "文件恢复成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpDelete("{id}/snapshots/{snapshotId}")]
    public async Task<IActionResult> DeleteSnapshot(int id, string snapshotId)
    {
        try
        {
            await _repositoryService.DeleteSnapshotAsync(id, snapshotId);
            return Ok(new { message = "快照删除成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}

public class RestoreFileRequest
{
    public string SnapshotId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
}

public class RestoreRequest
{
    public string TargetPath { get; set; } = string.Empty;
}

public class ResticFileTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Permissions { get; set; } = string.Empty;
    public DateTime Mtime { get; set; }
    public bool IsLeaf { get; set; }
    public List<ResticFileTreeNode> Children { get; set; } = new();
}