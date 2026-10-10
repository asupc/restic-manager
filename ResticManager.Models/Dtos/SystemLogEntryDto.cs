namespace ResticManager.Models.Dtos;

/// <summary>
/// 系统日志条目（来源于 data/logs/ 下的 JSON Lines 日志文件）
/// </summary>
public class SystemLogEntryDto
{
    /// <summary>
    /// 日志时间（本地时间，带时区偏移的原始字符串，如 2026-10-10T14:03:05.123+08:00）
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 日志级别（Trace/Debug/Information/Warning/Error/Critical）
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// 日志类别（记录器名称，通常为类全名）
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 异常信息（含堆栈，无异常时为 null）
    /// </summary>
    public string? Exception { get; set; }
}
