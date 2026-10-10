using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using ResticManager.Application.Services.Abstractions;

namespace ResticManager.Application.Jobs
{
    public class BackupTaskJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackupTaskJob> _logger;

        public BackupTaskJob(IServiceProvider serviceProvider, ILogger<BackupTaskJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var taskId = context.JobDetail.JobDataMap.GetIntValue("taskId");
            _logger.LogInformation("Quartz 触发备份任务，ID：{TaskId}", taskId);

            // 获取取消令牌
            var cancellationToken = context.CancellationToken;

            using var scope = _serviceProvider.CreateScope();
            var taskService = scope.ServiceProvider.GetRequiredService<IBackupTaskService>();

            try
            {
                await taskService.RunTaskAsync(taskId);
                _logger.LogInformation("备份任务 {TaskId} 执行完成", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行备份任务 {TaskId} 失败", taskId);
                throw new JobExecutionException($"执行备份任务 {taskId} 出错", ex, false);
            }
        }
    }
}
