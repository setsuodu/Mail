using Mail.Server.Api.Auth;
using Mail.Server.Api.Json;
using Mail.Server.Api.Models;
using Mail.Server.Api.Storage;

namespace Mail.Server.Api.Endpoints;

public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/inbox");

        group.MapGet("/", HandleList);
        group.MapPost("/{id:guid}/read", HandleMarkRead);
        group.MapPost("/{id:guid}/claim", HandleClaim);
        group.MapDelete("/{id:guid}", HandleDelete);
    }

    private static async Task<IResult> HandleList(
        string? projectId,
        bool? includeClaimed,
        int? page,
        int? pageSize,
        IMailStore store,
        IConfiguration config,
        HttpContext http)
    {
        var userId = JwtHelper.TryGetUserId(http, config["Auth:JwtSecret"]);
        if (userId is null)
            return ApiKeyAuth.Unauthorized();

        var pid = projectId ?? config["DefaultProjectId"] ?? "default";
        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is > 0 and <= 100 ? pageSize.Value : 50;
        var include = includeClaimed ?? true;

        var (items, total) = await store.ListInboxAsync(pid, userId, include, p, ps, http.RequestAborted);
        return Results.Ok(new InboxListResponse
        {
            Items = items,
            Page = p,
            PageSize = ps,
            Total = total
        });
    }

    private static async Task<IResult> HandleMarkRead(
        Guid id,
        string? projectId,
        IMailStore store,
        IConfiguration config,
        HttpContext http)
    {
        var userId = JwtHelper.TryGetUserId(http, config["Auth:JwtSecret"]);
        if (userId is null)
            return ApiKeyAuth.Unauthorized();

        var pid = projectId ?? config["DefaultProjectId"] ?? "default";
        var item = await store.MarkReadAsync(pid, userId, id, http.RequestAborted);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> HandleClaim(
        Guid id,
        string? projectId,
        IMailStore store,
        IConfiguration config,
        HttpContext http)
    {
        var userId = JwtHelper.TryGetUserId(http, config["Auth:JwtSecret"]);
        if (userId is null)
            return ApiKeyAuth.Unauthorized();

        var pid = projectId ?? config["DefaultProjectId"] ?? "default";
        var result = await store.ClaimAsync(pid, userId, id, http.RequestAborted);
        if (result is null)
        {
            // Distinguish not found vs expired is simplified: store returns null for both;
            // re-check existence if needed. For v1 return 404.
            return Results.Json(new ErrorResponse { Error = "mail not found or expired" }, AppJsonContext.Default.ErrorResponse, statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDelete(
        Guid id,
        string? projectId,
        IMailStore store,
        IConfiguration config,
        HttpContext http)
    {
        var userId = JwtHelper.TryGetUserId(http, config["Auth:JwtSecret"]);
        if (userId is null)
            return ApiKeyAuth.Unauthorized();

        var pid = projectId ?? config["DefaultProjectId"] ?? "default";
        var ok = await store.SoftDeleteAsync(pid, userId, id, http.RequestAborted);
        return ok ? Results.NoContent() : Results.NotFound();
    }
}
