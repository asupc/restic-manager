using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResticManager.Application.Services.Abstractions;

namespace ResticManager.Web.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var user = await _authService.RegisterAsync(request.Username, request.Password, request.Email);
            return Ok(new
            {
                message = "注册成功"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var token = await _authService.LoginAsync(request.Username, request.Password);
        if (token == null)
            return Unauthorized(new { message = "无效的用户名或密码" });

        return Ok(new { token });
    }

    [HttpGet("has-user")]
    [AllowAnonymous]
    public async Task<IActionResult> HasAnyUser()
    {
        var hasUser = await _authService.HasAnyUserAsync();
        return Ok(new { hasUser });
    }
}

public class RegisterRequest
{
    [Required(ErrorMessage = "用户名不能为空")]
    [MinLength(3, ErrorMessage = "用户名长度不能少于3个字符")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码长度不能少于6个字符")]
    public string Password { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required(ErrorMessage = "用户名不能为空")]
    [MinLength(1, ErrorMessage = "用户名不能为空")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(1, ErrorMessage = "密码不能为空")]
    public string Password { get; set; } = string.Empty;
}