# 服务器部署

## 环境变量

| 变量 | 说明 |
|------|------|
| `ConnectionStrings__Postgres` | PostgreSQL 连接串 |
| `Auth__AdminApiKey` | GM/Dashboard 密钥 |
| `Auth__JwtSecret` | 与 MP 共用的 JWT HS256 Secret |
| `DefaultProjectId` | 客户端未传 projectId 时的默认值 |
| `ASPNETCORE_URLS` | 默认 `http://+:8080` |

## Docker Compose

见 `deploy/docker-compose.example.yml`。建议主机端口 **12081**。

## 迁移

启动时自动 DbUp。仅迁移：

```bash
dotnet run --project server/src/Mail.Server.Api -- --migrate
# 或
RUN_MIGRATION_ONLY=true
```

## 发信示例

```bash
curl -X POST http://localhost:12081/api/v1/admin/mails \
  -H "X-Admin-Api-Key: change-me-admin" \
  -H "Content-Type: application/json" \
  -d '{
    "projectId": "match3",
    "title": "补偿礼包",
    "content": "维护补偿，请查收",
    "attachments": [{"itemId": "gem", "count": 100}],
    "targetUserIds": ["user-1", "user-2"],
    "senderName": "System"
  }'
```
