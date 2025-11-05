using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Application.Services.Implements;

public class BackupTaskService : IBackupTaskService
{
    private readonly ResticManagerDbContext _context;
    private readonly IResticService _resticService;
    private readonly IBackupRecordService _recordService;

    public BackupTaskService(ResticManagerDbContext context,
        IResticService resticService,
        IBackupRecordService recordService)
    {
        _context = context;
        _resticService = resticService;
        _recordService = recordService;
    }

    public async Task<List<BackupTask>> GetAllTasksAsync()
    {
        return await _context.BackupTasks
            .Include(t => t.Repository)
            .Include(t => t.Rule)
            .ToListAsync();
    }

    public async Task<BackupTask> GetTaskByIdAsync(int id)
    {
        return await _context.BackupTasks
            .Include(t => t.Repository)
            .Include(t => t.Rule)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<BackupTask> CreateTaskAsync(BackupTask task)
    {
        _context.BackupTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task UpdateTaskAsync(BackupTask task)
    {
        var t = await _context.BackupTasks.FindAsync(task.Id);
        t.Name = task.Name;
        t.Description = task.Description;
        t.SourcePath = task.SourcePath;
        t.RepositoryId = task.RepositoryId;
        t.IsEnabled = task.IsEnabled;
        t.CronExpression = task.CronExpression;
        t.BackupRuleId = task.BackupRuleId;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteTaskAsync(int id)
    {
        var task = await _context.BackupTasks.FindAsync(id);
        if (task != null)
        {
            _context.BackupTasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RunTaskAsync(int taskId)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RunTaskAsync 开始执行任务 ID: {taskId}");
        
        var task = await _context.BackupTasks
            .Include(t => t.Repository)
            .Include(t => t.Rule)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 未找到");
            throw new Exception("任务未找到");
        }

        // 检查任务是否已经在运行
        if (task.LastRunStatus == "Running") {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 正在运行中，无法再次启动");
            throw new Exception("任务正在运行中，无法再次启动");
        }

        if (!task.Repository.IsInitialized) {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 关联的仓库未初始化");
            throw new Exception("关联的仓库未初始化");
        }
        
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始执行备份任务：{task.Name}");
        
        // 更新任务状态为运行中
        task.LastRunAt = DateTime.Now;
        task.LastRunStatus = "Running";
        _context.Entry(task).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        // 创建备份记录
        var record = new BackupRecord
        {
            BackupTaskId = task.Id,
            StartTime = DateTime.Now,
            Status = "Running"
        };

        _context.BackupRecords.Add(record);
        await _context.SaveChangesAsync();

        try
        {
            // 执行备份
            string includePatterns = null;
            string excludePatterns = null;

            if (task.Rule != null)
            {
                includePatterns = task.Rule.IncludePatterns;
                excludePatterns = task.Rule.ExcludePatterns;
            }

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调用 ResticService 执行备份");
            var result = await _resticService.CreateBackupAsync(
                task.SourcePath,
                task.Repository.Path,
                task.Repository.Password,
                includePatterns,
                excludePatterns);

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 备份执行完成");

            // 更新备份记录
            record.EndTime = DateTime.Now;
            record.Status = "Succeeded";
            record.FilesProcessed = result.Summary?.TotalFilesProcessed ?? 0;
            record.DataTransferred = result.Summary?.TotalBytesProcessed ?? 0;
            record.Summary = result.Summary;
            record.Output = result.RawOutput;

            await _context.SaveChangesAsync();

            // 更新任务状态
            task.LastRunAt = DateTime.Now;
            task.LastRunStatus = "Succeeded";
            _context.Entry(task).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 执行成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 ID: {taskId} 执行失败: {ex}");
            
            // 更新备份记录错误信息
            record.EndTime = DateTime.Now;
            record.Status = "Failed";
            record.Error = ex.Message;

            await _context.SaveChangesAsync();

            // 更新任务状态
            task.LastRunAt = DateTime.Now;
            task.LastRunStatus = "Failed";
            _context.Entry(task).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            throw;
        }
    }

    public async Task<List<BackupTask>> GetEnabledTasksAsync()
    {
        return await _context.BackupTasks
            .Where(t => t.IsEnabled)
            .Include(t => t.Rule)
            .Include(t => t.Repository)
            .ToListAsync();
    }
}