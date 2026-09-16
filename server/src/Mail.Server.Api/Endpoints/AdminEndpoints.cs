using Mail.Server.Api.Auth;
using Mail.Server.Api.Json;
using Mail.Server.Api.Models;
using Mail.Server.Api.Storage;

namespace Mail.Server.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/mails");

        group.MapPost("/", HandleCreate);
        group.MapGet("/", HandleList);
        group.MapGet("/{id:guid}", HandleGet);
    }

    private static async Task<IResult> HandleCreate(
        AdminCreateMailRequest body,
        IMailStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:AdminApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateAdmin(http, key))
            return ApiKeyAuth.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.ProjectId) ||
            string.IsNullOrWhiteSpace(body.Title) ||
            string.IsNullOrWhiteSpace(body.Content))
        {
            return Results.Json(new ErrorResponse { Error = "projectId, title, content are required" }, AppJsonContext.Default.ErrorResponse, statusCode: StatusCodes.Status400BadRequest);
        }

        var isBroadcast = body.Broadcast;
        if (!isBroadcast && (body.TargetUserIds is null || body.TargetUserIds.Count == 0))
        {
            return Results.Json(new ErrorResponse { Error = "targetUserIds required unless broadcast=true" }, AppJsonContext.Default.ErrorResponse, statusCode: StatusCodes.Status400BadRequest);
        }

        var summary = await store.CreateMailAsync(body, createdBy: "admin", http.RequestAborted);
        return Results.Created($"/api/v1/admin/mails/{summary.Id}", summary);
    }

    private static async Task<IResult> HandleList(
        string? projectId,
        int? page,
        int? pageSize,
        IMailStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:AdminApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateAdmin(http, key))
            return ApiKeyAuth.Unauthorized();

        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is > 0 and <= 100 ? pageSize.Value : 20;

        var (items, total) = await store.AdminListAsync(projectId, p, ps, http.RequestAborted);
        return Results.Ok(new AdminMailListResponse
        {
            Items = items,
            Page = p,
            PageSize = ps,
            Total = total
        });
    }

    private static async Task<IResult> HandleGet(
        Guid id,
        IMailStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:AdminApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateAdmin(http, key))
            return ApiKeyAuth.Unauthorized();

        var detail = await store.AdminGetAsync(id, http.RequestAborted);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }
}
