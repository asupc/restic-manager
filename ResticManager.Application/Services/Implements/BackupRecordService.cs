using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Application.Services.Implements;

public class BackupRecordService : IBackupRecordService
{
    private readonly ResticManagerDbContext _context;
    //private readonly IBackupTaskService _taskService; // 用于校验任务存在性

    public BackupRecordService(ResticManagerDbContext context)
    {
        _context = context;
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
