namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 定时维护任务的类型元数据：种子默认值、任务标题模板与提交参数的唯一事实来源。
/// 新增维护类型时在此追加一条，启动时会自动补插缺失的配置行，无需迁移。
/// </summary>
public static class MaintenanceDefinitions
{
    /// <param name="Type">AsyncTaskTypes 常量值</param>
    /// <param name="TitleTemplate">任务标题模板，{0} 为仓库名</param>
    /// <param name="Parameters">提交 AsyncTask 的参数（深度校验需 subset）</param>
    /// <param name="DefaultEnabled">种子默认开关：只读类默认启用，写操作类默认禁用</param>
    /// <param name="DefaultCron">种子默认执行计划，已避开 0 点备份高峰与 3 点内置健康检查</param>
    /// <param name="Order">展示顺序，种子按此顺序插入</param>
    public sealed record Definition(string Type, string TitleTemplate, object Parameters,
        bool DefaultEnabled, string DefaultCron, int Order);

    public static readonly Definition[] All =
    {
        new(AsyncTaskTypes.SnapshotSync, "[定时] 同步仓库 {0} 的快照", new { },
            DefaultEnabled: true, DefaultCron: "0 0 4 * * ?", Order: 1),
        new(AsyncTaskTypes.RepositoryStats, "[定时] 统计仓库 {0}", new { },
            DefaultEnabled: true, DefaultCron: "0 30 4 * * ?", Order: 2),
        new(AsyncTaskTypes.RepositoryPrune, "[定时] 清理仓库 {0} 未引用数据", new { },
            DefaultEnabled: false, DefaultCron: "0 0 2 ? * SUN", Order: 3),
        new(AsyncTaskTypes.RepositoryDeepCheck, "[定时] 深度校验仓库 {0}", new { subset = "5%" },
            DefaultEnabled: false, DefaultCron: "0 0 5 ? * SUN", Order: 4),
        new(AsyncTaskTypes.RepositoryRepairIndex, "[定时] 修复仓库 {0} 索引", new { },
            DefaultEnabled: false, DefaultCron: "0 30 3 ? * SUN", Order: 5),
        new(AsyncTaskTypes.RepositoryRepairSnapshots, "[定时] 修复仓库 {0} 损坏快照", new { },
            DefaultEnabled: false, DefaultCron: "0 0 3 1 * ?", Order: 6),
    };
}
