using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using ResticManager.Data;

namespace ResticManager.Sqlite;

public class SqliteDbContextFactory : IDesignTimeDbContextFactory<SqliteDbContext>
{
    public SqliteDbContext CreateDbContext(string[] args)
    {
        // 初始化SQLitePCL提供程序
        SQLitePCL.Batteries.Init();
        
        var optionsBuilder = new DbContextOptionsBuilder<ResticManagerDbContext>();
        
        // 获取当前目录或指定目录加载配置
        var basePath = Directory.GetCurrentDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
            
        var configuration = builder.Build();

        var connectionString = configuration.GetConnectionString("SqliteConnection") 
            ?? "Data Source=ResticManager.db";
        
        optionsBuilder.UseSqlite(connectionString);

        return new SqliteDbContext(optionsBuilder.Options);
    }
}