using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 仓库健康检查服务接口
/// </summary>
public interface IRepositoryHealthCheckService
{
    /// <summary>
    /// 对单个仓库执行 restic check，并把结果写入数据库
    /// </summary>
    /// <param name="repositoryId">仓库 ID</param>
    /// <param name="trigger">触发方式：Scheduled / Manual</param>
    /// <returns>本次检查记录</returns>
    Task<RepositoryCheckRecord> CheckRepositoryAsync(int repositoryId, string trigger = "Manual");

    /// <summary>
    /// 对所有已初始化的仓库执行检查（用于 Quartz 定时任务）
    /// </summary>
    /// <returns>本次执行产生的检查记录列表</returns>
    Task<List<RepositoryCheckRecord>> CheckAllRepositoriesAsync(string trigger = "Scheduled");

    /// <summary>
    /// 获取某仓库最近的健康检查记录
    /// </summary>
    Task<List<RepositoryCheckRecord>> GetRecordsByRepositoryAsync(int repositoryId, int limit = 20);

    /// <summary>
    /// 获取某仓库最近一条检查记录
    /// </summary>
    Task<RepositoryCheckRecord?> GetLatestByRepositoryAsync(int repositoryId);

    /// <summary>
    /// 获取全部仓库健康概览（给首页用）
    /// </summary>
    Task<RepositoryHealthOverview> GetOverviewAsync();
}