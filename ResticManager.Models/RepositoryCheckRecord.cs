using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResticManager.Models;

/// <summary>
/// 仓库健康检查历史记录实体。每次执行 restic check 后写入一条。
/// </summary>
public class RepositoryCheckRecord : BaseModel
{
    /// <summary>
    /// 关联的仓库 ID
    /// </summary>
    [Required]
    public int RepositoryId { get; set; }

    /// <summary>
    /// 检查开始时间（本地时区）
    /// </summary>
    [Required]
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 检查结果状态：Passed / Failed
    /// </summary>
    [Required]
    public string Status { get; set; } = "Passed";

    /// <summary>
    /// 检查耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// restic check 的原始输出（成功时记录）
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// 错误信息（失败时记录）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 触发方式：Scheduled（定时） / Manual（手动）
    /// </summary>
    public string Trigger { get; set; } = "Scheduled";

    /// <summary>
    /// 关联的仓库（导航属性）
    /// </summary>
    [ForeignKey(nameof(RepositoryId))]
    public Repository? Repository { get; set; }
}