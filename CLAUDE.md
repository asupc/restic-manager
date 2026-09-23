# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概览

ResticManager 是基于 .NET（`net10.0`）后端 + Vue 2 前端的 **restic 备份图形化管理工具**。后端通过 `Process` 包装调用 restic CLI（仓库内 `ResticManager.Web/restic/0.18.1/` 自带 Windows / Linux 二进制），使用 EF Core 持久化元数据，Quartz.NET 调度定时备份任务，JWT 处理认证，Swagger 提供 API 文档。

> 注：README 中写的是 ".NET 10 SDK"，但 `ResticManager.Web.csproj` 中 `TargetFramework` 是 `net10.0` 且 Dockerfile 使用 `mcr.microsoft.com/dotnet/aspnet:10`。请按 csproj/Dockerfile 为准。

## 解决方案结构

```
ResticManager.sln
├── ResticManager.Web          # ASP.NET Core 入口：Controllers、Program.cs、Middleware、Dockerfile、前端目录、restic 二进制
├── ResticManager.Application  # 业务服务 + Quartz Jobs + Utils（JwtUtils、ResticHelper、DateTimeConverter…）
├── ResticManager.Data         # 抽象 DbContext (ResticManagerDbContext)
├── ResticManager.Models       # 实体 (User/Repository/BackupTask/BackupRule/BackupRecord) + DTO
├── ResticManager.Sqlite       # SqliteDbContext + Migrations（默认数据库）
├── ResticManager.PgSql        # PgSqlDbContext + Migrations
└── ResticManager.MySql        # MySqlDbContext（存在但 Program.cs 中未启用，需手动切换）
```

依赖方向：`Web → Application → Data / Models`，数据库提供程序在 `Program.cs` 中按 `Database:DbType` 动态选择。

## 常用命令

### 后端（.NET）

在仓库根目录执行：

| 操作 | 命令 |
| --- | --- |
| 还原依赖 | `dotnet restore ResticManager.sln` |
| 调试运行后端 | `dotnet run --project ResticManager.Web`（默认 https://localhost:5000） |
| 编译整个解决方案 | `dotnet build ResticManager.sln -c Debug` |
| 发布（用于 Docker 镜像） | `dotnet publish ./ResticManager.Web/ResticManager.Web.csproj -c release -o restic-manager-release` |
| 添加 EF 迁移（Sqlite） | `dotnet ef migrations add <Name> --project ../ResticManager.Sqlite --startup-project ./ --context SqliteDbContext --output-dir Migrations` |
| 添加 EF 迁移（PostgreSQL） | `dotnet ef migrations add <Name> --project ../ResticManager.PgSql --startup-project ./ --context PgSqlDbContext --output-dir Migrations` |
| 一次性生成两个数据库的迁移 | 双击 `ResticManager.Web/migration.bat`（会自动用时间戳命名） |

> 项目目前**没有单元测试项目**，新增测试需要先在解决方案中新建测试工程。

### 前端（Vue 2 + Element UI）

前端位于 `ResticManager.Web/restic-manager-ui`：

| 操作 | 命令 |
| --- | --- |
| 安装依赖 | `npm install`（在该目录下执行） |
| 开发服务器（带 API 代理 → `http://localhost:5050`） | `npm run serve`（启动后访问 http://localhost:8080） |
| 生产构建 | `npm run build`（产物输出到 `../../restic-manager-release/wwwroot`，由后端静态文件中间件托管） |
| ESLint 检查 | `npm run lint` |

构建好的前端资源会被 `.NET 发布流程` 一并打包；修改前端后必须重新 `npm run build`，否则发布包里的页面不会更新。

### Docker 一键发布

Windows 下执行根目录的 `publish-build-docker.bat`：清空 `restic-manager-release` → `dotnet publish` → `npm run build` → `docker build` → `docker save` 输出 `restic-manager.tar`。

## 关键架构细节

### 启动配置（`ResticManager.Web/Program.cs`）

