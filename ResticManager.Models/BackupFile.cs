namespace ResticManager.Models;

/// <summary>
/// 备份文件实体类，表示备份任务中包含的文件信息
/// </summary>
public class BackupFile : BaseModel
{

    /// <summary>
    /// 逻辑外键：关联BackupTask的Id
    /// </summary>
    public int BackupTaskId { get; set; }

    /// <summary>
    /// restic快照ID
    /// </summary>
    public string SnapshotId { get; set; } = string.Empty;

    /// <summary>
    /// 快照创建时间
    /// </summary>
    public DateTime SnapshotTime { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 文件最后修改时间
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// 导航属性，关联的备份任务
    /// </summary>
    public BackupTask Task { get; set; }
}