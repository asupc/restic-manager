using System.Text.Json.Serialization;

namespace ResticManager.Models.Dtos;

/// <summary>
/// 表示快照中文件的树形节点结构
/// </summary>
public class ResticFileTreeNode
{
    /// <summary>
    /// 节点名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型 (file 或 dir)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（仅对文件有效）
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// 完整路径
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("uid")]
    public int Uid { get; set; }

    /// <summary>
    /// 组ID
    /// </summary>
    [JsonPropertyName("gid")]
    public int Gid { get; set; }

    /// <summary>
    /// 权限模式
    /// </summary>
    [JsonPropertyName("mode")]
    public long Mode { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    [JsonPropertyName("mtime")]
    public string Mtime { get; set; } = string.Empty;

    /// <summary>
    /// 权限字符串
    /// </summary>
    [JsonPropertyName("permissions")]
    public string Permissions { get; set; } = string.Empty;

    /// <summary>
    /// 访问时间
    /// </summary>
    [JsonPropertyName("atime")]
    public string Atime { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("ctime")]
    public string Ctime { get; set; } = string.Empty;

    /// <summary>
    /// 子节点列表（仅对目录有效）
    /// </summary>
    [JsonPropertyName("children")]
    public List<ResticFileTreeNode> Children { get; set; } = new();

    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("message_type")]
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// 结构类型
    /// </summary>
    [JsonPropertyName("struct_type")]
    public string StructType { get; set; } = string.Empty;

    /// <summary>
    /// 是否为叶子节点（没有子节点的目录或者文件）
    /// </summary>
    public bool IsLeaf { get; set; } = true;

    [JsonPropertyName("hasChildren")]
    public bool HasChildren
    {
        get
        {
            return Type == "dir" || Children.Count > 0;
        }
    }
}