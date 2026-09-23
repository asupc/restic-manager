using System.Text.Json.Serialization;

namespace ResticManager.Models.Dtos;

/// <summary>
/// restic find --json 的单个快照命中结果
/// </summary>
public class FindResult
{
    /// <summary>
    /// 命中的快照 ID
    /// </summary>
    [JsonPropertyName("snapshot")]
    public string Snapshot { get; set; }

    /// <summary>
    /// 命中数量
    /// </summary>
    [JsonPropertyName("hits")]
    public int Hits { get; set; }

    /// <summary>
    /// 命中的文件列表
    /// </summary>
    [JsonPropertyName("matches")]
    public List<FindMatch> Matches { get; set; } = new();
}

/// <summary>
/// restic find 命中的单个文件
/// </summary>
public class FindMatch
{
    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("mtime")]
    public string Mtime { get; set; }

    [JsonPropertyName("permissions")]
    public string Permissions { get; set; }
}
