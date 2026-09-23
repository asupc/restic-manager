# ResticManager

一个基于 .NET 10 + Vue 2 的 [restic](https://restic.net/) 备份图形化管理平台。提供用户认证、仓库管理、定时备份规则、备份记录、快照浏览、文件树查看与文件/目录恢复等能力，并通过 RESTful API 暴露所有功能。

> 项目名直译为 "restic 管理器"。已用于个人与小型团队的备份运维场景。

---

## 目录

- [功能特性](#功能特性)
- [技术架构](#技术架构)
- [解决方案结构](#解决方案结构)
- [快速开始](#快速开始)
  - [Docker 部署（推荐）](#docker-部署推荐)
  - [本地开发](#本地开发)
- [数据库配置](#数据库配置)
- [Restic 配置](#restic-配置)
- [API 文档](#api-文档)
- [常见问题](#常见问题)

---

## 功能特性

- 单用户注册 / 登录（系统只允许创建一个管理员账号）
- 仓库管理：本地 / SFTP / S3 等多种 restic 后端，初始化、校验、统计
- 备份任务：源路径、目标仓库、Cron 表达式、include/exclude 规则
- 定时调度：基于 Quartz.NET，Cron 时区跟随服务器本地时区
- 备份记录：执行结果摘要、错误信息、统计
- 快照浏览：快照列表、文件树、快照差异比较
- 文件 / 目录恢复：从快照恢复整个目录或单个文件
- SPA 前端 + Swagger API 文档

---

## 技术架构

### 后端

| 模块 | 选型 |
| --- | --- |
| 运行时 | .NET 10（`TargetFramework=net10.0`） |
| Web 框架 | ASP.NET Core |
| ORM | Entity Framework Core |
| 任务调度 | Quartz.NET |
| 认证 | JWT（`Microsoft.AspNetCore.Authentication.JwtBearer`） |
| API 文档 | Swagger / Swashbuckle.AspNetCore |
| 密码哈希 | ASP.NET Core `PasswordHasher<T>` |

### 前端

| 模块 | 选型 |
| --- | --- |
| 框架 | Vue 2 + Vue Router + Vuex |
| UI 组件 | Element UI |
| HTTP 客户端 | Axios |
| 构建工具 | Vue CLI 4 |

### 数据库

支持三种数据库：

| 类型 | 状态 | 说明 |
| --- | --- | --- |
| **SQLite** | ✅ 默认启用 | 适合个人 / 小型部署，无需额外服务 |
| **PostgreSQL** | ⚠️ 已实现，需手动启用 | 适合中大型部署 |
| **MySQL** | ⚠️ 已实现，需手动启用 | 代码中存在 `ResticManager.MySql` 项目，但 `Program.cs` 默认未注册 DbContext |

> "默认启用"指 `Program.cs` 中的 `AddScoped<ResticManagerDbContext, XxxDbContext>()` 行未被注释。**首次切换数据库需要先在对应项目下生成 EF 迁移**（见下文）。

### 后台异步任务（AsyncTask）

所有需要访问 restic 仓库的操作（快照同步、恢复、删除/批量删除、diff、标签、prune、check、unlock、repair、find、文件树浏览、仓库初始化/统计、手动运行备份）都是**后台异步任务**：HTTP 请求只做参数校验并登记任务，立即返回 `{ taskId }`，restic 命令在后台执行。前端不再等待接口返回，通过轮询获取状态与结果——这是为网盘挂载仓库（restic 命令可能耗时分钟到小时级）设计的，避免前端 60 秒超时假失败。

- 状态持久化在 `AsyncTasks` 表（`Pending/Running/Succeeded/Failed`），应用重启时遗留的在途任务自动标记为失败（含备份任务的状态位复位）。
- 并发控制：全局信号量限制同时在跑的 restic 进程数（3），每个仓库一把信号量串行化访问（防 restic 锁冲突）；**解锁仓库不排队、不防重入**（它是解除死锁的工具）。
- 防重入：同 `(类型, 仓库, 目标)` 已有在途任务时拒绝重复提交（返回 400 中文提示）。
- 查询接口：`GET /api/asynctasks/{id}`（单任务）、`GET /api/asynctasks/active`、`GET /api/asynctasks/dashboard`（聚合，供全局轮询）。
- 前端：提交后按钮进入"执行中"态并 `pollTask` 轮询（`src/utils/task-poller.js`）；侧边栏"后台任务"角标 + 跨页面完成/失败 Notification（Vuex `tasks` module 双频率轮询 2s/30s）。

已知限制：任务暂不支持取消（误发的 prune 只能等它跑完，或重启应用后用"解锁仓库"清理陈旧锁）；快照单文件下载仍是同步流式接口，超大文件可能受前端 60 秒超时影响。

---

## 解决方案结构

```
ResticManager.sln
├── ResticManager.Web          # ASP.NET Core 入口：Controllers、Program.cs、Middleware、Vue 工程、restic 二进制
├── ResticManager.Application  # 业务服务（Services/Abstractions + Implements）、Quartz Jobs、工具类
├── ResticManager.Data         # 抽象 DbContext (ResticManagerDbContext)
├── ResticManager.Models       # 实体（User/Repository/BackupTask/BackupRule/BackupRecord）与 DTO
├── ResticManager.Sqlite       # SqliteDbContext + Migrations
├── ResticManager.PgSql        # PgSqlDbContext + Migrations
└── ResticManager.MySql        # MySqlDbContext（需手动启用）
```

依赖方向：`Web → Application → Data / Models`，数据库提供程序通过 DI 注入的 `ResticManagerDbContext` 实现切换。

关键文件：

- `ResticManager.Web/Program.cs`：启动入口、数据库选择、JWT / CORS / Swagger 配置、SPA fallback 中间件、`Database.Migrate()`
- `ResticManager.Web/Controllers/`：REST API（Auth / Repository / AsyncTask / BackupTasks / BackupRules / BackupRecords / BackupFiles）
- `ResticManager.Application/Services/Implements/ResticService.cs`：通过 `Process` 调用 restic CLI 的封装
- `ResticManager.Application/Services/Implements/AsyncTaskService.cs`：后台异步任务服务（登记/防重入/并发控制/执行/孤儿清理）
- `ResticManager.Application/Services/Implements/QuartzSchedulerService.cs`：Quartz 调度托管服务
- `ResticManager.Application/Jobs/BackupTaskJob.cs`：每个定时任务的执行入口
- `ResticManager.Web/migration.bat`：一次性为所有数据库生成 EF 迁移

---

## 快速开始

### Docker 部署（推荐）

#### 1. 使用 docker-compose

创建 `docker-compose.yml`：

```yaml
version: "3.4"

services:
  restic-manager:
    image: asupc/restic-manager:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://0.0.0.0:80
      - TZ=Asia/Shanghai
    ports:
      - "5051:80"
    volumes:
      # 配置与 SQLite 数据库
      - "./data/:/app/data"
      # 备份数据目录（务必挂载，否则重建容器后备份丢失）
      - "/host/restic/data:/restic"
```

启动：

```bash
docker-compose up -d
```

访问 `http://ip:5051`，首次进入会引导创建管理员账号。

#### 2. 自行构建镜像

Windows 下在仓库根目录执行 `publish-build-docker.bat`：

```cmd
publish-build-docker.bat
```

脚本会依次：`dotnet publish` → `npm run build` → `docker build` → `docker save` 输出 `restic-manager.tar`。把 tar 拷到目标服务器后：

```bash
docker load -i restic-manager.tar
docker run -d -p 5051:80 \
  -v ./data:/app/data \
  -v /host/restic/data:/restic \
  --name restic-manager \
  asupc/restic-manager
```

### 本地开发

#### 准备

1. 安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. 安装 Node.js LTS 与 npm

#### 启动后端

```bash
dotnet restore ResticManager.sln
dotnet run --project ResticManager.Web
```

默认监听 `https://localhost:5000`（HTTP 5000 自动重定向到 HTTPS）。

#### 启动前端开发服务器

```bash
cd ResticManager.Web/restic-manager-ui
npm install
npm run serve
```

前端开发服务器在 `http://localhost:8080`，通过 `vue.config.js` 配置的代理把 `/api` 转发到 `http://localhost:5050`。

#### 生产构建

```bash
cd ResticManager.Web/restic-manager-ui
npm run build
```

> 构建产物输出到 `restic-manager-release/wwwroot`，由后端的静态文件中间件托管。**修改前端后必须重新构建**，否则发布包内仍是旧页面。

#### 一键发布

`publish-build-docker.bat` 完成：清空 `restic-manager-release` → `dotnet publish` → `npm run build` → `docker build` → `docker save`。

---

## 数据库配置

首次启动会自动在 `data/appsettings.json` 生成默认配置（SQLite）。修改后重启生效。完整示例：

```json
{
  "Database": {
    "DbType": "sqlite"
  },
  "ConnectionStrings": {
    "SqliteConnection": "Data Source=./data/restic-manager.db",
    "MySqlConnection": "server=localhost;port=3306;database=ResticManager;user=root;password=your_password",
    "PostgreSqlConnection": "Host=localhost;Database=ResticManager;Username=postgres;Password=your_password"
  },
  "Jwt": {
    "Secret": "自动生成，至少 32 字符"
  },
  "Restic": {
    "Path": "restic/0.18.1/restic(.exe)"
  }
}
```

`Database:DbType` 可选值：

- `sqlite` — 默认
- `postgresql` — 需先在 `ResticManager.PgSql` 下生成迁移并启用
- `mysql` — 需先在 `ResticManager.MySql` 下生成迁移并启用

### 切换到 PostgreSQL / MySQL

1. 在目标数据库项目下生成迁移：

   ```bash
   # PostgreSQL
   dotnet ef migrations add <Name> \
     --project ../ResticManager.PgSql \
     --startup-project ./ResticManager.Web \
     --context PgSqlDbContext \
     --output-dir Migrations

   # MySQL
   dotnet ef migrations add <Name> \
     --project ../ResticManager.MySql \
     --startup-project ./ResticManager.Web \
     --context MySqlDbContext \
     --output-dir Migrations
   ```

   或直接双击 `ResticManager.Web/migration.bat` 一次性为两种数据库生成。

2. 在 `ResticManager.Web/Program.cs` 中取消对应分支的注释：

   ```csharp
   switch (databaseProvider.ToLower())
   {
       case "mysql":
           builder.Services.AddScoped<ResticManagerDbContext, MySqlDbContext>();
           break;
       case "postgresql":
           builder.Services.AddScoped<ResticManagerDbContext, PgSqlDbContext>();
           break;
       default:
           builder.Services.AddScoped<ResticManagerDbContext, SqliteDbContext>();
           break;
   }
   ```

   `AddDbContext` 中也要把对应分支取消注释（`UseMySql` / `UseNpgsql`）。

3. 在 `ResticManager.Web.csproj` 中补上对应项目的 `ProjectReference`。

4. 修改 `data/appsettings.json` 的 `DbType` 和对应连接字符串，重启。

---

## Restic 配置

启动时会从 `Restic:Path` 读取 restic 可执行文件路径。仓库自带 `ResticManager.Web/restic/0.18.1/`（Windows / Linux 双版本），按操作系统自动选择 `restic.exe` 或 `restic`。

如需使用系统安装的 restic，覆盖为：

```json
{ "Restic": { "Path": "/usr/local/bin/restic" } }
```

---

## API 文档

启动后访问 `/swagger` 查看 OpenAPI 文档。所有需要鉴权的接口在请求头携带 `Authorization: Bearer <token>`，token 通过 `POST /api/auth/login` 获取。

主要端点：

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| POST | `/api/auth/register` | 注册（仅在无用户时可用） |
| POST | `/api/auth/login` | 登录获取 JWT |
| GET  | `/api/auth/has-user` | 是否已存在用户 |
| GET / POST / PUT / DELETE | `/api/Repository` | 仓库 CRUD |
| GET / POST / PUT / DELETE | `/api/BackupTasks` | 备份任务 CRUD |
| POST | `/api/BackupTasks/{id}/run` | 立即执行一次备份 |
| PUT | `/api/BackupTasks/{id}/schedule` | 更新 Cron 表达式 |
| GET / POST / PUT / DELETE | `/api/BackupRules` | 备份规则 CRUD |
| GET | `/api/BackupRecords` | 备份记录查询 |
| GET | `/api/BackupFiles` | 快照内文件浏览 |

---

## 常见问题

**Q: 容器重建后所有数据都没了？**
A: 必须挂载 `/app/data`（配置 + SQLite 数据库）和 `/restic`（备份仓库）两个卷。Docker 容器默认是无状态的。

**Q: `Jwt:Secret` 能否手动修改？**
A: 可以，但必须保持 ≥ 32 字符。启动时如果检测到 `Secret` 缺失或长度不足，会自动重新生成并覆盖配置文件——这会让所有已签发的 token 失效。

**Q: 备份任务的密码如何传递给 restic？**
A: 通过 `ResticService` 把仓库密码写入子进程的 `StandardInput`，**不会出现在命令行参数**（避免 `ps` 抓取）。

**Q: 备份任务调度时区是什么？**
A: `QuartzSchedulerService` 把 Cron 触发器显式设为 `TimeZoneInfo.Local`，跟随服务器所在时区。

**Q: 为什么只允许注册一个用户？**
A: `AuthService.RegisterAsync` 会检查 `Users` 表是否已有记录，避免多管理员冲突。若需多人协作，请手动扩展该方法并补充角色字段。

**Q: 前端改完代码页面没变化？**
A: dev server 是热更新；生产构建需要 `npm run build` 后 `dotnet publish` / 重新构建 Docker 镜像，前端产物才会被后端静态文件中间件加载。

**Q: 切换到 MySQL / PostgreSQL 后启动报 "找不到 DbContext 实现"？**
A: 检查 `Program.cs` 中 `AddDbContext` 与 `AddScoped<ResticManagerDbContext, XxxDbContext>()` 两个 switch 的对应分支都已启用。

**Q: restic 二进制能不能升级？**
A: 可以，但需同时替换 `ResticManager.Web/restic/0.18.1/restic` 和 `restic.exe`，并把 `Restic:Path` 中的 `0.18.1` 同步更新。

**Q: API 调用 404 但路径不是以 `/api` 开头？**
A: SPA fallback 中间件会把所有非 `/api`、非静态文件的请求重写到 `/index.html`，由 Vue Router 处理。**这是预期行为**，不是 bug。