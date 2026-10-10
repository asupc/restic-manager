using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Application.Services.Implements;

public class BackupRuleService : IBackupRuleService
{
    private readonly ResticManagerDbContext _context;
    private readonly ILogger<BackupRuleService> _logger;

    public BackupRuleService(ResticManagerDbContext context, ILogger<BackupRuleService> logger)
    {
        _context = context;
        _logger = logger;
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
        _logger.LogInformation("备份规则已创建：{RuleId} {RuleName}", rule.Id, rule.Name);
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
        _logger.LogInformation("备份规则已更新：{RuleId} {RuleName}", existingRule.Id, existingRule.Name);
        return existingRule;
    }

    public async Task<bool> DeleteRuleAsync(int id)
    {
        var rule = await GetRuleByIdAsync(id);
        if (rule == null) return false;

        _context.BackupRules.Remove(rule);
        await _context.SaveChangesAsync();
        _logger.LogInformation("备份规则已删除：{RuleId} {RuleName}", id, rule.Name);
        return true;
    }
}