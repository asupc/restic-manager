using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using ResticManager.Data;

namespace ResticManager.PgSql;

public class PgSqlDbContextFactory : IDesignTimeDbContextFactory<PgSqlDbContext>
{
    public PgSqlDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ResticManagerDbContext>();
        
        // 获取当前目录或指定目录加载配置
        var basePath = Directory.GetCurrentDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("data/appsettings.json", optional: true, reloadOnChange: true);

        var configuration = builder.Build();

        var connectionString = configuration.GetConnectionString("PostgreSqlConnection") 
            ?? "Host=localhost;Database=ResticManager;Username=postgres;Password=password";
        
        optionsBuilder.UseNpgsql(connectionString);

        return new PgSqlDbContext(optionsBuilder.Options);
    }
}