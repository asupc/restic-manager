using Microsoft.EntityFrameworkCore;
using Quartz;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Application.Services.Implements;

public class MaintenanceSettingService : IMaintenanceSettingService
{
    private readonly ResticManagerDbContext _context;

    public MaintenanceSettingService(ResticManagerDbContext context)
    {
        _context = context;
    }

    public async Task<List<MaintenanceSetting>> GetAllAsync()
    {
        // 种子按 MaintenanceDefinitions.Order 顺序插入，Id 序即定义序
        return await _context.MaintenanceSettings.OrderBy(s => s.Id).ToListAsync();
    }

    public async Task<MaintenanceSetting?> UpdateAsync(int id, bool isEnabled, string cronExpression)
    {
        var setting = await _context.MaintenanceSettings.FindAsync(id);
        if (setting == null)
            return null;

        if (string.IsNullOrWhiteSpace(cronExpression) || !CronExpression.IsValidExpression(cronExpression))
        {
            throw new InvalidOperationException("Cron 表达式无效,需 6 段 Quartz 格式,如 0 30 4 * * ?");
        }

        setting.IsEnabled = isEnabled;
        setting.CronExpression = cronExpression.Trim();
        setting.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return setting;
    }

    public async Task EnsureSeededAsync()
    {
        var existingTypes = await _context.MaintenanceSettings.Select(s => s.Type).ToListAsync();
        var missing = MaintenanceDefinitions.All.Where(d => !existingTypes.Contains(d.Type));
        foreach (var def in missing)
        {
            _context.MaintenanceSettings.Add(new MaintenanceSetting
            {
                Type = def.Type,
                IsEnabled = def.DefaultEnabled,
                CronExpression = def.DefaultCron,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (missing.Any())
        {
            await _context.SaveChangesAsync();
        }
    }
}
