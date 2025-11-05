using System.ComponentModel.DataAnnotations;

namespace ResticManager.Models;

/// <summary>
/// 备份任务实体类，表示一个备份任务的配置和状态信息
/// </summary>
public class BackupTask : BaseModel
{
    /// <summary>
    /// 任务名称，必须填写
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 任务描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 源路径，必须填写，表示需要备份的目录路径
    /// </summary>
    [Required]
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// 关联的仓库ID，必须填写
    /// </summary>
    [Required]
    public int RepositoryId { get; set; }

    /// <summary>
    /// 任务是否启用，默认为true
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 任务创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后一次运行时间
    /// </summary>
    public DateTime? LastRunAt { get; set; }
    
    /// <summary>
    /// 仓库类型（local, sftp, s3等）
    /// </summary>
    public string RepositoryType { get; set; } = "local";
    
    /// <summary>
    /// 最后一次运行的状态
    /// </summary>
    public string LastRunStatus { get; set; } = "NeverRun";
    
    /// <summary>
    /// 任务计划Cron表达式，用于定时执行任务
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;
    
    /// <summary>
    /// 关联的备份规则ID
    /// </summary>
    public int? BackupRuleId { get; set; }

    /// <summary>
    /// 导航属性，关联的备份规则
    /// </summary>
    public BackupRule Rule { get; set; }
    
    /// <summary>
    /// 导航属性，关联的仓库
    /// </summary>
    public Repository Repository { get; set; }
    
    ///// <summary>
    ///// 导航属性，任务的备份记录列表
    ///// </summary>
    //public List<BackupRecord> Records { get; set; } = new();
    
    ///// <summary>
    ///// 导航属性，任务的备份文件列表
    ///// </summary>
    //public List<BackupFile> Files { get; set; } = new();
}