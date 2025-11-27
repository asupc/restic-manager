using ResticManager.Models.Dtos;
using System.Text.Json.Serialization;

namespace ResticManager.Models;

public class SnapshotInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("time")]
    public DateTime Time { get; set; }

    [JsonPropertyName("tree")]
    public string Tree { get; set; }

    [JsonPropertyName("paths")]
    public List<string> Paths { get; set; }

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; }

    /// <summary>
    /// Restic program version
    /// </summary>
    [JsonPropertyName("program_version")]
    public string ProgramVersion { get; set; }

    [JsonPropertyName("short_id")]
    public string ShortId { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("uid")]
    public int Uid { get; set; }

    [JsonPropertyName("gid")]
    public int Gid { get; set; }

    [JsonPropertyName("parent")]
    public string Parent { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; }

    [JsonPropertyName("summary")]
    public BackupSummary Summary { get; set; }
}