using Microsoft.Extensions.Configuration;

namespace ResticManager.Web.Logging;

/// <summary>
/// 文件日志配置（appsettings.json 的 Logging:File 节）
/// </summary>
public class FileLoggerOptions
{
    /// <summary>
    /// 是否启用文件日志（默认 true）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 日志目录（绝对路径；解析前相对当前工作目录，默认 data/logs）
    /// </summary>
    public string Directory { get; set; } = "data/logs";

    /// <summary>
    /// 日志文件保留天数（默认 15）
    /// </summary>
    public int RetainDays { get; set; } = 15;

    /// <summary>
    /// 从配置解析选项；旧配置文件缺少 Logging:File 节时使用代码默认值兜底
    /// </summary>
    public static FileLoggerOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Logging:File");

        var options = new FileLoggerOptions();

        if (bool.TryParse(section["Enabled"], out var enabled))
        {
            options.Enabled = enabled;
        }

        var directory = section["Directory"];
        if (!string.IsNullOrWhiteSpace(directory))
        {
            options.Directory = directory;
        }

        if (int.TryParse(section["RetainDays"], out var retainDays) && retainDays > 0)
        {
            options.RetainDays = retainDays;
        }

        // 相对路径基于当前工作目录（与 data/appsettings.json、Sqlite 连接串的约定一致）
        options.Directory = Path.GetFullPath(options.Directory);
        return options;
    }
}
