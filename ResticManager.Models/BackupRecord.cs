using ResticManager.Models.Dtos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ResticManager.Models;

/// <summary>
/// 备份记录实体类，表示一次备份任务的执行记录
/// </summary>
public class BackupRecord : BaseModel
{
    /// <summary>
    /// 逻辑外键：关联BackupTask的Id（无数据库约束）
    /// </summary>
    public int BackupTaskId { get; set; }

    /// <summary>
    /// 备份开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 备份结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 备份状态，必须填写，可选值：Pending, Running, Succeeded, Failed
    /// </summary>
    [Required]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// 处理的文件数量
    /// </summary>
    public long FilesProcessed { get; set; }

    /// <summary>
    /// 传输的数据大小（字节）
    /// </summary>
    public long DataTransferred { get; set; }

    /// <summary>
    /// 备份输出信息
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// 新增字段，用于存储详细的备份摘要信息（以JSON字符串形式存储）
    /// </summary>
    public string SummaryJson { get; set; } = string.Empty;

    /// <summary>
    /// 计算属性，用于获取反序列化的摘要信息
    /// </summary>
    [NotMapped]
    public BackupSummary Summary
    {
        get
        {
            if (string.IsNullOrEmpty(SummaryJson))
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
        set
        {
            if (value == null)
            {
                SummaryJson = string.Empty;
            }
            else
            {
                SummaryJson = JsonSerializer.Serialize(value);
            }
        }
    }

    /// <summary>
    /// 导航属性，关联的备份任务
    /// </summary>
    public BackupTask Task { get; set; }
}
