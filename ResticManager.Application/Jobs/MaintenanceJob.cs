using Microsoft.Extensions.DependencyInjection;
using Quartz;
using ResticManager.Application.Services.Abstractions;

namespace ResticManager.Application.Jobs;

/// <summary>
/// 定时维护 Job：按 MaintenanceDefinitions 中一种维护类型，对全部已初始化仓库
/// 批量登记后台任务后立即返回。实际执行由 AsyncTaskService 排队（全局并发闸 +
/// 每仓库锁 + 防重入），与页面手动入口走同一链路。由 QuartzSchedulerService 统一注册。
/// </summary>
public class MaintenanceJob : IJob
{
    private readonly IServiceProvider _serviceProvider;

    public MaintenanceJob(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var type = context.JobDetail.JobDataMap.GetString("type");
        var def = MaintenanceDefinitions.All.FirstOrDefault(d => d.Type == type);
        if (def == null)
        {
            throw new JobExecutionException($"未知的维护类型: {type}");
        }

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始定时维护：{def.Type}");
        var submitted = 0;
        var skipped = 0;

        using var scope = _serviceProvider.CreateScope();
        var repositoryService = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
        var asyncTaskService = scope.ServiceProvider.GetRequiredService<IAsyncTaskService>();

        try
        {
            var repositories = (await repositoryService.GetAllRepositoriesAsync())
                .Where(r => r.IsInitialized)
                .ToList();

            foreach (var repo in repositories)
            {
                try
                {
                    // 只登记任务立即返回；trigger 标记为 Quartz，前端任务列表可区分来源
                    await asyncTaskService.CreateTaskAsync(def.Type, repo.Id,
                        string.Format(def.TitleTemplate, repo.Name), def.Parameters, trigger: "Quartz");
                    submitted++;
                }
                catch (InvalidOperationException)
                {
                    // 该仓库已有同类任务在途（如手动触发），跳过继续下一个
                    skipped++;
                }
            }

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 定时维护 {def.Type} 登记完成：提交 {submitted} 个仓库，跳过 {skipped} 个");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 定时维护 {def.Type} 执行出错: {ex}");
            throw new JobExecutionException($"定时维护 {def.Type} 执行失败", ex, false);
        }
    }
}
