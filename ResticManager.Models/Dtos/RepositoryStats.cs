using System.Text.Json.Serialization;

namespace ResticManager.Models.Dtos;

/// <summary>
/// 仓库统计信息
/// </summary>
public class RepositoryStats
{
    /// <summary>
    /// 总大小（字节）
    /// </summary>
    [JsonPropertyName("total_size")]
    public long TotalSize { get; set; }

    /// <summary>
    /// 总文件数量
    /// </summary>
    [JsonPropertyName("total_file_count")]
    public int TotalFileCount { get; set; }

    /// <summary>
    /// 快照数量
    /// </summary>
    [JsonPropertyName("snapshots_count")]
    public int SnapshotsCount { get; set; }
}
