using ResticManager.Models;

namespace ResticManager.Application.Services.Abstractions;

public interface IBackupTaskService
{
    Task<List<BackupTask>> GetAllTasksAsync();
    Task<BackupTask> GetTaskByIdAsync(int id);
    Task<BackupTask> CreateTaskAsync(BackupTask task);
    Task UpdateTaskAsync(BackupTask task);
    Task DeleteTaskAsync(int id);
    Task RunTaskAsync(int taskId);
    
    Task<List<BackupTask>> GetEnabledTasksAsync();
}