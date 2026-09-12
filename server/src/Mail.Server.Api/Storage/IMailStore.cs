using Mail.Server.Api.Models;

namespace Mail.Server.Api.Storage;

public interface IMailStore
{
    Task<(List<MailItem> Items, int Total)> ListInboxAsync(
        string projectId, string userId, bool includeClaimed, int page, int pageSize, CancellationToken ct = default);

    Task<MailItem?> MarkReadAsync(string projectId, string userId, Guid userMailId, CancellationToken ct = default);

    Task<ClaimResponse?> ClaimAsync(string projectId, string userId, Guid userMailId, CancellationToken ct = default);

    Task<bool> SoftDeleteAsync(string projectId, string userId, Guid userMailId, CancellationToken ct = default);

    Task<AdminMailSummary> CreateMailAsync(AdminCreateMailRequest request, string? createdBy, CancellationToken ct = default);

    Task<(List<AdminMailSummary> Items, int Total)> AdminListAsync(
        string? projectId, int page, int pageSize, CancellationToken ct = default);

    Task<AdminMailDetail?> AdminGetAsync(Guid mailId, CancellationToken ct = default);
}
