using ResticManager.Models;

namespace ResticManager.Application.Services.Abstractions;

public interface IBackupTaskService
{
    Task<List<BackupTask>> GetAllTasksAsync();
    Task<BackupTask> GetTaskByIdAsync(int id);
    Task<BackupTask> CreateTaskAsync(BackupTask task);
    Task UpdateTaskAsync(BackupTask task);
    Task DeleteTaskAsync(int id);
    /// <summary>
    /// 执行一次备份任务（阻塞直到备份+保留策略+快照缓存同步完成），
    /// 返回本次执行的 BackupRecord Id。手动触发经 AsyncTaskService 后台调用，
    /// Quartz 定时触发经 BackupTaskJob 直接调用，两条路径汇聚于此。
    /// </summary>
    Task<int> RunTaskAsync(int taskId);
    
    Task<List<BackupTask>> GetEnabledTasksAsync();
}