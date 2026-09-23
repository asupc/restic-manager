using System.ComponentModel.DataAnnotations;

namespace ResticManager.Models;

/// <summary>
/// 备份规则实体类，定义备份任务的过滤规则和保留策略
/// </summary>
public class BackupRule : BaseModel
{

    /// <summary>
    /// 规则名称，便于识别和复用，必须填写
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 包含模式，用于指定需要备份的文件或目录模式，多个模式用分号分隔
    /// </summary>
    public string IncludePatterns { get; set; } = string.Empty;

    /// <summary>
    /// 排除模式，用于指定不需要备份的文件或目录模式，多个模式用分号分隔
    /// </summary>
    public string ExcludePatterns { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用每日备份保留策略
    /// </summary>
    public bool KeepDaily { get; set; } = true;
    
    /// <summary>
    /// 每日备份保留数量
    /// </summary>
    public int KeepDailyCount { get; set; } = 7;

    /// <summary>
    /// 是否启用每周备份保留策略
    /// </summary>
    public bool KeepWeekly { get; set; } = true;
    
    /// <summary>
    /// 每周备份保留数量
    /// </summary>
    public int KeepWeeklyCount { get; set; } = 4;

    /// <summary>
    /// 是否启用每月备份保留策略
    /// </summary>
    public bool KeepMonthly { get; set; } = true;

    /// <summary>
    /// 每月备份保留数量
    /// </summary>
    public int KeepMonthlyCount { get; set; } = 12;

    /// <summary>
    /// 是否启用每年备份保留策略
    /// </summary>
    public bool KeepYearly { get; set; } = false;

    /// <summary>
    /// 每年备份保留数量
    /// </summary>
    public int KeepYearlyCount { get; set; } = 2;

    /// <summary>
    /// 规则创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 规则更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}