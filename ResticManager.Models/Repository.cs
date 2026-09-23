using System.ComponentModel.DataAnnotations;

namespace ResticManager.Models;

/// <summary>
/// 仓库实体类，表示一个restic备份仓库的配置信息
/// </summary>
public class Repository : BaseModel
{

    /// <summary>
    /// 仓库名称，必须填写
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 仓库路径，必须填写
    /// </summary>
    [Required]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 仓库密码，必须填写
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 仓库类型，默认为local，支持local, sftp, s3等
    /// </summary>
    public string Type { get; set; } = "local";

    /// <summary>
    /// 仓库创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 仓库是否已初始化
    /// </summary>
    public bool IsInitialized { get; set; } = false;

    /// <summary>
    /// 最近一次成功统计的结果 JSON（持久化，页面刷新后无需重新统计）
    /// </summary>
    public string? StatsJson { get; set; }

    /// <summary>
    /// 最近一次成功统计的时间（UTC）
    /// </summary>
    public DateTime? StatsUpdatedAt { get; set; }

    /// <summary>
    /// 导航属性，关联到该仓库的所有备份任务
    /// </summary>
    public List<BackupTask> Tasks { get; set; } = new();
}