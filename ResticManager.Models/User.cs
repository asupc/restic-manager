using System.ComponentModel.DataAnnotations;

namespace ResticManager.Models;

/// <summary>
/// 用户实体类，表示系统用户信息
/// </summary>
public class User : BaseModel
{
    /// <summary>
    /// 用户名，必须填写
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希值，必须填写
    /// </summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 用户邮箱
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 用户创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}