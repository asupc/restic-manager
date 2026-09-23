using System.Text.Json.Serialization;

namespace ResticManager.Models.Dtos;

/// <summary>
/// 备份快照 DTO，对齐前端 `SnapshotInfo` 消费的 JSON 结构，
/// 便于前端无感切换数据源（之前从远端实时拉，改为从本地 DB 读）。
/// </summary>
public class BackupSnapshotDto
{
    /// <summary>
    /// 本地表主键。
    /// </summary>
    [JsonPropertyName("localId")]
    public int LocalId { get; set; }

    /// <summary>
    /// 远端快照完整 ID。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 短 ID。
    /// </summary>
    [JsonPropertyName("short_id")]
    public string ShortId { get; set; } = string.Empty;

    /// <summary>
    /// 快照时间。
    /// </summary>
    [JsonPropertyName("time")]
    public DateTime Time { get; set; }

    /// <summary>
    /// tree blob。
    /// </summary>
    [JsonPropertyName("tree")]
    public string Tree { get; set; } = string.Empty;

    /// <summary>
    /// 备份路径列表。
    /// </summary>
    [JsonPropertyName("paths")]
    public List<string> Paths { get; set; } = new();

    /// <summary>
    /// 标签列表。
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 主机名。
    /// </summary>
    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// 用户名。
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Unix UID。
    /// </summary>
    [JsonPropertyName("uid")]
    public int? Uid { get; set; }

    /// <summary>
    /// Unix GID。
    /// </summary>
    [JsonPropertyName("gid")]
    public int? Gid { get; set; }

    /// <summary>
    /// 创建快照的 restic 版本。
    /// </summary>
    [JsonPropertyName("program_version")]
    public string ProgramVersion { get; set; } = string.Empty;

    /// <summary>
    /// 父快照 ID。
    /// </summary>
    [JsonPropertyName("parent")]
    public string Parent { get; set; } = string.Empty;

    /// <summary>
    /// 快照统计信息（文件数、总大小、耗时等），与之前远端
    /// `SnapshotInfo.summary` 字段结构一致，前端无感切换。
    /// </summary>
    [JsonPropertyName("summary")]
    public BackupSummary? Summary { get; set; }

    /// <summary>
    /// 关联的备份记录 ID（可空）。
    /// </summary>
    [JsonPropertyName("backup_record_id")]
    public int? BackupRecordId { get; set; }

    /// <summary>
    /// 最近一次同步时间。
    /// </summary>
    [JsonPropertyName("last_synced_at")]
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// 是否已在远端仓库被 forget。
    /// </summary>
    [JsonPropertyName("is_deleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>
/// 同步结果 DTO，供 `POST /api/repository/{id}/snapshots/sync` 返回。
/// </summary>
public class SyncSnapshotsResult
{
    /// <summary>本地新增数量。</summary>
    public int Added { get; set; }
    /// <summary>本地更新数量。</summary>
    public int Updated { get; set; }
    /// <summary>本地标记为已删除的数量。</summary>
    public int MarkedDeleted { get; set; }
    /// <summary>远端快照总数。</summary>
    public int RemoteTotal { get; set; }
}