# Mail

游戏内邮件 / 补偿服务：Unity 客户端收件箱 + .NET 10 服务端下发/查询。

- **客户端**：Unity UPM 包（`client/Packages/com.setsuodu.mail`，OpenUPM 子目录发布）
- **服务端**：ASP.NET Core Minimal API，Native AOT，产物为 Docker 镜像（GHCR）
- **不含**：Dashboard 前端、IM 通知集成

完整协议见 `shared/openapi.yaml`。

---

## 文档导航

| 文档 | 说明 |
|------|------|
| [主页介绍](README.md) | 项目概览、仓库结构、API 功能表、发布说明 |
| [客户端部署](docs/client-deployment.md) | Unity 包安装、配置、接入步骤 |
| [服务器部署](docs/server-deployment.md) | 本地运行、Docker Compose、环境变量 |
| [设计文档](docs/DESIGN.md) | 技术选型、数据模型、AOT、CI/CD |

---

## 仓库结构

```
Mail/
├── client/
│   └── Packages/com.setsuodu.mail/   # Unity UPM 包
├── server/src/Mail.Server.Api/       # .NET 10 API（AOT）
├── shared/openapi.yaml               # 协议唯一事实来源
├── deploy/docker-compose.example.yml
├── docs/
└── .github/workflows/
```

---

## API 功能表

完整契约以 `shared/openapi.yaml` 为准。业务接口前缀 `/api/v1`（健康检查除外）。

| 方法 | 路径 | 鉴权 | 功能说明 |
|------|------|------|----------|
| GET | `/health` | 无 | 健康检查 |
| GET | `/api/v1/inbox` | Bearer JWT（玩家） | 拉取收件箱（未删除） |
| POST | `/api/v1/inbox/{id}/read` | Bearer JWT | 标记已读 |
| POST | `/api/v1/inbox/{id}/claim` | Bearer JWT | 领取附件 |
| DELETE | `/api/v1/inbox/{id}` | Bearer JWT | 删除邮件 |
| POST | `/api/v1/admin/mails` | X-Admin-Api-Key | GM/运营发信（单发/群发/全服） |
| GET | `/api/v1/admin/mails` | X-Admin-Api-Key | 管理端列表查询 |
| GET | `/api/v1/admin/mails/{id}` | X-Admin-Api-Key | 管理端详情 |

### 鉴权说明

- **玩家接口**：Header `Authorization: Bearer <JWT>`（与 MP 共用 Secret，HS256；从 claims 取 `sub` / `user_id`）
- **Admin（Dashboard / GM）**：Header `X-Admin-Api-Key`
- 两者互不通用。

---

## 数据库迁移

| 场景 | 命令 |
|------|------|
| Compose 只跑迁移 | `cd server && docker compose run --rm mail-migrate` |
| 本地无 Docker | `dotnet run --project server/src/Mail.Server.Api -- --migrate` |
| 正常启动 | 启动时自动 DbUp（单副本）；多副本请拆独立 Job |

---

## 发布 Tag 策略

| Tag | 触发工作流 | 产物 |
|-----|------------|------|
| `client-vX.Y.Z` | release-client | OpenUPM 客户端包 |
| `server-vX.Y.Z` | release-server | GHCR 镜像 `mail-server:X.Y.Z` |
| `vX.Y.Z` | release-all | 客户端 + 服务端联合发布 |

---

## 快速开始（服务端本地）

```bash
# 需要 .NET 10 SDK + PostgreSQL
export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=mail;Username=mail;Password=mail"
export Auth__AdminApiKey=dev-admin-key
export Auth__JwtSecret=your-shared-jwt-secret-with-mp

cd server
dotnet run --project src/Mail.Server.Api
# → http://localhost:8080/health
```

或使用示例 Compose（替换镜像名与密钥）：

```bash
docker compose -f deploy/docker-compose.example.yml up
```

## 客户端接入

1. 通过 OpenUPM / git URL 安装 `com.setsuodu.mail`
2. 场景中添加 `MailClient`（菜单：Tools → Mail → Create MailClient GameObject）
3. 配置 Server Base Url，登录后设置 JWT

## 技术要点（服务端）

- Minimal API + CreateSlimBuilder + JsonSerializerContext（AOT）
- PostgreSQL + 原生 ADO.NET（Npgsql）+ DbUp 嵌入式迁移
- Docker：多阶段 AOT publish → runtime-deps chiseled 镜像
- 玩家身份来自 JWT claims，不在本服务存账号表