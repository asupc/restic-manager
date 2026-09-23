using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Application.Services.Abstractions
{
    public interface IBackupRecordService
    {
        Task<List<BackupRecord>> GetRecordsByTaskIdAsync(int taskId);
        Task<PagedResult<BackupRecord>> GetPagedRecordsByTaskIdAsync(int taskId, int page, int pageSize);
        Task<PagedResult<BackupRecord>> GetPagedRecordsAsync(int page, int pageSize, int? taskId = null, string? status = null, string? keyword = null);
        Task<BackupRecord> GetRecordByIdAsync(int id);
        Task<BackupRecord> CreateRecordAsync(BackupRecord record);
        Task<BackupRecord> UpdateRecordAsync(int id, BackupRecord updatedRecord);
        Task<bool> DeleteRecordAsync(int id);
        Task<BackupRecord> GetLatestRecordByTaskIdAsync(int taskId);
    }
}
