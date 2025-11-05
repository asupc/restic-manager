using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Application.Services.Implements;

public class BackupRuleService : IBackupRuleService
{
    private readonly ResticManagerDbContext _context;

    public BackupRuleService(ResticManagerDbContext context)
    {
        _context = context;
    }

    public async Task<List<BackupRule>> GetAllRules()
    {
        return await _context.BackupRules.ToListAsync();
    }

    public async Task<BackupRule> GetRuleByIdAsync(int id)
    {
        return await _context.BackupRules
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<BackupRule> CreateRuleAsync(BackupRule rule)
    {
        rule.CreatedAt = DateTime.Now;
        rule.UpdatedAt = DateTime.Now;
        _context.BackupRules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    public async Task<BackupRule> UpdateRuleAsync(int id, BackupRule updatedRule)
    {
        var existingRule = await GetRuleByIdAsync(id);
        if (existingRule == null)
            return null;

        existingRule.Name = updatedRule.Name;
        existingRule.IncludePatterns = updatedRule.IncludePatterns;
        existingRule.ExcludePatterns = updatedRule.ExcludePatterns;
        existingRule.KeepDaily = updatedRule.KeepDaily;
        existingRule.KeepDailyCount = updatedRule.KeepDailyCount;
        existingRule.KeepWeekly = updatedRule.KeepWeekly;
        existingRule.KeepWeeklyCount = updatedRule.KeepWeeklyCount;
        existingRule.KeepMonthly = updatedRule.KeepMonthly;
        existingRule.KeepMonthlyCount = updatedRule.KeepMonthlyCount;
        existingRule.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        return existingRule;
    }

    public async Task<bool> DeleteRuleAsync(int id)
    {
        var rule = await GetRuleByIdAsync(id);
        if (rule == null) return false;

        _context.BackupRules.Remove(rule);
        await _context.SaveChangesAsync();
        return true;
    }
}