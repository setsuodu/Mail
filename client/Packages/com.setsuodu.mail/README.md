# Mail Unity Client

Unity 客户端游戏内邮件与补偿服务 UPM 包。用于拉取玩家收件箱、标记已读、领取邮件附件以及删除邮件。

## Summary

* **功能**：玩家收件箱拉取、邮件已读/删除状态同步、邮件附件领取。
* **环境**：支持 Unity 全平台，轻量级无重度依赖。
* **路径**：UPM 完整包位于仓库 `client/Packages/com.setsuodu.mail`。

## GetStarted

### 1. 安装包 (OpenUPM)

```bash
openupm add com.setsuodu.mail
```

### 2. 初始化接入

1. 在场景中创建客户端对象（菜单：**Tools** → **Mail** → **Create MailClient GameObject**）。
2. 配置 **Server Base Url**。
3. 玩家登录成功后，设置对应的 **Bearer JWT** 鉴权凭证：

```csharp
using Setsuodu.Mail;

public class MailInitializer
{
    public static void SetPlayerToken(string jwtToken)
    {
        // 设置玩家 JWT 凭证 (Header: Authorization: Bearer <JWT>)
        MailClient.Instance.SetAuthToken(jwtToken); 
    }
}
```

## API

客户端内部基于以下服务端核心接口进行网络交互与状态同步：

* **`GET /v1/inbox`**：拉取当前玩家的未删除邮件列表（收件箱）。
* **`POST /v1/inbox/{id}/read`**：将指定 ID 的邮件标记为已读。
* **`POST /v1/inbox/{id}/claim`**：领取指定 ID 邮件中的附件。
* **`DELETE /v1/inbox/{id}`**：删除指定 ID 的邮件。
