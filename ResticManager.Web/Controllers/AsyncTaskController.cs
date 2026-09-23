using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Models;

namespace ResticManager.Web.Controllers;

/// <summary>
/// 后台异步任务查询接口：前端提交仓库类操作后立即返回，
/// 通过这里的接口轮询任务状态与结果。
/// </summary>
[ApiController]
[Route("api/asynctasks")]
[Authorize]
public class AsyncTaskController : ControllerBase
{
    private readonly IAsyncTaskService _asyncTaskService;

    public AsyncTaskController(IAsyncTaskService asyncTaskService)
    {
        _asyncTaskService = asyncTaskService;
    }

    /// <summary>
    /// 获取单个任务的状态与结果
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AsyncTask>> GetTask(int id)
    {
        var task = await _asyncTaskService.GetTaskAsync(id);
        if (task == null)
        {
            return NotFound(new { message = "任务不存在" });
        }
        return Ok(task);
    }

    /// <summary>
    /// 获取全部进行中（Pending/Running）的任务
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<AsyncTask>>> GetActiveTasks()
    {
        return Ok(await _asyncTaskService.GetActiveTasksAsync());
    }

    /// <summary>
    /// 全局轮询聚合接口：一次返回进行中的任务与最近完成的任务，
    /// 供前端侧边栏角标与完成通知使用，减少轮询请求数。
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var active = await _asyncTaskService.GetActiveTasksAsync();
        var recentlyCompleted = await _asyncTaskService.GetRecentlyCompletedAsync();
        return Ok(new { active, recentlyCompleted });
    }
}
