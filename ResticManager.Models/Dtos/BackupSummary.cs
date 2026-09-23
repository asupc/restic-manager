using System.Text.Json.Serialization;

namespace ResticManager.Models.Dtos;

/// <summary>
/// 用于存储restic备份输出的摘要信息
/// </summary>
public class BackupSummary : BaseModel
{
    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("message_type")]
    public string MessageType { get; set; } = string.Empty;
    
    /// <summary>
    /// 新增文件数
    /// </summary>
    [JsonPropertyName("files_new")]
    public int FilesNew { get; set; }
    
    /// <summary>
    /// 修改文件数
    /// </summary>
    [JsonPropertyName("files_changed")]
    public int FilesChanged { get; set; }
    
    /// <summary>
    /// 未修改文件数
    /// </summary>
    [JsonPropertyName("files_unmodified")]
    public int FilesUnmodified { get; set; }
    
    /// <summary>
    /// 新增目录数
    /// </summary>
    [JsonPropertyName("dirs_new")]
    public int DirsNew { get; set; }
    
    /// <summary>
    /// 修改目录数
    /// </summary>
    [JsonPropertyName("dirs_changed")]
    public int DirsChanged { get; set; }
    
    /// <summary>
    /// 未修改目录数
    /// </summary>
    [JsonPropertyName("dirs_unmodified")]
    public int DirsUnmodified { get; set; }
    
    /// <summary>
    /// 数据块数
    /// </summary>
    [JsonPropertyName("data_blobs")]
    public int DataBlobs { get; set; }
    
    /// <summary>
    /// 树块数
    /// </summary>
    [JsonPropertyName("tree_blobs")]
    public int TreeBlobs { get; set; }
    
    /// <summary>
    /// 新增数据大小（字节）
    /// </summary>
    [JsonPropertyName("data_added")]
    public long DataAdded { get; set; }
    
    /// <summary>
    /// 总处理文件数
    /// </summary>
    [JsonPropertyName("total_files_processed")]
    public int TotalFilesProcessed { get; set; }
    
    /// <summary>
    /// 总处理字节数
    /// </summary>
    [JsonPropertyName("total_bytes_processed")]
    public long TotalBytesProcessed { get; set; }
    
    /// <summary>
    /// 总耗时（秒）
    /// </summary>
    [JsonPropertyName("total_duration")]
    public double TotalDuration { get; set; }

    /// <summary>
    /// 本次备份生成的快照 ID。restic `backup --json` 末尾 summary 行
    /// 自带该字段，之前由于类缺少此字段被反序列化时丢弃，现在补回。
    /// </summary>
    [JsonPropertyName("snapshot_id")]
    public string SnapshotId { get; set; } = string.Empty;


    [JsonPropertyName("backup_start")]
    public DateTime? BackupStart { get; set; }

    [JsonPropertyName("backup_end")]
    public DateTime? BackupEnd { get; set; }
}