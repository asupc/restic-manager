using Microsoft.Extensions.DependencyInjection;
using Quartz;
using ResticManager.Application.Services.Abstractions;

namespace ResticManager.Application.Jobs
{
    public class BackupTaskJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;

        public BackupTaskJob(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var taskId = context.JobDetail.JobDataMap.GetIntValue("taskId");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始执行备份任务 ID: {taskId}");

            // 获取取消令牌
            var cancellationToken = context.CancellationToken;

            using var scope = _serviceProvider.CreateScope();
            var taskService = scope.ServiceProvider.GetRequiredService<IBackupTaskService>();

            try
            {
                await taskService.RunTaskAsync(taskId);
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 备份任务 ID: {taskId} 执行完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 执行备份任务 {taskId} 出错: {ex}");
                throw new JobExecutionException($"执行备份任务 {taskId} 出错", ex, false);
            }
        }
    }
}