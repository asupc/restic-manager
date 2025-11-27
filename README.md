# ResticManager

一个基于 .NET 9 和 Vue.js 的图形化备份管理系统，用于简化 [restic](https://restic.net/) 备份工具的使用。

## 功能特点

- 用户认证（注册/登录）
- 存储库管理
- 备份规则配置
- 定时任务管理
- 备份记录查看
- 文件浏览器
- 快照管理
- RESTful API

## 技术架构

### 后端

- [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- 多数据库支持 (SQLite/MySQL/PostgreSQL)
- [JWT](https://jwt.io/) 认证
- [Quartz.NET](https://www.quartz-scheduler.net/) 任务调度
- [Swagger/OpenAPI](https://swagger.io/specification/) API 文档

### 前端

- [Vue.js 2](https://vuejs.org/v2/guide/)
- [Element UI](https://element.eleme.io/#/en-US)
- [Axios](https://axios-http.com/)
- [Vue Router](https://router.vuejs.org/)
- [Vuex](https://vuex.vuejs.org/)

### 数据库支持

本项目支持多种数据库：

1. SQLite (推荐、默认，适合小型部署)
2. PostgreSQL (适合大型部署)

## 部署指南

### Docker 部署（推荐）

使用 Docker 是最简单的部署方式，适用于 Windows、Linux 和 macOS。

#### 使用 docker-compose 部署

创建一个 docker-compose.yml 文件：

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
      # 映射你的数据目录
      # 注意 /app/data目录数据非常重要，包含你的配置或数据库数据
      # 如果未映射，容器重建数据将被清空
      - "./data/:/app/data"
      - "/host/restic/data:/restic" # 映射你的备份数据目录
```

然后运行以下命令启动服务：

```bash
docker-compose up -d
```

访问 `http://ip:5051` 即可使用。

#### 自行构建 Docker 镜像

Windows 下 执行 publish&build-docker.bat ，构建好的镜像使用 docker load -i <镜像文件.tar> 导入到目标服务器中
使用 docker run 启动镜像

```bash
docker run -d -p 5051:80 \
  -v ./data:/app/data \
  -v /host/restic/data:/restic \
  -v /host/apps:/apps \
  --name restic-manager \
  restic-manager
```

或者使用 docker/docker-compose.yml 启动镜像

```bash
docker-compose up -d
```

### Windows 部署

1. 确保系统安装了 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

2. 克隆或下载项目源代码

3. 在项目根目录执行以下命令安装前端依赖：

   ```cmd
   cd ResticManager.Web\restic-manager-ui
   npm install
   ```

4. 构建前端项目：

   ```cmd
   npm run build
   ```

5. 返回项目根目录并运行后端：

   ```cmd
   cd ..\..
   dotnet run --project ResticManager.Web
   ```

6. 访问 `http://localhost:5000` 使用应用

### Linux 部署

1. 安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)：

   ```bash
   # Ubuntu/Debian
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-10.0

   # CentOS/RHEL/Fedora
   sudo dnf install dotnet-sdk-10.0
   ```

2. 安装 Node.js 和 npm：

   ```bash
   curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
   sudo apt-get install -y nodejs
   ```

3. 克隆或下载项目源代码

4. 安装前端依赖并构建：

   ```bash
   cd ResticManager.Web/restic-manager-ui
   npm install
   npm run build
   ```

5. 返回项目根目录并运行后端：

   ```bash
   cd ../..
   dotnet run --project ResticManager.Web
   ```

6. 访问 `http://localhost:5000` 使用应用

### 配置数据库

#### 无配置时，系统将自动创建默认配置文件，启动后可以自行修改配置文件，修改后重新启动服务生效

在 `data/appsettings.json` 中配置数据库：

```json
{
  "Database": {
    "DbType": "sqlite"
  },
  "ConnectionStrings": {
    "SqliteConnection": "Data Source=./data/restic-manager.db",
    "PostgreSqlConnection": "Host=localhost;Database=ResticManager;Username=postgres;Password=your_password"
  }
}
```

将 `DbType` 设置为所需的数据库类型：

- `sqlite` for SQLite
- `postgresql` for PostgreSQL

### 配置 Restic

确保系统已安装 [restic](https://restic.net/) 并在 PATH 中可用，或者在 `data/appsettings.json` 中指定其路径：

```json
{
  "Restic": {
    "Path": "/usr/local/bin/restic"
  }
}
```

## API 文档

运行后访问 `/swagger` 查看 API 文档。
