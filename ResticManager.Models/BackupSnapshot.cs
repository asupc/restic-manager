using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using ResticManager.Models.Dtos;

namespace ResticManager.Models;

/// <summary>
/// 备份快照实体。把 restic 远端仓库的快照元数据持久化到本地数据库，
/// 避免"快照列表 / 版本记录"页面每次都向网盘仓库发起 `restic snapshots` 调用。
///
/// 注意：只缓存快照元数据，**不**缓存快照内的文件树（历史上 `SnapshotFiles`
/// 实体因实现复杂被移除，保持本实体轻量）。
/// </summary>
public class BackupSnapshot : BaseModel
{
    /// <summary>
    /// restic 远端快照 ID（64 字符）。与 <see cref="RepositoryId"/> 组成复合唯一索引。
    /// </summary>
    [Required]
    public string SnapshotId { get; set; } = string.Empty;

    /// <summary>
    /// restic 短 ID，便于 UI 展示。
    /// </summary>
    public string ShortId { get; set; } = string.Empty;

    /// <summary>
    /// 所属远端仓库（逻辑外键，关联 <see cref="Repository.Id"/>）。
    /// </summary>
    public int RepositoryId { get; set; }

    /// <summary>
    /// 所属远端仓库导航属性。
    /// </summary>
    public Repository Repository { get; set; }

    /// <summary>
    /// 关联到本地执行记录。可空——远端仓库中的快照不一定由本系统创建。
    /// </summary>
    public int? BackupRecordId { get; set; }

    /// <summary>
    /// 关联到本地执行记录的导航属性。
    /// </summary>
    public BackupRecord BackupRecord { get; set; }

    /// <summary>
    /// 快照时间（来自 restic JSON 的 `time` 字段）。
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// 快照对应的 tree blob ID。
    /// </summary>
    public string Tree { get; set; } = string.Empty;

    /// <summary>
    /// 创建该快照的主机名。
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// 创建该快照的用户名。
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Unix UID。
    /// </summary>
    public int? Uid { get; set; }

    /// <summary>
    /// Unix GID。
    /// </summary>
    public int? Gid { get; set; }

    /// <summary>
    /// 创建快照时使用的 restic 程序版本。
    /// </summary>
    public string ProgramVersion { get; set; } = string.Empty;

    /// <summary>
    /// 父快照 ID（增量备份链）。
    /// </summary>
    public string ParentSnapshotId { get; set; } = string.Empty;

    /// <summary>
    /// 备份路径列表，JSON 序列化存储（例如 ["D:\\projects"]）。
    /// </summary>
    public string PathsJson { get; set; } = "[]";

    /// <summary>
    /// 快照标签列表，JSON 序列化存储。
    /// </summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>
    /// 快照统计信息（restic JSON 的 `summary` 字段），JSON 序列化存储。
    /// 与 <see cref="BackupRecord.SummaryJson"/> 采用同一既有模式，
    /// 快照列表页的文件数/大小/耗时等指标依赖该数据。
    /// </summary>
    public string SummaryJson { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次从远端仓库同步该记录的时间（用于排查缓存新鲜度）。
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// 远端仓库已 forget 但本地尚未清理的快照，置为 true。
    /// 默认查询会过滤掉本字段为 true 的快照。
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 计算属性：备份路径列表。
    /// </summary>
    [NotMapped]
    public List<string> Paths
    {
        get
        {
            if (string.IsNullOrEmpty(PathsJson))
                return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(PathsJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
        set => PathsJson = JsonSerializer.Serialize(value ?? new List<string>());
    }

    /// <summary>
    /// 计算属性：标签列表。
    /// </summary>
    [NotMapped]
    public List<string> Tags
    {
        get
        {
            if (string.IsNullOrEmpty(TagsJson))
                return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(TagsJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
        set => TagsJson = JsonSerializer.Serialize(value ?? new List<string>());
    }

    /// <summary>
    /// 计算属性：快照统计信息（文件数、总大小、耗时等）。
    /// </summary>
    [NotMapped]
    public BackupSummary? Summary
    {
        get
        {
            if (string.IsNullOrEmpty(SummaryJson) || SummaryJson == "null")
                return null;
            try
            {
                return JsonSerializer.Deserialize<BackupSummary>(SummaryJson);
            }
            catch
            {
                return null;
            }
        }
        set => SummaryJson = value == null ? string.Empty : JsonSerializer.Serialize(value);
    }
}