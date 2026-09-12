# Mail 服务 设计文档

对齐 BugReport 组件约定，实现游戏内邮件 / 补偿。

## 1. 范围

- **做**：玩家收件箱、已读、领取附件（幂等）、软删除；GM/运营发信（显式 user 列表）
- **不做**：Dashboard UI、IM 通知、全服广播自动 fan-out（v1 要求显式 targetUserIds）

## 2. 数据模型

- `mails`：邮件模板/内容（title、content、attachments JSONB、expire_at）
- `user_mails`：玩家实例（is_read / is_claimed / is_deleted，UNIQUE(mail_id, user_id)）

领取在 DB 事务内 `FOR UPDATE`，保证幂等。

## 3. 鉴权

- 玩家：Bearer JWT（与 MP 共用 HS256 Secret，claims：`sub` / `user_id` / `uid`）
- Admin：`X-Admin-Api-Key`

## 4. 技术选型

与 BugReport 相同：.NET 10 Minimal API + CreateSlimBuilder + AOT + Npgsql + DbUp + chiseled runtime-deps。

JWT 校验使用 `System.IdentityModel.Tokens.Jwt`（注意 AOT 裁剪；仅验证签名与过期，不做反射策略）。

## 5. 客户端

Unity UPM：`MailClient` + `MailApiClient`。登录后 `SetJwt`，再 `RefreshAsync` / `ClaimAsync`。
附件发放由游戏业务层根据 `ClaimResponse.attachments` 处理（本服务不扣库存/不发道具）。

## 6. 待办

- [ ] 全服邮件模板 + 登录时懒 fan-out
- [ ] 多副本时 DbUp 拆独立 Job
- [ ] Admin JWT / 角色权限（当前静态 Key）
- [ ] 附件领取与游戏发奖服务的事务回执（outbox）
