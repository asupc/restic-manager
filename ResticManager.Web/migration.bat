@echo off
:: 获取当前日期时间
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
:: 提取日期时间部分
set year=%dt:~0,4%
set month=%dt:~4,2%
set day=%dt:~6,2%
set hour=%dt:~8,2%
set minute=%dt:~10,2%

:: 创建默认迁移名称(格式: yyyyMMddHHmm)
set defaultMigrationName=%year%%month%%day%%hour%%minute%
echo defaultMigrationName: %defaultMigrationName%
:: dotnet ef migrations add %defaultMigrationName% --project ../ResticManager.MySql --startup-project ./ --context MySqlDbContext --output-dir Migrations
dotnet ef migrations add %defaultMigrationName% --project ../ResticManager.PgSql --startup-project ./ --context PgSqlDbContext --output-dir Migrations
dotnet ef migrations add %defaultMigrationName% --project ../ResticManager.Sqlite --startup-project ./ --context SqliteDbContext --output-dir Migrations

pause