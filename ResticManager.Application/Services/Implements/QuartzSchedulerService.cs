using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using ResticManager.Application.Jobs;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Models;
using System.Collections.Specialized;

namespace ResticManager.Application.Services.Implements;

public class QuartzSchedulerService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private IScheduler _scheduler;

    public QuartzSchedulerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private async Task InitScheduler()
    {
        // 只有当调度器尚未初始化时才创建
        if (_scheduler != null)
            return;

        // 创建调度器工厂和调度器
        NameValueCollection props = new()
        {
            {"quartz.serializer.type", "binary"}
        };
        StdSchedulerFactory factory = new(props);

        _scheduler = await factory.GetScheduler();
        
        // 设置自定义的 JobFactory 以支持依赖注入
        _scheduler.JobFactory = new ServiceJobFactory(_serviceProvider);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始启动 Quartz 调度器");
        await InitScheduler();
        // 启动调度器
        await _scheduler.Start(cancellationToken);
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Quartz 调度器启动完成");

        // 注册任务和触发器
        await ScheduleAllTasks(cancellationToken);
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 所有任务调度完成");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始停止 Quartz 调度器");

        if (_scheduler != null)
        {
            await _scheduler.Shutdown(cancellationToken);
        }
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Quartz 调度器已停止");
    }

    private async Task ScheduleAllTasks(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var taskService = scope.ServiceProvider.GetRequiredService<IBackupTaskService>();
            var tasks = await taskService.GetEnabledTasksAsync();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始调度 {tasks.Count} 个启用的任务");

            foreach (var task in tasks)
            {
                if (!string.IsNullOrEmpty(task.CronExpression))
                {
                    await ScheduleTask(task, cancellationToken);
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 {task.Name} 没有 Cron 表达式，跳过调度");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调度任务时发生错误: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 异常详情: {ex}");
        }
    }

    public async Task ScheduleTask(BackupTask task, CancellationToken cancellationToken)
    {
        await InitScheduler();
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ScheduleTask：{task.Name}，Cron：{task.CronExpression}");
        
        // 验证 Cron 表达式是否有效
        if (!CronExpression.IsValidExpression(task.CronExpression))
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 无效的 Cron 表达式: {task.CronExpression}");
            return;
        }
        
        // 创建任务
        var jobKey = new JobKey($"BackupTaskJob_{task.Id}");
        var jobDetail = JobBuilder.Create<BackupTaskJob>()
            .WithIdentity(jobKey)
            .UsingJobData("taskId", task.Id)
            .Build();

        // 创建触发器并设置时区为本地时区
        var triggerKey = new TriggerKey($"BackupTaskTrigger_{task.Id}");
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithCronSchedule(task.CronExpression, x => x.InTimeZone(TimeZoneInfo.Local))
            .Build();

        // 调度任务
        if (await _scheduler.CheckExists(jobKey, cancellationToken))
        {
            await _scheduler.DeleteJob(jobKey, cancellationToken);
        }

        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 {task.Name} 已调度，Cron表达式: {task.CronExpression}，时区: {TimeZoneInfo.Local.DisplayName}");
        
        // 验证下一次执行时间
        var nextFireTime = trigger.GetNextFireTimeUtc();
        if (nextFireTime.HasValue)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 {task.Name} 下一次执行时间: {nextFireTime.Value.ToLocalTime()}");
        }
    }

    public async Task RemoveTask(int taskId, CancellationToken cancellationToken)
    {
        if (_scheduler == null)
            await InitScheduler();

        var jobKey = new JobKey($"BackupTaskJob_{taskId}");
        if (await _scheduler.CheckExists(jobKey, cancellationToken))
        {
            await _scheduler.DeleteJob(jobKey, cancellationToken);
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 任务 {taskId} 已移除");
        }
    }

    public async Task RescheduleTask(BackupTask task, CancellationToken cancellationToken)
    {
        // 先移除旧的调度
        await RemoveTask(task.Id, cancellationToken);

        // 如果任务启用且有Cron表达式，则重新调度
        if (task.IsEnabled && !string.IsNullOrEmpty(task.CronExpression))
        {
            await ScheduleTask(task, cancellationToken);
        }
    }
}

public class ServiceJobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceJobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return (IJob)_serviceProvider.GetRequiredService(bundle.JobDetail.JobType);
    }

    public void ReturnJob(IJob job)
    {
        // 不需要特殊处理
    }
}