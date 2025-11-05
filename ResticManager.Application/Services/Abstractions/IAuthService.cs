using ResticManager.Models;

namespace ResticManager.Application.Services.Abstractions;

/// <summary>
/// 认证服务接口，提供用户注册和登录功能
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户注册
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="email">邮箱</param>
    /// <returns>创建的用户对象</returns>
    Task<User> RegisterAsync(string username, string password, string email);
    
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>JWT令牌</returns>
    Task<string> LoginAsync(string username, string password);

    /// <summary>
    /// 检查系统中是否已存在任何用户
    /// </summary>
    /// <returns>如果存在至少一个用户则返回 true，否则返回 false</returns>
    Task<bool> HasAnyUserAsync();
}