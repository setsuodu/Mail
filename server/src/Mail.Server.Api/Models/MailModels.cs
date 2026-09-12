namespace Mail.Server.Api.Models;

public sealed class MailAttachment
{
    public required string ItemId { get; set; }
    public int Count { get; set; }
    public Dictionary<string, object>? Meta { get; set; }
}

public sealed class MailItem
{
    public Guid Id { get; set; }
    public Guid MailId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public List<MailAttachment> Attachments { get; set; } = [];
    public bool IsRead { get; set; }
    public bool IsClaimed { get; set; }
    public DateTimeOffset? ExpireAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
}

public sealed class InboxListResponse
{
    public required List<MailItem> Items { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public sealed class ClaimResponse
{
    public Guid MailItemId { get; set; }
    public bool AlreadyClaimed { get; set; }
    public List<MailAttachment> Attachments { get; set; } = [];
    public DateTimeOffset ClaimedAt { get; set; }
}

public sealed class AdminCreateMailRequest
{
    public required string ProjectId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public List<MailAttachment>? Attachments { get; set; }
    public List<string>? TargetUserIds { get; set; }
    public DateTimeOffset? ExpireAt { get; set; }
    public string? SenderName { get; set; }
}

public sealed class AdminMailSummary
{
    public Guid Id { get; set; }
    public required string ProjectId { get; set; }
    public required string Title { get; set; }
    public int TargetCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpireAt { get; set; }
    public string? SenderName { get; set; }
}

public sealed class AdminMailDetail
{
    public Guid Id { get; set; }
    public required string ProjectId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public List<MailAttachment> Attachments { get; set; } = [];
    public int TargetCount { get; set; }
    public List<string> TargetUserIds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpireAt { get; set; }
    public string? SenderName { get; set; }
}

public sealed class AdminMailListResponse
{
    public required List<AdminMailSummary> Items { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public sealed class HealthResponse
{
    public required string Status { get; set; }
}

public sealed class ErrorResponse
{
    public required string Error { get; set; }
}


