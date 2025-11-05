using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Application.Utils;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Application.Services.Implements;

public class AuthService : IAuthService
{
    private readonly ResticManagerDbContext _context;
    private readonly JwtUtils _jwtUtils;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(ResticManagerDbContext context, JwtUtils jwtUtils, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _jwtUtils = jwtUtils;
        _passwordHasher = passwordHasher;
    }

    public async Task<User> RegisterAsync(string username, string password, string email)
    {
        // 检查是否已经存在用户，实现单用户模式
        if (await _context.Users.AnyAsync())
        {
            throw new Exception("系统已存在用户，无法注册更多用户");
        }

        var user = new User
        {
            Username = username,
            Email = email
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<string> LoginAsync(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return _jwtUtils.GenerateToken(user);
    }
    
    // 检查是否存在用户
    public async Task<bool> HasAnyUserAsync()
    {
        return await _context.Users.AnyAsync();
    }
}