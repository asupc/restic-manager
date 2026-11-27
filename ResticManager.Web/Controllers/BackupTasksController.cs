using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Application.Services.Implements;
using ResticManager.Models;
using ResticManager.Application.Utils;
using ResticManager.Models.Dtos;

namespace ResticManager.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BackupTasksController : ControllerBase
{
    private readonly IBackupTaskService _taskService;
    private readonly QuartzSchedulerService _schedulerService;
    private readonly JwtUtils _jwtUtils;
    private readonly IAsyncTaskService _asyncTaskService;

    public BackupTasksController(IBackupTaskService taskService, QuartzSchedulerService schedulerService, JwtUtils jwtUtils, IAsyncTaskService asyncTaskService)
    {
        _taskService = taskService;
        _schedulerService = schedulerService;
        _jwtUtils = jwtUtils;
        _asyncTaskService = asyncTaskService;
    }

    /// <summary>
    /// 返回脱敏后的任务副本，清除关联仓库的敏感字段且不污染 EF 跟踪状态
    /// </summary>
    private static BackupTask SanitizeTask(BackupTask task) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Description = task.Description,
        SourcePath = task.SourcePath,
        RepositoryId = task.RepositoryId,
        IsEnabled = task.IsEnabled,
        CreatedAt = task.CreatedAt,
        LastRunAt = task.LastRunAt,
        RepositoryType = task.RepositoryType,
        LastRunStatus = task.LastRunStatus,
        CronExpression = task.CronExpression,
        BackupRuleId = task.BackupRuleId,
        Rule = task.Rule,
        Repository = task.Repository == null ? null : new Repository
        {
            Id = task.Repository.Id,
            Name = task.Repository.Name,
            Path = task.Repository.Path,
            Password = string.Empty,
            Type = task.Repository.Type,
            CredentialsJson = null,
            CreatedAt = task.Repository.CreatedAt,
            IsInitialized = task.Repository.IsInitialized,
            StatsJson = task.Repository.StatsJson,
            StatsUpdatedAt = task.Repository.StatsUpdatedAt
        }
    };

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _taskService.GetAllTasksAsync();
        return Ok(tasks.Select(SanitizeTask).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var task = await _taskService.GetTaskByIdAsync(id);

        if (task == null)
            return NotFound(new { message = "未找到指定的任务" });

        return Ok(SanitizeTask(task));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BackupTask task)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!string.IsNullOrWhiteSpace(task.CronExpression) && !CronExpression.IsValidExpression(task.CronExpression))
            return BadRequest(new { message = "无效的 Cron 表达式" });

        var createdTask = await _taskService.CreateTaskAsync(task);

        // 如果任务已启用且有Cron表达式，则安排调度
        if (createdTask.IsEnabled && !string.IsNullOrEmpty(createdTask.CronExpression))
        {
            await _schedulerService.ScheduleTask(createdTask, HttpContext.RequestAborted);
        }

        return CreatedAtAction(nameof(GetById), new { id = createdTask.Id }, SanitizeTask(createdTask));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] BackupTask task)
    {
        if (id != task.Id)
            return BadRequest();

        if (!string.IsNullOrWhiteSpace(task.CronExpression) && !CronExpression.IsValidExpression(task.CronExpression))
            return BadRequest(new { message = "无效的 Cron 表达式" });

        var existingTask = await _taskService.GetTaskByIdAsync(id);
        if (existingTask == null)
            return NotFound();

        // 更新任务
        await _taskService.UpdateTaskAsync(task);

        // 更新调度
        await _schedulerService.RescheduleTask(task, HttpContext.RequestAborted);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await _taskService.GetTaskByIdAsync(id);
        if (task == null)
            return NotFound();

        // 移除任务调度
        await _schedulerService.RemoveTask(id, HttpContext.RequestAborted);

        await _taskService.DeleteTaskAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/run")]
    public async Task<IActionResult> Run(int id)
    {
        var task = await _taskService.GetTaskByIdAsync(id);
        if (task == null)
            return NotFound(new { message = "未找到指定的任务" });
        if (!task.Repository.IsInitialized)
            return BadRequest(new { message = "关联的仓库未初始化" });

        try
        {
            // 登记后台任务后立即返回；实际执行（含保留策略与快照缓存同步）由 AsyncTaskService 完成，
            // 与 Quartz 定时触发的 BackupTaskJob 是同一条执行路径
            var taskId = await _asyncTaskService.CreateTaskAsync(
                AsyncTaskTypes.BackupRun,
                task.RepositoryId,
                $"备份任务：{task.Name}",
                new { backupTaskId = id },
                targetKey: id.ToString());
            return Ok(new { taskId, message = "备份任务已提交，正在后台执行" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("folders")]
    public IActionResult GetFolders([FromQuery] string path = "")
    {
        // 如果没有提供路径，则使用根路径
        var directoryPath = string.IsNullOrEmpty(path) ? "/" : path;

        // 安全校验非法路径字符
        if (directoryPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || directoryPath.Contains('\0'))
        {
            return BadRequest(new { message = "路径包含非法字符" });
        }

        // 在Windows系统上，如果没有提供路径且是根路径，则列出所有驱动器
        if (string.IsNullOrEmpty(path) && Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new FolderInfo
                {
                    Name = d.Name,
                    Type = "folder"
                })
                .ToList();
            return Ok(drives);
        }

        try
        {
            // 确保路径存在且为目录
            if (!Directory.Exists(directoryPath))
            {
                return BadRequest(new { message = "指定的路径不存在或不是目录" });
            }

            // 获取目录内容
            var folderInfos = new List<FolderInfo>();

            // 添加子目录
            var directories = Directory.GetDirectories(directoryPath);
            foreach (var dir in directories)
            {
                folderInfos.Add(new FolderInfo
                {
                    Name = Path.GetFileName(dir),
                    Type = "folder"
                });
            }

            // 添加文件
            var files = Directory.GetFiles(directoryPath);
            foreach (var file in files)
            {
                folderInfos.Add(new FolderInfo
                {
                    Name = Path.GetFileName(file),
                    Type = "file"
                });
            }

            return Ok(folderInfos);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "没有权限访问指定目录" });
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return BadRequest(new { message = "无效的路径格式" });
        }
        catch (IOException ex)
        {
            return BadRequest(new { message = $"无法读取目录内容：{ex.Message}" });
        }
    }

    /// <summary>
    /// 更新任务的调度计划
    /// </summary>
    /// <param name="id">任务ID</param>
    /// <param name="request">包含Cron表达式的请求对象</param>
    /// <returns>成功返回NoContent，失败返回相应错误</returns>
    [HttpPut("{id}/schedule")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] UpdateScheduleRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "请求体不能为空" });

        if (!string.IsNullOrWhiteSpace(request.CronExpression) && !CronExpression.IsValidExpression(request.CronExpression))
            return BadRequest(new { message = "无效的 Cron 表达式" });

        var task = await _taskService.GetTaskByIdAsync(id);
        if (task == null)
            return NotFound();

        task.CronExpression = request.CronExpression;
        await _taskService.UpdateTaskAsync(task);

        // 更新调度
        await _schedulerService.RescheduleTask(task, HttpContext.RequestAborted);

        return NoContent();
    }
}

public class UpdateScheduleRequest
{
    public string CronExpression { get; set; } = string.Empty;
}