- 启动时 `EnsureAppSettingsFile()` 会确保 `data/appsettings.json` 存在；如不存在或 `Jwt:Secret` 长度 < 32，会自动生成新默认配置（使用 `RandomHelper.GenerateSecureRandomString(32)`）。
- 数据库连接由 `Database:DbType` 决定（`sqlite` / `mysql` / `postgresql`，默认 `sqlite`）。**实际注入到 DI 的 DbContext 实现当前只启用了 `SqliteDbContext`**（`mysql`、`postgresql` 的注册代码被注释）。
- 启动顺序：`UseGlobalExceptionMiddleware` → `UseAuthentication` → `UseAuthorization` → 自定义 SPA fallback 中间件（任何非 `/api`、无扩展名的路径都重写为 `/index.html`，避免 Vue Router 刷新 404）→ `UseStaticFiles` + `MapFallbackToFile("/index.html")`。
- 启动末尾调用 `context.Database.Migrate()` 自动应用迁移（不要手动删表）。

### 备份任务执行链

1. `BackupTasksController` 接收 HTTP 请求 → `BackupTaskService` 操作数据库。
2. 创建/更新/删除任务时，`QuartzSchedulerService.ScheduleTask / RescheduleTask / RemoveTask` 维护 Quartz Job。
3. **手动"运行"**：`POST /backuptasks/{id}/run` 登记 `BackupRun` 后台任务立即返回 `{ taskId }`（见下节）；Quartz 定时触发仍走 `BackupTaskJob.Execute` → `IBackupTaskService.RunTaskAsync(taskId)`（同步执行，不经任务队列）。两条路径汇聚于同一个 `RunTaskAsync`（返回 BackupRecord Id）。
4. `BackupTaskService.RunTaskAsync` 调用 `IResticService.CreateBackupAsync`，后者通过 `Process` 执行 `restic backup --json`（路径/密码通过 `StandardInput` 传入，避免出现在命令行中）。
5. 解析 JSON 输出得到 `BackupSummary`，结果写入 `BackupRecord`。

### 后台异步任务系统（AsyncTask）

**所有访问 restic 仓库的接口都是后台异步任务**（`RepositoryController` 的仓库类 Action、手动运行备份）：HTTP 请求只做校验并登记任务，立即返回 `Ok(new { taskId, message })`，restic 命令由 `AsyncTaskService` 在后台执行。前端不等待接口返回，通过轮询 `GET /api/asynctasks/{id}` 获取状态与结果（`resultJson`）。为网盘仓库设计，避免前端 axios 60 秒超时假失败。

- 核心服务：`AsyncTaskService`（Singleton + IHostedService，只注入 `IServiceScopeFactory`）。类型常量在 `AsyncTaskTypes`。新增任务类型：加常量 → `ExecuteAsync` 加 switch 分支 → Controller 加 `SubmitRepositoryTaskAsync` 提交。
- 并发控制：全局 `SemaphoreSlim(3)` + 每仓库一把信号量串行化（防 restic 锁冲突）；`RepositoryUnlock` 不取仓库锁、不防重入（解除死锁的工具）。
- 防重入：同 `(Type, RepositoryId, TargetKey)` 已有 Pending/Running 任务时抛 `InvalidOperationException`（Controller 转 400）；`BackupRun` 额外预检 `BackupTasks.LastRunStatus=="Running"`。
- 启动清理（`StartAsync`，注册在 Quartz 之前）：遗留 Pending/Running 任务标 Failed("应用重启导致任务中断")，并复位 `BackupTasks.LastRunStatus` / `BackupRecords.Status` 的 Running 状态位；顺带删除 1 天前的临时类（文件树/搜索/diff）已完成任务行。
- 前端配套：`api.js` 支持 `{ loading: false }` 跳过全局全屏 loading；`utils/task-poller.js` 的 `pollTask`；Vuex `tasks` module 双频率轮询（活动 2s/空闲 30s）驱动侧边栏角标与跨页面 Notification；页面 mounted 时按 `(repositoryId, type)` 恢复在途任务轮询。

### Restic CLI 封装（`ResticService.cs`）

