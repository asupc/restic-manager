using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResticManager.Models.Dtos;
using ResticManager.Web.Logging;

namespace ResticManager.Web.Controllers;

/// <summary>
/// 系统日志：查询文件日志（data/logs/），供"系统日志"页面分页展示
/// </summary>
[ApiController]
[Route("api/systemlogs")]
[Authorize]
public class SystemLogsController : ControllerBase
{
    private readonly SystemLogReader _logReader;

    public SystemLogsController(SystemLogReader logReader)
    {
        _logReader = logReader;
    }

    /// <summary>
    /// 分页查询系统日志（按时间倒序）
    /// </summary>
    /// <param name="page">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小（1-200）</param>
    /// <param name="level">最低日志级别（返回该级别及以上）</param>
    /// <param name="keyword">关键词（匹配消息/类别/异常）</param>
    /// <param name="startDate">起始日期（yyyy-MM-dd，含当天）</param>
    /// <param name="endDate">结束日期（yyyy-MM-dd，含当天）</param>
    [HttpGet("paged")]
    public ActionResult<PagedResult<SystemLogEntryDto>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? level = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var result = _logReader.GetPaged(page, pageSize, level, keyword, startDate, endDate);
        return Ok(result);
    }
}
