using ResticManager.Models;

namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 定时维护任务配置的读写与种子初始化。
/// </summary>
public interface IMaintenanceSettingService
{
    /// <summary>
    /// 全部维护配置（按 Id 升序，即 MaintenanceDefinitions 的定义顺序）
    /// </summary>
    Task<List<MaintenanceSetting>> GetAllAsync();

    /// <summary>
    /// 更新开关与执行计划。Cron 无效时抛 InvalidOperationException（Controller 转 400）。
    /// </summary>
    Task<MaintenanceSetting?> UpdateAsync(int id, bool isEnabled, string cronExpression);

    /// <summary>
    /// 按 MaintenanceDefinitions 补插缺失的配置行（不覆盖用户已修改的值），幂等。
    /// </summary>
    Task EnsureSeededAsync();
}
