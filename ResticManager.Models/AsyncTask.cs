using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResticManager.Models;

/// <summary>
/// 后台异步任务实体。所有需要访问 restic 仓库的操作（同步快照、恢复、清理、校验等）
/// 都登记为一条 AsyncTask：HTTP 请求立即返回任务 ID，实际执行在后台进行，
/// 前端通过轮询任务状态获取结果。
/// </summary>
public class AsyncTask : BaseModel
{
    /// <summary>
    /// 任务类型，取值见 ResticManager.Application.Services.Abstractions.AsyncTaskTypes
    /// </summary>
    [Required]
    [MaxLength(40)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 关联的仓库 ID；仓库删除后任务历史保留（置空）
    /// </summary>
    public int? RepositoryId { get; set; }

    /// <summary>
    /// 人类可读的任务名（如"同步仓库 NAS-备份 的快照"），通知与任务列表直接展示
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 防重入键：BackupRun=备份任务Id；快照类操作=快照Id（文件树为 "{snapshotId}:{path}"）；仓库级操作为空串。
    /// 同 (Type, RepositoryId, TargetKey) 存在 Pending/Running 任务时拒绝重复提交
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TargetKey { get; set; } = string.Empty;

    /// <summary>
    /// 请求参数 JSON（snapshotId/targetPath/verify/pattern/subset 等），执行时反序列化
    /// </summary>
    public string? ParametersJson { get; set; }

    /// <summary>
    /// 任务状态：Pending（排队中） / Running（执行中） / Succeeded / Failed
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// 成功结果 JSON（SyncSnapshotsResult、SnapshotDiffResult、文件树等），失败时为空
    /// </summary>
    public string? ResultJson { get; set; }

    /// <summary>
    /// 失败信息（异常 Message）
    /// </summary>
    [MaxLength(4000)]
    public string? Error { get; set; }

    /// <summary>
    /// 触发方式：Manual（页面手动） / Quartz（定时调度）
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Trigger { get; set; } = "Manual";

    /// <summary>
    /// 开始执行（取得执行权）时间，UTC
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间，UTC
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// 提交时间，UTC
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的仓库（导航属性）
    /// </summary>
    [ForeignKey(nameof(RepositoryId))]
    public Repository? Repository { get; set; }
}
