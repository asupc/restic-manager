using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using ResticManager.Application.Jobs;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Application.Services.Implements;
using ResticManager.Application.Utils;
using ResticManager.Data;
using ResticManager.Models;
using ResticManager.Sqlite;
using ResticManager.Web.Middleware;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

// 检查并生成 appsettings.json 文件
EnsureAppSettingsFile();

var builder = WebApplication.CreateBuilder(args);

// 设置配置文件路径
builder.Configuration.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "data"));
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var connectionString = builder.Configuration.GetConnectionString("SqliteConnection");
var databaseProvider = builder.Configuration.GetSection("Database")["DbType"] ?? "sqlite";
Console.WriteLine("DBType：" + databaseProvider);
// 添加基础的DbContextOptions配置
builder.Services.AddDbContext<ResticManagerDbContext>(options =>
{
    switch (databaseProvider.ToLower())
    {
        case "mysql":
            var mysqlConnectionString = builder.Configuration.GetConnectionString("MySqlConnection");
            var serverVersion = ServerVersion.AutoDetect(mysqlConnectionString);
            options.UseMySql(mysqlConnectionString, serverVersion);
            break;
        case "postgresql":
            options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSqlConnection"));
            break;
        default:
            options.UseSqlite(string.IsNullOrEmpty(connectionString) ? "ResticManager.db" : connectionString);
            break;
    }
});

// 根据数据库类型注册具体实现
switch (databaseProvider.ToLower())
{
    //case "mysql":
    //    builder.Services.AddScoped<ResticManagerDbContext, MySqlDbContext>();
    //    break;
    //case "postgresql":
    //    builder.Services.AddScoped<ResticManagerDbContext, PgSqlDbContext>();
    //    break;
    default:
        builder.Services.AddScoped<ResticManagerDbContext, SqliteDbContext>();
        break;
}

builder.Services.AddScoped<JwtUtils>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IResticService, ResticService>();
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<IBackupTaskService, BackupTaskService>();
builder.Services.AddScoped<IBackupRecordService, BackupRecordService>();
builder.Services.AddScoped<IBackupRuleService, BackupRuleService>();
builder.Services.AddScoped<IRepositoryHealthCheckService, RepositoryHealthCheckService>();
builder.Services.AddScoped<IMaintenanceSettingService, MaintenanceSettingService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// 后台异步任务服务（Singleton + HostedService）：
// 启动时先做遗留任务清理（标记失败），再启动 Quartz 调度器
builder.Services.AddSingleton<IAsyncTaskService, AsyncTaskService>();
builder.Services.AddHostedService(sp => (AsyncTaskService)sp.GetRequiredService<IAsyncTaskService>());

// 添加 Quartz 调度服务
builder.Services.AddHostedService<QuartzSchedulerService>();
builder.Services.AddSingleton<QuartzSchedulerService>();

// 添加对 Quartz Job 的依赖注入支持
builder.Services.AddTransient<BackupTaskJob>();
builder.Services.AddTransient<RepositoryHealthCheckJob>();
builder.Services.AddTransient<MaintenanceJob>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);
        // 添加自定义的时间转换器
        options.JsonSerializerOptions.Converters.Add(new UtcToLocalDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcToLocalNullableDateTimeConverter());
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);
    });

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy =>
        {
            // 允许前端开发服务器和生产环境自身访问
            var allowedOrigins = new List<string>
            {
                "http://localhost:8080",
                "http://localhost:5050",
                "https://localhost:5050"
            };

            // 允许通过配置覆盖 (appsettings.json 中 CORS:AllowedOrigins)
            var extraOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            if (extraOrigins != null) allowedOrigins.AddRange(extraOrigins);

            policy.WithOrigins(allowedOrigins.ToArray())
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 添加日志服务
builder.Services.AddLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowVueApp");

// 添加全局异常处理中间件
app.UseGlobalExceptionMiddleware();

app.UseAuthentication();
app.UseAuthorization();

// 添加对 SPA 的支持，解决刷新页面 404 的问题
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (!path.HasValue || path.Value.StartsWith("/api") || Path.HasExtension(path.Value))
    {
        await next();
        return;
    }

    // 保存原始路径，以便客户端路由可以处理它
    context.Request.Path = "/index.html";
    await next();
});

