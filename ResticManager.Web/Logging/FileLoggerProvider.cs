using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;

namespace ResticManager.Web.Logging;

/// <summary>
/// 文件日志 Provider：按天滚动写入 JSON Lines 格式日志（restic-manager-yyyyMMdd.log），
/// 供"系统日志"页面查询。写入失败绝不向业务代码抛异常。
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// 单条日志的 JSON 行结构
    /// </summary>
    private readonly record struct LogLine(string Ts, string Level, string Category, string Message, string? Exception);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // 宽松转义：中文与 +、& 等符号不转义，保证人工 tail 文件时可读
        // （日志只写入文件与 JSON API，不存在 HTML 内嵌场景）
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // 字段名统一 camelCase（与 SystemLogReader 的解析键一致）
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly FileLoggerOptions _options;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string _currentDate = string.Empty;

    // 写入连续失败后停写，冷却期后重试一次（避免每条日志都触发异常开销）
    private bool _disabled;
    private DateTime _disabledAt;

    public FileLoggerProvider(FileLoggerOptions options)
    {
        _options = options;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (_lock)
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch
            {
                // 关停阶段吞异常
            }
            _writer = null;
        }
    }

    /// <summary>
    /// 写入一条日志（由 FileLogger 调用，任何情况下不得抛出）
    /// </summary>
    internal void Write(string category, LogLevel level, string message, Exception? exception)
    {
        try
        {
            lock (_lock)
            {
                if (_disabled)
                {
                    // 冷却期（1 分钟）过后重试一次
                    if ((DateTime.Now - _disabledAt).TotalMinutes < 1)
                    {
                        return;
                    }
                    _disabled = false;
                }

                try
                {
                    EnsureWriter();

                    var line = new LogLine(
                        DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"),
                        level.ToString(),
                        category,
                        message,
                        exception?.ToString());

                    _writer!.WriteLine(JsonSerializer.Serialize(line, JsonOptions));
                    _writer.Flush();
                }
                catch
                {
                    // 写入失败：停写并清理句柄，冷却期后重试
                    _disabled = true;
                    _disabledAt = DateTime.Now;
                    try
                    {
                        _writer?.Dispose();
                    }
                    catch
                    {
                        // 忽略
                    }
                    _writer = null;
                    Console.Error.WriteLine($"[FileLogger] 写入日志文件失败，已暂停写入（目录: {_options.Directory}）");
                }
            }
        }
        catch
        {
            // 最外层兜底：日志组件绝不能影响业务
        }
    }

    /// <summary>
    /// 确保 Writer 打开且指向当天文件（调用方须持锁）
    /// </summary>
    private void EnsureWriter()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        if (_writer != null && _currentDate == today)
        {
            return;
        }

        _writer?.Dispose();
        System.IO.Directory.CreateDirectory(_options.Directory);
        var path = Path.Combine(_options.Directory, $"restic-manager-{today}.log");
        // FileShare.Read 允许查询端 / tail 并发读取
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
        _currentDate = today;

        // 跨天滚动时顺带清理过期文件
        CleanupExpired(today);
    }

    /// <summary>
    /// 删除超过保留期的日志文件（调用方须持锁；单个文件删除失败留待下次）
    /// </summary>
    private void CleanupExpired(string today)
    {
        try
        {
            if (!DateTime.TryParseExact(today, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var todayDate))
            {
                return;
            }
            var expireBefore = todayDate.AddDays(-_options.RetainDays).ToString("yyyyMMdd");

            foreach (var file in System.IO.Directory.EnumerateFiles(_options.Directory, "restic-manager-*.log"))
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var datePart = name.Substring(name.Length - 8);
                    if (datePart.CompareTo(expireBefore) < 0)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // 单个文件清理失败不影响其他文件
                }
            }
        }
        catch
        {
            // 清理失败不影响写入
        }
    }

    /// <summary>
    /// 文件日志记录器（过滤已由 LoggerFactory 完成，这里只负责写入）
    /// </summary>
    private sealed class FileLogger : ILogger
    {
        private static readonly IDisposable EmptyScope = new EmptyDisposable();

        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => EmptyScope;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter?.Invoke(state, exception) ?? state?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(message) && exception == null)
            {
                return;
            }
            _provider.Write(_category, logLevel, message, exception);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
