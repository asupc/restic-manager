using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<QuartzSchedulerService> _logger;
    private IScheduler _scheduler;

    public QuartzSchedulerService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<QuartzSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
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
        _logger.LogInformation("正在启动 Quartz 调度器");
        await InitScheduler();
        // 启动调度器
        await _scheduler.Start(cancellationToken);
        _logger.LogInformation("Quartz 调度器启动完成");

        // 注册任务和触发器
        await ScheduleAllTasks(cancellationToken);
        // 注册系统级健康检查任务
        await ScheduleHealthCheckJob(cancellationToken);
        // 注册定时维护任务（按设置页配置）
        await ScheduleMaintenanceSettings(cancellationToken);
        _logger.LogInformation("所有任务调度完成");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止 Quartz 调度器");

        if (_scheduler != null)
        {
            await _scheduler.Shutdown(cancellationToken);
        }
        _logger.LogInformation("Quartz 调度器已停止");
    }

    private async Task ScheduleAllTasks(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var taskService = scope.ServiceProvider.GetRequiredService<IBackupTaskService>();
            var tasks = await taskService.GetEnabledTasksAsync();

            _logger.LogInformation("开始调度 {Count} 个启用的备份任务", tasks.Count);

            foreach (var task in tasks)
            {
                if (!string.IsNullOrEmpty(task.CronExpression))
                {
                    await ScheduleTask(task, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("任务 {TaskName} 未配置 Cron 表达式，跳过调度", task.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调度备份任务时发生错误");
        }
    }

    public async Task ScheduleTask(BackupTask task, CancellationToken cancellationToken)
    {
        await InitScheduler();
        _logger.LogInformation("调度备份任务 {TaskName}，Cron：{Cron}", task.Name, task.CronExpression);

        // 验证 Cron 表达式是否有效
        if (!CronExpression.IsValidExpression(task.CronExpression))
        {
            _logger.LogWarning("无效的 Cron 表达式：{Cron}（任务 {TaskName}）", task.CronExpression, task.Name);
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
        _logger.LogInformation("任务 {TaskName} 已调度，Cron：{Cron}，时区：{TimeZone}", task.Name, task.CronExpression, TimeZoneInfo.Local.DisplayName);

        // 验证下一次执行时间
        var nextFireTime = trigger.GetNextFireTimeUtc();
        if (nextFireTime.HasValue)
        {
            _logger.LogInformation("任务 {TaskName} 下一次执行时间：{NextFireTime}", task.Name, nextFireTime.Value.ToLocalTime());
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
            _logger.LogInformation("备份任务 {TaskId} 的调度已移除", taskId);
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

    /// <summary>
    /// 注册系统级仓库健康检查 Job。默认每天凌晨 3 点执行，
    /// 可通过 appsettings.json 的 "HealthCheck:Cron" 覆盖。
    /// </summary>
    private async Task ScheduleHealthCheckJob(CancellationToken cancellationToken)
    {
        await InitScheduler();

        var cron = _configuration["HealthCheck:Cron"] ?? "0 0 3 * * ?";
        if (!CronExpression.IsValidExpression(cron))
        {
            _logger.LogWarning("健康检查 Cron 表达式无效：{Cron}，使用默认值", cron);
            cron = "0 0 3 * * ?";
        }

        var jobKey = new JobKey(RepositoryHealthCheckJob.JobKeyName);
        var jobDetail = JobBuilder.Create<RepositoryHealthCheckJob>()
            .WithIdentity(jobKey)
            .Build();

        var triggerKey = new TriggerKey($"{RepositoryHealthCheckJob.JobKeyName}_Trigger");
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithCronSchedule(cron, x => x.InTimeZone(TimeZoneInfo.Local))
            .Build();

        if (await _scheduler.CheckExists(jobKey, cancellationToken))
        {
            await _scheduler.DeleteJob(jobKey, cancellationToken);
        }
        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);

        var nextFireTime = trigger.GetNextFireTimeUtc();
        _logger.LogInformation("仓库健康检查 Job 已注册，Cron：{Cron}，下次执行：{NextFireTime}", cron, nextFireTime?.ToLocalTime());
    }

    /// <summary>
    /// 启动时初始化维护配置种子并注册所有启用的定时维护 Job。
    /// </summary>
    private async Task ScheduleMaintenanceSettings(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<IMaintenanceSettingService>();
            await settingService.EnsureSeededAsync();

            var settings = await settingService.GetAllAsync();
            foreach (var setting in settings.Where(s => s.IsEnabled))
            {
                await ScheduleMaintenanceSetting(setting, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调度定时维护任务时发生错误");
        }
    }

    /// <summary>
    /// 注册单个维护类型的 Job，JobKey/TriggerKey 以类型区分。
    /// </summary>
    private async Task ScheduleMaintenanceSetting(MaintenanceSetting setting, CancellationToken cancellationToken)
    {
        await InitScheduler();

        if (!CronExpression.IsValidExpression(setting.CronExpression))
        {
            _logger.LogWarning("维护任务 {Type} 的 Cron 表达式无效：{Cron}，跳过调度", setting.Type, setting.CronExpression);
            return;
        }

        var jobKey = new JobKey($"MaintenanceJob_{setting.Type}");
        var jobDetail = JobBuilder.Create<MaintenanceJob>()
            .WithIdentity(jobKey)
            .UsingJobData("type", setting.Type)
            .Build();

        var triggerKey = new TriggerKey($"MaintenanceTrigger_{setting.Type}");
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithCronSchedule(setting.CronExpression, x => x.InTimeZone(TimeZoneInfo.Local))
            .Build();

        if (await _scheduler.CheckExists(jobKey, cancellationToken))
        {
            await _scheduler.DeleteJob(jobKey, cancellationToken);
        }
        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);

        var nextFireTime = trigger.GetNextFireTimeUtc();
        _logger.LogInformation("定时维护 {Type} 已注册，Cron：{Cron}，下次执行：{NextFireTime}", setting.Type, setting.CronExpression, nextFireTime?.ToLocalTime());
    }

    /// <summary>
    /// 设置更新后重调度单个维护类型（先移除旧调度，启用且表达式有效时重新注册）。
    /// </summary>
    public async Task RescheduleMaintenanceSetting(MaintenanceSetting setting, CancellationToken cancellationToken)
    {
        await InitScheduler();

        var jobKey = new JobKey($"MaintenanceJob_{setting.Type}");
        if (await _scheduler.CheckExists(jobKey, cancellationToken))
        {
            await _scheduler.DeleteJob(jobKey, cancellationToken);
        }

        if (setting.IsEnabled && CronExpression.IsValidExpression(setting.CronExpression))
        {
            await ScheduleMaintenanceSetting(setting, cancellationToken);
        }
    }

    /// <summary>
    /// 查询某个维护类型下一次触发时间（UTC）；未启用或未调度返回 null。
    /// </summary>
    public async Task<DateTime?> GetNextFireTimeAsync(string type, CancellationToken cancellationToken)
    {
        await InitScheduler();

        var trigger = await _scheduler.GetTrigger(new TriggerKey($"MaintenanceTrigger_{type}"), cancellationToken);
        return trigger?.GetNextFireTimeUtc()?.UtcDateTime;
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