using System.Text.Json;
using Mail.Server.Api.Json;
using Mail.Server.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace Mail.Server.Api.Storage;

public sealed class PostgresMailStore : IMailStore
{
    private readonly NpgsqlDataSource _ds;

    public PostgresMailStore(NpgsqlDataSource ds) => _ds = ds;

    public async Task<(List<MailItem> Items, int Total)> ListInboxAsync(
        string projectId, string userId, bool includeClaimed, int page, int pageSize, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);

        var claimedFilter = includeClaimed ? "" : "AND um.is_claimed = FALSE";
        var where = $"""
            um.project_id = @project_id AND um.user_id = @user_id AND um.is_deleted = FALSE
            AND (m.expire_at IS NULL OR m.expire_at > NOW())
            {claimedFilter}
            """;

        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"""
            SELECT COUNT(*)
            FROM user_mails um
            INNER JOIN mails m ON m.id = um.mail_id
            WHERE {where}
            """;
        countCmd.Parameters.AddWithValue("project_id", projectId);
        countCmd.Parameters.AddWithValue("user_id", userId);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = $"""
            SELECT um.id, um.mail_id, m.title, m.content, m.attachments::text,
                   um.is_read, um.is_claimed, m.expire_at, um.created_at, um.claimed_at
            FROM user_mails um
            INNER JOIN mails m ON m.id = um.mail_id
            WHERE {where}
            ORDER BY um.created_at DESC
            LIMIT @limit OFFSET @offset
            """;
        listCmd.Parameters.AddWithValue("project_id", projectId);
        listCmd.Parameters.AddWithValue("user_id", userId);
        listCmd.Parameters.AddWithValue("limit", pageSize);
        listCmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

        var items = new List<MailItem>();
        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(MapMailItem(reader));

