using System.Text.Json.Serialization;
using Mail.Server.Api.Models;

namespace Mail.Server.Api.Json;

[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(MailAttachment))]
[JsonSerializable(typeof(List<MailAttachment>))]
[JsonSerializable(typeof(MailItem))]
[JsonSerializable(typeof(List<MailItem>))]
[JsonSerializable(typeof(InboxListResponse))]
[JsonSerializable(typeof(ClaimResponse))]
[JsonSerializable(typeof(AdminCreateMailRequest))]
[JsonSerializable(typeof(AdminMailSummary))]
[JsonSerializable(typeof(List<AdminMailSummary>))]
[JsonSerializable(typeof(AdminMailDetail))]
[JsonSerializable(typeof(AdminMailListResponse))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class AppJsonContext : JsonSerializerContext
{
}
