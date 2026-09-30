# WorkQ-Server

我Q 聊天服务端。用 C# 和 ASP.NET Core 实现，提供账号认证、私聊/群聊频道、历史消息、已读状态，以及基于 SignalR 的实时消息和在线状态广播。

## 技术栈

- .NET 9 / ASP.NET Core Minimal API
- SignalR 实时通信
- JWT Bearer 认证，PBKDF2-SHA256 密码存储
- EF Core 9 + SQLite
- xUnit 集成测试

## 本地运行

需要 .NET 9 SDK。

```bash
dotnet run --project src/WorkQ.Server
```

开发环境会自动迁移 SQLite 数据库，启动后：

- 健康检查：`http://localhost:5xxx/health`
- Swagger：`http://localhost:5xxx/swagger`

## 测试

```bash
dotnet test
```

## CI 打包

仓库内置 GitHub Actions 工作流（`.github/workflows/ci.yml`）：

- 每次 push/PR 都会执行 `dotnet restore`、`dotnet build`、`dotnet test`
- 构建测试通过后，按架构矩阵发布自包含单文件服务端：

| 平台 | 架构 | 产物 |
| --- | --- | --- |
| Linux | x64 | `workq-server-linux-x64.tar.gz` |
| Linux | arm64 | `workq-server-linux-arm64.tar.gz` |
| Linux (musl) | x64 | `workq-server-linux-musl-x64.tar.gz` |
| Linux (musl) | arm64 | `workq-server-linux-musl-arm64.tar.gz` |
| Windows | x64 | `workq-server-win-x64.zip` |
| Windows | arm64 | `workq-server-win-arm64.zip` |
| macOS | x64 | `workq-server-osx-x64.tar.gz` |
| macOS | arm64 | `workq-server-osx-arm64.tar.gz` |

- 每个平台压缩包都会附带 `.md5`、`.sha1`、`.sha256` 三个校验文件，并上传为 GitHub Actions Artifact
- 推送 `v*.*.*` 格式的 tag（例如 `v1.0.20260930`）时，工作流会自动把所有平台产物附加到 GitHub Release，并额外生成 `MD5SUMS.txt`、`SHA1SUMS.txt`、`SHA256SUMS.txt` 三个汇总校验文件

压缩包内是自包含单文件服务端和可直接编辑的 `appsettings.json`，目标机器无需安装 .NET 运行时。解压后启动方式与源码运行一致，例如：

```bash
tar -xzf workq-server-linux-x64.tar.gz
./workq-server-linux-x64
```

## 数据库迁移

```bash
dotnet ef migrations add <Name> --project src/WorkQ.Server
dotnet ef database update --project src/WorkQ.Server
```

## HTTP API

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| POST | `/api/auth/register` | 注册并返回 JWT |
| POST | `/api/auth/login` | 登录并返回 JWT |
| GET | `/api/users/me` | 当前用户信息 |
| GET | `/api/users?query=` | 搜索用户（含在线状态） |
| GET | `/api/channels` | 当前用户的频道列表（含未读数） |
| POST | `/api/channels` | 创建私聊或群聊频道 |
| GET | `/api/channels/{id}` | 频道详情 |
| POST | `/api/channels/{id}/members` | 群聊加人 |
| POST | `/api/channels/{id}/join` | 加入群聊 |
| POST | `/api/channels/{id}/leave` | 退出频道 |
| DELETE | `/api/channels/{id}` | 删除频道（仅群主） |
| GET | `/api/channels/{id}/messages` | 分页历史消息 |
| POST | `/api/channels/{id}/messages` | 发送消息（支持 `clientId` 去重） |
| POST | `/api/channels/{id}/read` | 标记已读 |

除注册和登录外，均需要在请求头携带 `Authorization: Bearer <token>`。

## SignalR

实时端点：`/hubs/chat`，连接时用 `access_token` 查询参数携带 JWT。

客户端可调用：

- `JoinChannel(channelId)`
- `LeaveChannel(channelId)`
- `SendMessage(channelId, content, clientId)`
- `Typing(channelId)`
- `GetChannelOnlineUsers(channelId)`

服务端广播事件：

- `ReceiveMessage`
- `MessageSent`
- `UserPresenceChanged`
- `UserTyping`
- `JoinedChannel`
- `LeftChannel`

## 部署注意

生产环境必须通过环境变量或配置系统替换 `appsettings.json` 中的开发用 `Jwt:Key`，并保持 `Database:AutoMigrate` 为 `false`，由部署流程手动执行数据库迁移。
