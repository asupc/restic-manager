using System.Collections.Concurrent;
using ResticManager.Application.Services.Abstractions;

namespace ResticManager.Application.Services.Implements;

/// <summary>
/// 每仓库一把信号量（条目不回收，仓库数量有限）。
/// 此前这把锁只保护 AsyncTaskService 的后台任务，而 Quartz 定时备份
/// （BackupTaskJob → RunTaskAsync）与定时健康检查（RepositoryHealthCheckJob →
/// restic check）直连服务方法、绕过了它，导致两者同时刻对同一仓库发起
/// restic 排它锁竞争（定时备份与健康检查默认 Cron 同为 03:00 时每天必撞），
/// 输掉的一方直接失败。抽为单例后所有入口共用，竞争在应用内排队化解。
/// </summary>
public sealed class RepositoryLockManager : IRepositoryLockManager
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public async Task<IAsyncDisposable> AcquireAsync(int repositoryId)
    {
        var semaphore = _locks.GetOrAdd(repositoryId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        return new Releaser(semaphore);
    }

    /// <summary>释放时归还信号量的句柄</summary>
    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
