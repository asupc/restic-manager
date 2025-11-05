using ResticManager.Models;

namespace ResticManager.Application.Services.Abstractions
{
    public interface IBackupRecordService
    {
        Task<List<BackupRecord>> GetRecordsByTaskIdAsync(int taskId);
        Task<BackupRecord> GetRecordByIdAsync(int id);
        Task<BackupRecord> CreateRecordAsync(BackupRecord record);
        Task<BackupRecord> UpdateRecordAsync(int id, BackupRecord updatedRecord);
        Task<bool> DeleteRecordAsync(int id);
        Task<BackupRecord> GetLatestRecordByTaskIdAsync(int taskId);
    }
}