app.MapControllers();

// 确保数据库已创建 
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ResticManagerDbContext>();
    context.Database.Migrate();
}

app.UseStaticFiles();
app.UseDefaultFiles();
app.MapFallbackToFile("/index.html");

app.Run();

// 确保 appsettings.json 文件存在并有效
static void EnsureAppSettingsFile()
{
    var dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data");
    var appSettingsPath = Path.Combine(dataDirectory, "appsettings.json");

    // 确保 Data 目录存在
    if (!Directory.Exists(dataDirectory))
    {
        Directory.CreateDirectory(dataDirectory);
    }

    // 如果文件不存在，则创建默认配置文件
    if (!File.Exists(appSettingsPath))
    {
        CreateDefaultAppSettings(appSettingsPath);
        return;
    }

    // 尝试读取并验证配置文件
    try
    {
        var jsonContent = File.ReadAllText(appSettingsPath);
        var config = System.Text.Json.JsonDocument.Parse(jsonContent);

        // 检查 Jwt:Secret 是否存在且符合要求
        var jwtSecret = config.RootElement.GetProperty("Jwt").GetProperty("Secret").GetString();
        if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
        {
            // 重新生成 Jwt:Secret
            var updatedJson = UpdateJwtSecret(jsonContent);
            File.WriteAllText(appSettingsPath, updatedJson);
        }
    }
    catch (Exception)
    {
        // 如果读取或解析失败，则创建默认配置文件
        CreateDefaultAppSettings(appSettingsPath);
    }
}

/// <summary>
/// 生成默认的appsettings.json 文件
/// </summary>
static void CreateDefaultAppSettings(string appSettingsPath)
{
    string path = "restic/0.18.1/restic.exe";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        path = "restic/0.18.1/restic";
    }
    var defaultConfig = new
    {
        Database = new
        {
            DbType = "sqlite"
        },
        ConnectionStrings = new
        {
            SqliteConnection = "Data Source=./data/restic-manager.db",
            MySqlConnection = "server=localhost;port=3306;database=ResticManager;user=root;password=your_password",
            PostgreSqlConnection = "Host=localhost;Database=ResticManager;Username=postgres;Password=your_password"
        },
        Jwt = new
        {
            Secret = RandomHelper.GenerateSecureRandomString(32)
        },
        Restic = new
        {
            Path = path
        },
        Logging = new
        {
            LogLevel = new
            {
                Default = "Warning",
                Microsoft_AspNetCore = "Warning"
            }
        },
        AllowedHosts = "*"
    };

    var json = System.Text.Json.JsonSerializer.Serialize(defaultConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(appSettingsPath, json);
}

// 更新 Jwt:Secret
static string UpdateJwtSecret(string jsonContent)
{
    var config = System.Text.Json.JsonDocument.Parse(jsonContent);
    var jsonObject = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonContent);

    // 创建更新后的配置对象
    var updatedConfig = new Dictionary<string, object>();

    foreach (var property in jsonObject.EnumerateObject())
    {
        if (property.Name == "Jwt")
        {
            var jwtObject = new Dictionary<string, object>();
            foreach (var jwtProperty in property.Value.EnumerateObject())
            {
                if (jwtProperty.Name == "Secret")
                {
                    jwtObject["Secret"] = RandomHelper.GenerateSecureRandomString(32);
                }
                else
                {
                    jwtObject[jwtProperty.Name] = jwtProperty.Value.ToString();
                }
            }
            updatedConfig["Jwt"] = jwtObject;
        }
        else
        {
            updatedConfig[property.Name] = property.Value.ToString();
        }
    }

    return System.Text.Json.JsonSerializer.Serialize(updatedConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
}