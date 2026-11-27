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

        // 仅在内存中保留当前页所需的最新 Top-K 条（最小堆），避免全量加载历史日志导致高内存占用
        var targetCount = (long)page * pageSize;
        var maxKeep = targetCount > int.MaxValue ? int.MaxValue : (int)targetCount;
        var topEntries = new PriorityQueue<(DateTimeOffset sortKey, long seq, SystemLogEntryDto entry), (DateTimeOffset sortKey, long seq)>();
        var total = 0;
        long seq = 0;

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

                        if (root.ValueKind != JsonValueKind.Object ||
                            !root.TryGetProperty("ts", out var tsEl) || tsEl.ValueKind != JsonValueKind.String ||
                            !root.TryGetProperty("level", out var levelEl) || levelEl.ValueKind != JsonValueKind.String ||
                            !root.TryGetProperty("category", out var categoryEl) || categoryEl.ValueKind != JsonValueKind.String ||
                            !root.TryGetProperty("message", out var messageEl) || messageEl.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        string? exceptionText = null;
                        if (root.TryGetProperty("exception", out var exEl))
                        {
                            if (exEl.ValueKind == JsonValueKind.String)
                            {
                                exceptionText = exEl.GetString();
                            }
                            else if (exEl.ValueKind != JsonValueKind.Null)
                            {
                                exceptionText = exEl.GetRawText();
                            }
                        }

                        var timestamp = tsEl.GetString() ?? string.Empty;
                        var levelText = levelEl.GetString() ?? string.Empty;
                        var category = categoryEl.GetString() ?? string.Empty;
                        var message = messageEl.GetString() ?? string.Empty;

                        // 级别过滤：该级别及以上
                        if (minLevel.HasValue &&
                            (!Enum.TryParse<LogLevel>(levelText, true, out var entryLevel) || entryLevel < minLevel.Value))
                        {
                            continue;
                        }

                        // 关键词过滤（消息/类别/异常，忽略大小写）
                        if (!string.IsNullOrEmpty(keyword) &&
                            message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 &&
                            category.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 &&
                            (exceptionText?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                        {
                            continue;
                        }

                        if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sortKey))
                        {
                            continue;
                        }

                        total++;
                        var currentSeq = ++seq;
                        var priority = (sortKey, currentSeq);

                        if (topEntries.Count < maxKeep)
                        {
                            var entry = new SystemLogEntryDto
                            {
                                Timestamp = timestamp,
                                Level = levelText,
                                Category = category,
                                Message = message,
                                Exception = exceptionText
                            };
                            topEntries.Enqueue((sortKey, currentSeq, entry), priority);
                        }
                        else if (topEntries.Count > 0)
                        {
                            var oldest = topEntries.Peek();
                            if (priority.CompareTo((oldest.sortKey, oldest.seq)) > 0)
                            {
                                var entry = new SystemLogEntryDto
                                {
                                    Timestamp = timestamp,
                                    Level = levelText,
                                    Category = category,
                                    Message = message,
                                    Exception = exceptionText
                                };
                                topEntries.DequeueEnqueue((sortKey, currentSeq, entry), priority);
                            }
                        }
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                    {
                        // 写入方写了一半的行或字段类型异常，跳过单行不影响整文件读取
                    }
                }
            }
            catch
            {
                // 单个文件读取失败跳过，不影响其他文件
            }
        }

        result.Total = total;
        var skip = (long)(page - 1) * pageSize;
        if (skip >= total || topEntries.Count == 0)
        {
            result.Items = new List<SystemLogEntryDto>();
            return result;
        }

        // 时间倒序（新 → 旧）
        var kept = new List<(DateTimeOffset sortKey, long seq, SystemLogEntryDto entry)>(topEntries.Count);
        while (topEntries.Count > 0)
        {
            kept.Add(topEntries.Dequeue());
        }

        result.Items = kept
            .OrderByDescending(e => e.sortKey)
            .ThenByDescending(e => e.seq)
            .Skip((int)skip)
            .Take(pageSize)
            .Select(e => e.entry)
            .ToList();
        return result;
    }

    /// <summary>
    /// 从文件名解析日期（restic-manager-yyyyMMdd.log）
    /// </summary>
    private static (string path, DateTime? date) ExtractFileDate(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        const string prefix = "restic-manager-";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length != prefix.Length + 8)
        {
            return (path, null);
        }

        var datePart = name.Substring(prefix.Length, 8);
        return DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? (path, date)
            : (path, null);
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }
}
