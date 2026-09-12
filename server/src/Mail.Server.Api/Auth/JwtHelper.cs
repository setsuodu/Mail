namespace Mail.Server.Api.Auth;

/// <summary>
/// Thin wrapper: extract Bearer token → SimpleJwt (MP-compatible HS256).
/// </summary>
public static class JwtHelper
{
    public static string? TryGetUserId(HttpContext http, string? jwtSecret)
    {
        if (string.IsNullOrEmpty(jwtSecret))
        {
            Console.Error.WriteLine("[Mail] Auth:JwtSecret is empty");
            return null;
        }

        var auth = http.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("[Mail] Missing Authorization Bearer");
            return null;
        }

        var token = auth["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return null;

        var jwt = new SimpleJwt(jwtSecret);
        if (!jwt.TryValidate(token, out var claims) || claims?.UserKey is null)
        {
            Console.Error.WriteLine("[Mail] JWT validate failed (sig / exp / missing sub)");
            return null;
        }

        return claims.UserKey;
    }
}
