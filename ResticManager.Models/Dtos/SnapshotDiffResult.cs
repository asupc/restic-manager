using System.Text.Json.Serialization;

namespace ResticManager.Models.Dtos
{
    /// <summary>
    /// 快照差异结果
    /// </summary>
    public class SnapshotDiffResult
    {
        /// <summary>
        /// 变更列表
        /// </summary>
        public List<SnapshotDiffChange> Changes { get; set; } = new List<SnapshotDiffChange>();

        /// <summary>
        /// 统计信息
        /// </summary>
        public SnapshotDiffStatistics Statistics { get; set; }
    }

    /// <summary>
    /// 快照差异变更项
    /// </summary>
    public class SnapshotDiffChange
    {
        /// <summary>
        /// 消息类型
        /// </summary>
        [JsonPropertyName("message_type")]
        public string MessageType { get; set; }

        /// <summary>
        /// 文件路径
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>
        /// 变更修饰符 (+新增, -删除, M修改等)
        /// </summary>
        [JsonPropertyName("modifier")]
        public string Modifier { get; set; }
    }

    /// <summary>
    /// 快照差异统计信息
    /// </summary>
    public class SnapshotDiffStatistics
    {
        /// <summary>
        /// 消息类型
        /// </summary>
        [JsonPropertyName("message_type")]
        public string MessageType { get; set; }

        /// <summary>
        /// 源快照ID
        /// </summary>
        [JsonPropertyName("source_snapshot")]
        public string SourceSnapshot { get; set; }

        /// <summary>
        /// 目标快照ID
        /// </summary>
        [JsonPropertyName("target_snapshot")]
        public string TargetSnapshot { get; set; }

        /// <summary>
        /// 变更文件数
        /// </summary>
        [JsonPropertyName("changed_files")]
        public int ChangedFiles { get; set; }

        /// <summary>
        /// 新增项统计
        /// </summary>
        [JsonPropertyName("added")]
        public SnapshotDiffAddRemoveStats Added { get; set; }

        /// <summary>
        /// 删除项统计
        /// </summary>
        [JsonPropertyName("removed")]
        public SnapshotDiffAddRemoveStats Removed { get; set; }
    }

    /// <summary>
    /// 快照差异新增/删除统计信息
    /// </summary>
    public class SnapshotDiffAddRemoveStats
    {
        /// <summary>
        /// 文件数
        /// </summary>
        [JsonPropertyName("files")]
        public int Files { get; set; }

        /// <summary>
        /// 目录数
        /// </summary>
        [JsonPropertyName("dirs")]
        public int Dirs { get; set; }

        /// <summary>
        /// 其他项数
        /// </summary>
        [JsonPropertyName("others")]
        public int Others { get; set; }

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
        /// 字节数
        /// </summary>
        [JsonPropertyName("bytes")]
        public long Bytes { get; set; }
    }
}