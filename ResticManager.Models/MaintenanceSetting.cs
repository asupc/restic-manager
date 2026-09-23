using System.ComponentModel.DataAnnotations;

namespace ResticManager.Models;

/// <summary>
/// 定时维护任务配置。每种维护操作（刷新统计、快照同步、深度校验等）固定一行，
/// 开关与执行计划可在设置页面调整；触发时对全部已初始化仓库批量执行。
/// 类型清单与默认值见 ResticManager.Application.Services.Abstractions.MaintenanceDefinitions。
/// </summary>
public class MaintenanceSetting : BaseModel
{
    /// <summary>
    /// 维护类型，取 AsyncTaskTypes 常量值（如 RepositoryStats），全局唯一
    /// </summary>
    [Required]
    [MaxLength(40)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用定时执行
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Quartz Cron 表达式（6 段格式，如 0 30 4 * * ?），时区为本地时区
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// 最后修改时间，UTC
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
