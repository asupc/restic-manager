namespace ResticManager.Models.Dtos;

/// <summary>
/// 单个仓库的健康状态摘要，给首页"待办提醒"使用
/// </summary>
public class RepositoryHealthSummary
{
    /// <summary>
    /// 仓库 ID
    /// </summary>
    public int RepositoryId { get; set; }

    /// <summary>
    /// 仓库名称
    /// </summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次检查的状态：Passed / Failed / NeverChecked
    /// </summary>
    public string Status { get; set; } = "NeverChecked";

    /// <summary>
    /// 最近一次检查时间
    /// </summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>
    /// 最近一次错误信息（失败时）
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 距离上次检查的小时数（用于显示）
    /// </summary>
    public double? HoursSinceLastCheck { get; set; }
}

/// <summary>
/// 全部仓库的健康概览
/// </summary>
public class RepositoryHealthOverview
{
    /// <summary>
    /// 仓库总数
    /// </summary>
    public int TotalRepositories { get; set; }

    /// <summary>
    /// 已初始化的仓库数
    /// </summary>
    public int InitializedRepositories { get; set; }

    /// <summary>
    /// 最近一次检查通过的仓库数
    /// </summary>
    public int PassedRepositories { get; set; }

    /// <summary>
    /// 最近一次检查失败的仓库数
    /// </summary>
    public int FailedRepositories { get; set; }

    /// <summary>
    /// 从未检查过的已初始化仓库数
    /// </summary>
    public int NeverCheckedRepositories { get; set; }

    /// <summary>
    /// 需要关注的仓库（失败或从未检查）
    /// </summary>
    public List<RepositoryHealthSummary> AttentionItems { get; set; } = new();
}