namespace ResticManager.Models;

/// <summary>
/// 备份结果类，用于封装restic备份操作的结果
/// </summary>
public class BackupResult : BaseModel
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 原始输出内容
    /// </summary>
    public string RawOutput { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// 备份摘要信息
    /// </summary>
    public BackupSummary Summary { get; set; }
}