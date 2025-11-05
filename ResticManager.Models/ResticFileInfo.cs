using System.Text.Json.Serialization;

namespace ResticManager.Models;

public class ResticFileInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } 
    
    [JsonPropertyName("type")]
    public string Type { get; set; } 
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
    
    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("paths")]
    public List<string> Paths { get; set; }

    [JsonPropertyName("uid")]
    public int Uid { get; set; } 
    
    [JsonPropertyName("gid")]
    public int Gid { get; set; } 
    
    [JsonPropertyName("mode")]
    public long Mode { get; set; } 
    
    [JsonPropertyName("mtime")]
    public string Mtime { get; set; }
    
    [JsonPropertyName("permissions")]
    public string Permissions { get; set; }
    
    [JsonPropertyName("atime")]
    public string Atime { get; set; }
    
    [JsonPropertyName("ctime")]
    public string Ctime { get; set; }
    
    [JsonPropertyName("message_type")]
    public string MessageType { get; set; }
    
    [JsonPropertyName("struct_type")]
    public string StructType { get; set; }
}