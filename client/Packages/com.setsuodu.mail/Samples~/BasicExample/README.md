# Inbox Demo（端到端跑通 Mail API）

## 前置

1. 服务端已启动：

```bash
cd server
cp .env.example .env
docker compose up --build
# http://localhost:12081/health
```

2. `.env` 中与 Sample 默认值一致（或改 Sample Inspector）：

| Sample 字段 | .env |
|-------------|------|
| Admin Api Key | `AUTH_ADMIN_API_KEY`（默认 `dev-admin-key`） |
| Jwt Secret | `AUTH_JWT_SECRET`（默认 `dev-jwt-secret-shared-with-mp`） |
| Project Id | `DEFAULT_PROJECT_ID`（默认 `default`） |

## 导入 Sample

Package Manager → **Mail** → Samples → **Inbox Demo** → Import  
或直接把 `Samples~/InboxDemo` 下脚本挂到场景。

## 场景步骤

1. 菜单 **Tools → Mail → Create MailClient GameObject**
2. Inspector 设置 **Server Base Url** = `http://localhost:12081`，**Project Id** = `default`
3. 空物体挂上 **MailInboxSample**（字段与上表对齐）
4. Play

## 快捷键

| 键 | 作用 |
|----|------|
| **F1** | Admin 发一封补偿信给 `sampleUserId`（带 gem×100） |
| **F2** | 用本地 HS256 JWT 拉收件箱并打印 |
| **F3** | 第一封标记已读 |
| **F4** | 第一封领取附件（看 Console 发奖日志） |
| **F5** | 第一封软删除 |
| **F6** | 完整流程：Seed → Refresh → Read → Claim → Refresh |

组件上也可右键 Context Menu 触发同样操作。

## 预期日志

- F1：`Admin seed OK 201`
- F2：`Inbox count>=1` 及标题列表
- F4：`grant itemId=gem count=100`
- 再按 F4：`already=True`（幂等）

## 说明

- Sample 内 `SampleJwt` 仅用于本地联调，正式环境请用 MP 登录下发的 JWT。
- `MailClient` 的 projectId / URL 以场景里组件为准；Admin 发信用 Sample 上的 Base Url + Admin Key。
