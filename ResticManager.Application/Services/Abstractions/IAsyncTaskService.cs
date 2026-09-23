using ResticManager.Models;

namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 后台异步任务服务：仓库类慢操作的登记、防重入、后台执行与状态查询。
/// HTTP 请求通过 CreateTaskAsync 立即拿到任务 ID 后返回，实际执行在后台进行。
/// </summary>
public interface IAsyncTaskService
{
    /// <summary>
    /// 创建并立即在后台启动一个异步任务，返回任务 ID。
    /// 同 (type, repositoryId, targetKey) 已存在 Pending/Running 任务时抛
    /// InvalidOperationException（RepositoryUnlock 类型除外，它不做防重入拒绝）。
    /// </summary>
    /// <param name="type">任务类型，取值见 AsyncTaskTypes</param>
    /// <param name="repositoryId">关联仓库 ID（可空）</param>
    /// <param name="title">人类可读任务名，通知直接展示</param>
    /// <param name="parameters">请求参数对象，序列化为 ParametersJson</param>
    /// <param name="targetKey">防重入键：BackupRun=备份任务Id；快照类=快照Id；仓库级=""</param>
    /// <param name="trigger">触发方式：Manual / Quartz</param>
    /// <returns>任务 ID</returns>
    Task<int> CreateTaskAsync(string type, int? repositoryId, string title, object parameters, string targetKey = "", string trigger = "Manual");

    /// <summary>
    /// 按 ID 获取任务
    /// </summary>
    Task<AsyncTask?> GetTaskAsync(int id);

    /// <summary>
    /// 获取全部 Pending/Running 任务（侧边栏角标、页面恢复轮询用）
    /// </summary>
    Task<List<AsyncTask>> GetActiveTasksAsync();

    /// <summary>
    /// 获取最近完成的任务（EndTime 在 withinMinutes 分钟内），全局完成通知用
    /// </summary>
    Task<List<AsyncTask>> GetRecentlyCompletedAsync(int withinMinutes = 2);
}