- 所有 restic 命令通过 `ExecuteResticCommandAsync` / `ExecuteResticCommandWithLineOutputAsync` 启动外部进程，UTF-8 输入输出。
- 密码通过 `StandardInput` 流式传入，**不要改为命令行参数**（会被 `ps` 看到）。
- `--json` 输出按行解析为 `ResticFileInfo` / `SnapshotInfo` 等。
- 路径中含 `:\` 时会被转换为 `/apps/<drive>/...` 形式（参考 `ListFilesInSnapshotAsync`）。

### 调度器（`QuartzSchedulerService`）

- 以 `IHostedService` 形式启动，启动时遍历所有 `IsEnabled=true` 且 `CronExpression` 非空的任务。
- 每个任务的 JobKey/TriggerKey 命名规则：`BackupTaskJob_{Id}` / `BackupTaskTrigger_{Id}`，重新调度会先 `DeleteJob` 再 `ScheduleJob`。
- Cron 时区固定为 `TimeZoneInfo.Local`。

### 多数据库扩展点

新增数据库（例如把 MySql 真正启用）需要：
1. 在 `ResticManager.Web.csproj` 添加对 `ResticManager.MySql` 的 `ProjectReference`。
2. 在 `Program.cs` 取消注释 `case "mysql"` 分支并启用对应的 `AddScoped<ResticManagerDbContext, MySqlDbContext>()`。
3. 实现一个 `MySqlEntityTypeConfigurations/` 下的列配置（参考其他数据库项目结构）。

### 数据库迁移

迁移文件按数据库分别存放：
- `ResticManager.Sqlite/Migrations/`
- `ResticManager.PgSql/Migrations/`
- （MySql 暂无迁移目录）

新增实体时需要同时为需要支持的数据库添加迁移，并通过 `ResticManager.Web/migration.bat` 一次性生成。

### 配置说明

默认配置位于 `data/appsettings.json`（运行时目录，首次启动自动创建）：

```jsonc
{
  "Database": { "DbType": "sqlite" },          // sqlite | mysql | postgresql
  "ConnectionStrings": {
    "SqliteConnection": "Data Source=./data/restic-manager.db",
    "MySqlConnection": "...",
    "PostgreSqlConnection": "..."
  },
  "Jwt": { "Secret": "..." },                    // 自动生成，必须 ≥32 字符
  "Restic": { "Path": "restic/0.18.1/restic(.exe)" }
}
```

部署时**必须挂载 `/app/data`**（保存数据库和配置）和 `/restic`（保存备份仓库），否则容器重建后数据丢失。

### CORS

默认只放行 `http://localhost:8080`（前端开发服务器）。生产部署如果前后端同源（Nginx 反代或 SPA 同站点），无需调整；分离部署需在 `Program.cs` 的 `AddCors` 中补充来源。

## 常见陷阱

- 修改前端代码后必须 `npm run build`，否则发布目录里仍是旧版本——`vue.config.js` 的 `outputDir` 指向解决方案根目录的 `restic-manager-release/wwwroot`，**不**和 dev server 的内存构建相同。
- **不要在 HTTP 请求内直接 await restic 调用**。仓库类接口的约定是"校验 → 登记 AsyncTask → 立即返回 `{ taskId }`"，改接口时保持这一结构，否则网盘仓库下前端必然 60 秒超时。
- 前端 babel 版本较老：`??`（空值合并）会导致构建报 `Module parse failed`（`?.` 可用）。写新代码避免 `??`。
- 生成 PgSql 迁移**不能**直接 `dotnet ef`（Web 未引用 PgSql 项目；且临时加引用后 Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4 与解决方案解析出的 EF 10 设计时二进制不兼容，报 `Method not found`）。现有做法：参照 Sqlite 迁移**手写** PgSql 迁移 + Designer（可从 ModelSnapshot 派生）+ 更新 `PgSqlDbContextModelSnapshot.cs`，文件名时间戳与 Sqlite 版保持一致。
- `Program.cs` 中 `case "mysql"` / `case "postgresql"` 的 DbContext 注册被注释，开启前请确认对应项目已被引用、迁移已生成。
- 任何 `appsettings.json` 变更后，**不要**手动生成 `Jwt:Secret`，让启动逻辑自动保证长度。
- `BackupTask.LastRunStatus` 是字符串字段（如 `NeverRun`），不是枚举，新增状态值时同步更新前端。
- `ResticService` 的路径处理依赖 Windows 风格分隔符被替换为 `/`，Linux 路径不受影响但请保持一致。
- 全局异常中间件返回中文 `message`："服务器内部错误"，前端按此字段判断错误展示。