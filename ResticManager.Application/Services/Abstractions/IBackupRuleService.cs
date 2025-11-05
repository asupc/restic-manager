using ResticManager.Models;

namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 备份规则服务接口，提供备份规则的管理功能
/// </summary>
public interface IBackupRuleService
{
    
    /// <summary>
    /// 根据ID获取备份规则
    /// </summary>
    /// <param name="id">规则ID</param>
    /// <returns>备份规则对象</returns>
    Task<BackupRule> GetRuleByIdAsync(int id);
    
    /// <summary>
    /// 创建备份规则
    /// </summary>
    /// <param name="rule">备份规则对象</param>
    /// <returns>创建的备份规则</returns>
    Task<BackupRule> CreateRuleAsync(BackupRule rule);
    
    /// <summary>
    /// 更新备份规则
    /// </summary>
    /// <param name="id">规则ID</param>
    /// <param name="updatedRule">更新后的备份规则对象</param>
    /// <returns>更新后的备份规则</returns>
    Task<BackupRule> UpdateRuleAsync(int id, BackupRule updatedRule);
    
    /// <summary>
    /// 删除备份规则
    /// </summary>
    /// <param name="id">规则ID</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteRuleAsync(int id);
}