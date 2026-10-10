using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Application.Services.Implements;

public class BackupRecordService : IBackupRecordService
{
    private readonly ResticManagerDbContext _context;
    private readonly ILogger<BackupRecordService> _logger;
    //private readonly IBackupTaskService _taskService; // 用于校验任务存在性

    public BackupRecordService(ResticManagerDbContext context, ILogger<BackupRecordService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取指定任务的所有备份记录
    /// </summary>
    public async Task<List<BackupRecord>> GetRecordsByTaskIdAsync(int taskId)
    {
        return await _context.BackupRecords
            .Where(r => r.BackupTaskId == taskId)
            .OrderByDescending(r => r.StartTime) // 按时间倒序（最新的在前）
            .ToListAsync();
    }

    /// <summary>
    /// 分页获取指定任务的备份记录
    /// </summary>
    public async Task<PagedResult<BackupRecord>> GetPagedRecordsByTaskIdAsync(int taskId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.BackupRecords
            .Where(r => r.BackupTaskId == taskId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<BackupRecord>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 多条件分页获取备份记录
    /// </summary>
    public async Task<PagedResult<BackupRecord>> GetPagedRecordsAsync(int page, int pageSize, int? taskId = null, string? status = null, string? keyword = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.BackupRecords
            .Include(r => r.Task)
            .AsQueryable();

        if (taskId.HasValue && taskId.Value > 0)
        {
            query = query.Where(r => r.BackupTaskId == taskId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(r => (r.Task != null && r.Task.Name.Contains(keyword)) || r.Error.Contains(keyword));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<BackupRecord>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 获取单个备份记录
    /// </summary>
    public async Task<BackupRecord> GetRecordByIdAsync(int id)
    {
        return await _context.BackupRecords.FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>
    /// 创建备份记录（自动校验关联的任务是否存在）
    /// </summary>
    public async Task<BackupRecord> CreateRecordAsync(BackupRecord record)
    {
        // 补充默认值
        record.StartTime = record.StartTime == default ? DateTime.Now : record.StartTime;
        record.Status = string.IsNullOrEmpty(record.Status) ? "Running" : record.Status;
        _context.BackupRecords.Add(record);
        await _context.SaveChangesAsync();
        _logger.LogInformation("备份记录已创建：{RecordId}（任务 {TaskId}）", record.Id, record.BackupTaskId);
        return record;
    }

    /// <summary>
    /// 更新备份记录（若修改任务关联，需校验新任务是否存在）
    /// </summary>
    public async Task<BackupRecord> UpdateRecordAsync(int id, BackupRecord updatedRecord)
    {
        var existingRecord = await GetRecordByIdAsync(id);
        if (existingRecord == null)
            return null;
        // 更新字段
        existingRecord.Status = updatedRecord.Status;
        existingRecord.EndTime = updatedRecord.EndTime;
        existingRecord.FilesProcessed = updatedRecord.FilesProcessed;
        existingRecord.DataTransferred = updatedRecord.DataTransferred;
        existingRecord.Output = updatedRecord.Output;
        existingRecord.BackupTaskId = updatedRecord.BackupTaskId; // 允许修改关联的任务
        await _context.SaveChangesAsync();
        _logger.LogInformation("备份记录已更新：{RecordId}", existingRecord.Id);
        return existingRecord;
    }

    /// <summary>
    /// 删除备份记录
    /// </summary>
    public async Task<bool> DeleteRecordAsync(int id)
    {
        var record = await GetRecordByIdAsync(id);
        if (record == null)
            return false;

        _context.BackupRecords.Remove(record);
        await _context.SaveChangesAsync();
        _logger.LogInformation("备份记录已删除：{RecordId}", id);
        return true;
    }

    /// <summary>
    /// 获取任务的最新备份记录
    /// </summary>
    public async Task<BackupRecord> GetLatestRecordByTaskIdAsync(int taskId)
    {
        return await _context.BackupRecords
            .Where(r => r.BackupTaskId == taskId)
            .OrderByDescending(r => r.StartTime)
            .FirstOrDefaultAsync();
    }
}