        return (items, total);
    }

    public async Task<MailItem?> MarkReadAsync(string projectId, string userId, Guid userMailId, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE user_mails SET is_read = TRUE
            WHERE id = @id AND project_id = @project_id AND user_id = @user_id AND is_deleted = FALSE
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("id", userMailId);
        cmd.Parameters.AddWithValue("project_id", projectId);
        cmd.Parameters.AddWithValue("user_id", userId);
        var updated = await cmd.ExecuteScalarAsync(ct);
        if (updated is null)
            return null;
        return await GetUserMailAsync(conn, projectId, userId, userMailId, ct);
    }

    public async Task<ClaimResponse?> ClaimAsync(string projectId, string userId, Guid userMailId, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using var select = conn.CreateCommand();
        select.Transaction = tx;
        select.CommandText = """
            SELECT um.id, um.is_claimed, um.claimed_at, m.attachments::text, m.expire_at
            FROM user_mails um
            INNER JOIN mails m ON m.id = um.mail_id
            WHERE um.id = @id AND um.project_id = @project_id AND um.user_id = @user_id AND um.is_deleted = FALSE
            FOR UPDATE OF um
            """;
        select.Parameters.AddWithValue("id", userMailId);
        select.Parameters.AddWithValue("project_id", projectId);
        select.Parameters.AddWithValue("user_id", userId);

        await using var reader = await select.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            await reader.CloseAsync();
            await tx.RollbackAsync(ct);
            return null;
        }

        var isClaimed = reader.GetBoolean(1);
        var claimedAt = reader.IsDBNull(2) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(2);
        var attachmentsJson = ReadText(reader, 3);
        var expireAt = ReadDto(reader, 4);
        await reader.CloseAsync();

        if (expireAt is { } exp && exp <= DateTimeOffset.UtcNow)
        {
            await tx.RollbackAsync(ct);
            return null; // caller maps to 410
        }

        var attachments = DeserializeAttachments(attachmentsJson);

        if (isClaimed && claimedAt is not null)
        {
            await tx.CommitAsync(ct);
            return new ClaimResponse
            {
                MailItemId = userMailId,
                AlreadyClaimed = true,
                Attachments = attachments,
                ClaimedAt = claimedAt.Value
            };
        }

        var now = DateTimeOffset.UtcNow;
        await using var update = conn.CreateCommand();
        update.Transaction = tx;
        update.CommandText = """
            UPDATE user_mails
            SET is_claimed = TRUE, is_read = TRUE, claimed_at = @claimed_at
            WHERE id = @id AND is_claimed = FALSE
            """;
        update.Parameters.AddWithValue("id", userMailId);
        update.Parameters.AddWithValue("claimed_at", now);
        await update.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);

        return new ClaimResponse
        {
            MailItemId = userMailId,
            AlreadyClaimed = false,
            Attachments = attachments,
            ClaimedAt = now
        };
    }

    public async Task<bool> SoftDeleteAsync(string projectId, string userId, Guid userMailId, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE user_mails SET is_deleted = TRUE
            WHERE id = @id AND project_id = @project_id AND user_id = @user_id AND is_deleted = FALSE
            """;
        cmd.Parameters.AddWithValue("id", userMailId);
        cmd.Parameters.AddWithValue("project_id", projectId);
        cmd.Parameters.AddWithValue("user_id", userId);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<AdminMailSummary> CreateMailAsync(AdminCreateMailRequest request, string? createdBy, CancellationToken ct = default)
    {
        var mailId = Guid.NewGuid();
        var targets = request.TargetUserIds?.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList() ?? [];
        var attachmentsJson = JsonSerializer.Serialize(
            request.Attachments ?? [],
            AppJsonContext.Default.ListMailAttachment);

        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using var insertMail = conn.CreateCommand();
        insertMail.Transaction = tx;
        insertMail.CommandText = """
            INSERT INTO mails (id, project_id, title, content, attachments, sender_name, expire_at, created_by)
            VALUES (@id, @project_id, @title, @content, @attachments, @sender_name, @expire_at, @created_by)
            """;
        insertMail.Parameters.AddWithValue("id", mailId);
        insertMail.Parameters.AddWithValue("project_id", request.ProjectId);
        insertMail.Parameters.AddWithValue("title", request.Title);
        insertMail.Parameters.AddWithValue("content", request.Content);
        insertMail.Parameters.Add(new NpgsqlParameter("attachments", NpgsqlDbType.Jsonb) { Value = attachmentsJson });
        insertMail.Parameters.AddWithValue("sender_name", (object?)request.SenderName ?? DBNull.Value);
        insertMail.Parameters.AddWithValue("expire_at", (object?)request.ExpireAt ?? DBNull.Value);
        insertMail.Parameters.AddWithValue("created_by", (object?)createdBy ?? DBNull.Value);
        await insertMail.ExecuteNonQueryAsync(ct);

        foreach (var uid in targets)
        {
            await using var insertUm = conn.CreateCommand();
            insertUm.Transaction = tx;
            insertUm.CommandText = """
                INSERT INTO user_mails (id, mail_id, project_id, user_id)
                VALUES (@id, @mail_id, @project_id, @user_id)
                ON CONFLICT (mail_id, user_id) DO NOTHING
                """;
            insertUm.Parameters.AddWithValue("id", Guid.NewGuid());
            insertUm.Parameters.AddWithValue("mail_id", mailId);
            insertUm.Parameters.AddWithValue("project_id", request.ProjectId);
            insertUm.Parameters.AddWithValue("user_id", uid);
            await insertUm.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        return new AdminMailSummary
        {
            Id = mailId,
            ProjectId = request.ProjectId,
            Title = request.Title,
            TargetCount = targets.Count,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpireAt = request.ExpireAt,
            SenderName = request.SenderName
        };
    }

    public async Task<(List<AdminMailSummary> Items, int Total)> AdminListAsync(
        string? projectId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        var where = string.IsNullOrEmpty(projectId) ? "" : "WHERE project_id = @project_id";

        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM mails {where}";
        if (!string.IsNullOrEmpty(projectId))
            countCmd.Parameters.AddWithValue("project_id", projectId);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = $"""
            SELECT m.id, m.project_id, m.title, m.created_at, m.expire_at, m.sender_name,
                   (SELECT COUNT(*) FROM user_mails um WHERE um.mail_id = m.id) AS target_count
            FROM mails m
            {where}
            ORDER BY m.created_at DESC
            LIMIT @limit OFFSET @offset
            """;
        if (!string.IsNullOrEmpty(projectId))
            listCmd.Parameters.AddWithValue("project_id", projectId);
        listCmd.Parameters.AddWithValue("limit", pageSize);
        listCmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

        var items = new List<AdminMailSummary>();
        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new AdminMailSummary
            {
                Id = reader.GetGuid(0),
                ProjectId = reader.GetString(1),
                Title = reader.GetString(2),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(3),
                ExpireAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                SenderName = reader.IsDBNull(5) ? null : reader.GetString(5),
                TargetCount = Convert.ToInt32(reader.GetInt64(6))
            });
        }

        return (items, total);
    }

    public async Task<AdminMailDetail?> AdminGetAsync(Guid mailId, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, project_id, title, content, attachments::text, created_at, expire_at, sender_name
            FROM mails WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", mailId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var detail = new AdminMailDetail
        {
            Id = reader.GetGuid(0),
            ProjectId = reader.GetString(1),
            Title = reader.GetString(2),
            Content = reader.GetString(3),
            Attachments = DeserializeAttachments(ReadText(reader, 4)),
            CreatedAt = ReadDto(reader, 5) ?? DateTimeOffset.UtcNow,
            ExpireAt = ReadDto(reader, 6),
            SenderName = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
        await reader.CloseAsync();

        await using var usersCmd = conn.CreateCommand();
        usersCmd.CommandText = "SELECT user_id FROM user_mails WHERE mail_id = @id ORDER BY created_at";
        usersCmd.Parameters.AddWithValue("id", mailId);
        var users = new List<string>();
        await using var ur = await usersCmd.ExecuteReaderAsync(ct);
        while (await ur.ReadAsync(ct))
            users.Add(ur.GetString(0));

        detail.TargetUserIds = users;
        detail.TargetCount = users.Count;
        return detail;
    }

    private static async Task<MailItem?> GetUserMailAsync(
        NpgsqlConnection conn, string projectId, string userId, Guid userMailId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT um.id, um.mail_id, m.title, m.content, m.attachments::text,
                   um.is_read, um.is_claimed, m.expire_at, um.created_at, um.claimed_at
            FROM user_mails um
            INNER JOIN mails m ON m.id = um.mail_id
            WHERE um.id = @id AND um.project_id = @project_id AND um.user_id = @user_id AND um.is_deleted = FALSE
            """;
        cmd.Parameters.AddWithValue("id", userMailId);
        cmd.Parameters.AddWithValue("project_id", projectId);
        cmd.Parameters.AddWithValue("user_id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        return MapMailItem(reader);
    }

    private static MailItem MapMailItem(NpgsqlDataReader reader)
    {
        return new MailItem
        {
            Id = reader.GetGuid(0),
            MailId = reader.GetGuid(1),
            Title = reader.GetString(2),
            Content = reader.GetString(3),
            Attachments = DeserializeAttachments(ReadText(reader, 4)),
            IsRead = reader.GetBoolean(5),
            IsClaimed = reader.GetBoolean(6),
            ExpireAt = ReadDto(reader, 7),
            CreatedAt = ReadDto(reader, 8) ?? DateTimeOffset.UtcNow,
            ClaimedAt = ReadDto(reader, 9)
        };
    }

    private static string ReadText(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return "[]";
        var v = reader.GetValue(ordinal);
        return v as string ?? v.ToString() ?? "[]";
    }

    private static DateTimeOffset? ReadDto(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var v = reader.GetValue(ordinal);
        return v switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => dt.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc))
                : new DateTimeOffset(dt),
            _ => DateTimeOffset.Parse(v.ToString()!)
        };
    }

    private static List<MailAttachment> DeserializeAttachments(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];
        try
        {
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.ListMailAttachment) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
