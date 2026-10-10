using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ResticManager.Models.Dtos;

namespace ResticManager.Web.Logging;

/// <summary>
/// 系统日志读取器：解析 data/logs/ 下的 JSON Lines 日志文件，提供过滤、排序与分页
/// </summary>
public class SystemLogReader
{
    private readonly FileLoggerOptions _options;

    public SystemLogReader(FileLoggerOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// 分页查询日志（按时间倒序）
    /// </summary>
    /// <param name="page">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小（1-200）</param>
    /// <param name="level">最低日志级别（返回该级别及以上）</param>
    /// <param name="keyword">关键词（匹配消息/类别/异常，忽略大小写）</param>
    /// <param name="startDate">起始日期（yyyy-MM-dd，含当天）</param>
    /// <param name="endDate">结束日期（yyyy-MM-dd，含当天）</param>
    public PagedResult<SystemLogEntryDto> GetPaged(int page, int pageSize, string? level, string? keyword, string? startDate, string? endDate)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        LogLevel? minLevel = null;
        if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<LogLevel>(level, true, out var parsedLevel))
        {
            minLevel = parsedLevel;
        }

        var start = ParseDate(startDate);
        var end = ParseDate(endDate);

        var result = new PagedResult<SystemLogEntryDto> { Page = page, PageSize = pageSize };

        if (!System.IO.Directory.Exists(_options.Directory))
        {
            return result;
        }

        // 文件名日期降序（新 → 旧），并按日期范围剪枝整文件
        var files = System.IO.Directory.EnumerateFiles(_options.Directory, "restic-manager-*.log")
            .Select(ExtractFileDate)
            .Where(f => f.date.HasValue)
            .Where(f => (start == null || f.date >= start) && (end == null || f.date <= end))
            .OrderByDescending(f => f.date!.Value)
            .Select(f => f.path)
            .ToList();

        var entries = new List<(DateTimeOffset sortKey, SystemLogEntryDto entry)>();

        foreach (var file in files)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;

                        if (!root.TryGetProperty("ts", out var tsEl) ||
                            !root.TryGetProperty("level", out var levelEl) ||
                            !root.TryGetProperty("category", out var categoryEl) ||
                            !root.TryGetProperty("message", out var messageEl))
                        {
                            continue;
                        }

                        var entry = new SystemLogEntryDto
                        {
                            Timestamp = tsEl.GetString() ?? string.Empty,
                            Level = levelEl.GetString() ?? string.Empty,
                            Category = categoryEl.GetString() ?? string.Empty,
                            Message = messageEl.GetString() ?? string.Empty,
                            Exception = root.TryGetProperty("exception", out var exEl) ? exEl.GetString() : null
                        };

                        // 级别过滤：该级别及以上
                        if (minLevel.HasValue &&
                            (!Enum.TryParse<LogLevel>(entry.Level, true, out var entryLevel) || entryLevel < minLevel.Value))
                        {
                            continue;
                        }

                        // 关键词过滤（消息/类别/异常，忽略大小写）
                        if (!string.IsNullOrEmpty(keyword) &&
                            entry.Message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 &&
                            entry.Category.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 &&
                            (entry.Exception?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                        {
                            continue;
                        }

                        if (!DateTimeOffset.TryParse(entry.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sortKey))
                        {
                            continue;
                        }

                        entries.Add((sortKey, entry));
                    }
                    catch (JsonException)
                    {
                        // 写入方写了一半的行，跳过（预期情况）
                    }
                }
            }
            catch
            {
                // 单个文件读取失败跳过，不影响其他文件
            }
        }

        // 时间倒序（新 → 旧）
        var ordered = entries.OrderByDescending(e => e.sortKey).Select(e => e.entry).ToList();
        result.Total = ordered.Count;
        result.Items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return result;
    }

    /// <summary>
    /// 从文件名解析日期（restic-manager-yyyyMMdd.log）
    /// </summary>
    private static (string path, DateTime? date) ExtractFileDate(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var datePart = name.Length >= 8 ? name.Substring(name.Length - 8) : string.Empty;
        return DateTime.TryParseExact(datePart, "yyyyMMdd", null, DateTimeStyles.None, out var date)
            ? (path, date)
            : (path, null);
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return DateTime.TryParseExact(value, "yyyy-MM-dd", null, DateTimeStyles.None, out var date) ? date : null;
    }
}
