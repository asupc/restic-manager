using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Application.Services.Implements;
using ResticManager.Models;

namespace ResticManager.Web.Controllers;

/// <summary>
/// 定时维护任务设置：列出每种维护操作的开关与执行计划，更新后立即重调度。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaintenanceSettingsController : ControllerBase
{
    private readonly IMaintenanceSettingService _settingService;
    private readonly QuartzSchedulerService _schedulerService;

    public MaintenanceSettingsController(IMaintenanceSettingService settingService, QuartzSchedulerService schedulerService)
    {
        _settingService = settingService;
        _schedulerService = schedulerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await _settingService.GetAllAsync();
        var result = new List<object>();
        foreach (var setting in settings)
        {
            result.Add(await ToViewModelAsync(setting));
        }
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMaintenanceSettingRequest request)
    {
        MaintenanceSetting setting;
        try
        {
            var updated = await _settingService.UpdateAsync(id, request.IsEnabled, request.CronExpression);
            if (updated == null)
            {
                return NotFound(new { message = "未找到指定的维护配置" });
            }
            setting = updated;
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // 更新调度（禁用时移除 Job）
        await _schedulerService.RescheduleMaintenanceSetting(setting, HttpContext.RequestAborted);

        return Ok(await ToViewModelAsync(setting));
    }

    private async Task<object> ToViewModelAsync(MaintenanceSetting setting)
    {
        var nextFireTime = await _schedulerService.GetNextFireTimeAsync(setting.Type, HttpContext.RequestAborted);
        return new
        {
            setting.Id,
            setting.Type,
            setting.IsEnabled,
            setting.CronExpression,
            setting.UpdatedAt,
            nextFireTime
        };
    }
}

public class UpdateMaintenanceSettingRequest
{
    public bool IsEnabled { get; set; }

    public string CronExpression { get; set; } = string.Empty;
}
