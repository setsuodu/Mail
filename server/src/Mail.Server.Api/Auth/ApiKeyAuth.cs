namespace Mail.Server.Api.Auth;

public static class ApiKeyAuth
{
    public const string AdminHeader = "X-Admin-Api-Key";

    public static bool ValidateAdmin(HttpContext http, string expectedKey)
    {
        if (string.IsNullOrEmpty(expectedKey))
            return false;
        if (!http.Request.Headers.TryGetValue(AdminHeader, out var provided))
            return false;
        return string.Equals(provided.ToString(), expectedKey, StringComparison.Ordinal);
    }

    public static IResult Unauthorized() =>
        Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
}
