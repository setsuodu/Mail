# 客户端部署

## 安装

OpenUPM 或 git URL：

```
https://github.com/LongLongGames/Mail.git?path=client/Packages/com.setsuodu.mail
```

## 接入步骤

1. 菜单 **Tools → Mail → Create MailClient GameObject**
2. Inspector 配置 `Server Base Url`、`Project Id`
3. 玩家登录 MP 拿到 JWT 后调用：

```csharp
MailClient.Instance.SetJwt(jwt);
await MailClient.Instance.RefreshAsync();
```

4. 领取：

```csharp
var claim = await MailClient.Instance.ClaimAsync(mailItemId);
foreach (var a in claim.attachments)
{
    // 游戏内发奖：a.itemId / a.count
}
```

## 注意

- JWT 与 MP 共用 Secret；过期需重新登录
- `Claim` 幂等：重复领取返回 `alreadyClaimed=true`，仍带附件列表
