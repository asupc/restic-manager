using Microsoft.Extensions.DependencyInjection;
using Quartz;
using ResticManager.Application.Services.Abstractions;

namespace ResticManager.Application.Jobs;

/// <summary>
/// 定时遍历所有已初始化仓库执行 restic check 的 Quartz Job。
/// 由 QuartzSchedulerService 统一注册。
/// </summary>
public class RepositoryHealthCheckJob : IJob
{
    public const string JobKeyName = "RepositoryHealthCheckJob";

    private readonly IServiceProvider _serviceProvider;

    public RepositoryHealthCheckJob(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始执行全量仓库健康检查");
        var cancellationToken = context.CancellationToken;

        using var scope = _serviceProvider.CreateScope();
        var healthService = scope.ServiceProvider.GetRequiredService<IRepositoryHealthCheckService>();

        try
        {
            var records = await healthService.CheckAllRepositoriesAsync("Scheduled");
            var passed = records.Count(r => r.Status == "Passed");
            var failed = records.Count(r => r.Status == "Failed");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 仓库健康检查完成：{records.Count} 个仓库，通过 {passed}，失败 {failed}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 仓库健康检查执行出错: {ex}");
            throw new JobExecutionException("仓库健康检查执行失败", ex, false);
        }
    }
}