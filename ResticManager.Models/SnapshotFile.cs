using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ResticManager.Models;

/// <summary>
/// 快照文件实体类，表示快照中包含的文件信息
/// </summary>
public class SnapshotFile : BaseModel
{
    /// <summary>
    /// 仓库ID
    /// </summary>
    public int RepositoryId { get; set; }

    /// <summary>
    /// 快照ID
    /// </summary>
    [Required]
    public string SnapshotId { get; set; } = string.Empty;

    /// <summary>
    /// 文件路径
    /// </summary>
    [Required]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 文件名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 文件类型 (file/directory)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 文件权限
    /// </summary>
    public string Permissions { get; set; } = string.Empty;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public string Mtime { get; set; } = string.Empty;

    /// <summary>
    /// 父级路径
    /// </summary>
    public string ParentPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否为叶子节点（没有子节点的目录或者文件）
    /// </summary>
    public bool IsLeaf { get; set; } = true;
}