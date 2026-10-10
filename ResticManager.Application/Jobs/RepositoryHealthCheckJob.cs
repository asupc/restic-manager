using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<RepositoryHealthCheckJob> _logger;

    public RepositoryHealthCheckJob(IServiceProvider serviceProvider, ILogger<RepositoryHealthCheckJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("开始执行全量仓库健康检查");
        var cancellationToken = context.CancellationToken;

        using var scope = _serviceProvider.CreateScope();
        var healthService = scope.ServiceProvider.GetRequiredService<IRepositoryHealthCheckService>();

        try
        {
            var records = await healthService.CheckAllRepositoriesAsync("Scheduled");
            var passed = records.Count(r => r.Status == "Passed");
            var failed = records.Count(r => r.Status == "Failed");
            _logger.LogInformation("仓库健康检查完成：共 {Total}，通过 {Passed}，失败 {Failed}", records.Count, passed, failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仓库健康检查执行失败");
            throw new JobExecutionException("仓库健康检查执行失败", ex, false);
        }
    }
}