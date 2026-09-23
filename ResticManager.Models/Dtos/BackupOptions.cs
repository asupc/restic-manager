namespace ResticManager.Models.Dtos;

/// <summary>
/// 备份高级选项，对应 restic backup 的可选参数。全部字段可空/默认关闭，
/// 不传时行为与旧版一致。
/// </summary>
public class BackupOptions
{
    /// <summary>
    /// 为本次备份的快照打上的标签，对应 --tag
    /// </summary>
    public List<string> Tags { get; set; }

    /// <summary>
    /// 排除包含 CACHEDIR.TAG 的缓存目录，对应 --exclude-caches
    /// </summary>
    public bool ExcludeCaches { get; set; }

    /// <summary>
    /// 排除大于指定大小的文件，如 "100M"、"1G"，对应 --exclude-larger-than
    /// </summary>
    public string ExcludeLargerThan { get; set; }

    /// <summary>
    /// 不跨越文件系统边界，对应 --one-file-system
    /// </summary>
    public bool OneFileSystem { get; set; }

    /// <summary>
    /// 试运行，不实际写入数据，对应 --dry-run
    /// </summary>
    public bool DryRun { get; set; }
}
