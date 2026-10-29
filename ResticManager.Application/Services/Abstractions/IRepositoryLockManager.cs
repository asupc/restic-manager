namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 每仓库互斥锁：串行化应用内对同一 restic 仓库的所有访问，防止 restic 后端锁冲突。
/// AsyncTaskService 的后台任务、Quartz 定时备份（BackupTaskService.RunTaskAsync）与
/// 定时/手动健康检查（RepositoryHealthCheckService）共用同一把锁。
/// </summary>
public interface IRepositoryLockManager
{
    /// <summary>
    /// 获取指定仓库的互斥锁。返回的句柄 Dispose 时归还信号量，
    /// 建议 <c>await using</c> 持有。等待期间调用方挂起，不占用线程。
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(int repositoryId);
}
