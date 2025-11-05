using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using ResticManager.Data;

namespace ResticManager.MySql;

public class MySqlDbContextFactory : IDesignTimeDbContextFactory<MySqlDbContext>
{
    public MySqlDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ResticManagerDbContext>();

        // 获取当前目录或指定目录加载配置
        var basePath = Directory.GetCurrentDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);

        var configuration = builder.Build();

        var connectionString = configuration.GetConnectionString("MySqlConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("未找到MySQL数据库连接字符串。");
        }
        //var serverVersion = ServerVersion.AutoDetect(connectionString);
        //optionsBuilder.UseMySql(connectionString);
        optionsBuilder.UseMySql(connectionString, serverVersion: ServerVersion.AutoDetect(connectionString));
        return new MySqlDbContext(optionsBuilder.Options);
    }
}