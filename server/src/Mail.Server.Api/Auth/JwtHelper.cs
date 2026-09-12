using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Mail.Server.Api.Auth;

public static class JwtHelper
{
    /// <summary>
    /// Extract user id from Bearer JWT. Accepts claim types: sub, user_id, uid.
    /// Returns null if missing/invalid.
    /// </summary>
    public static string? TryGetUserId(HttpContext http, string? jwtSecret)
    {
        if (string.IsNullOrEmpty(jwtSecret))
            return null;

        var auth = http.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = auth["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var principal = handler.ValidateToken(token, parameters, out _);
            var id = principal.FindFirstValue("sub")
                     ?? principal.FindFirstValue("user_id")
                     ?? principal.FindFirstValue("uid")
                     ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        catch
        {
            return null;
        }
    }
}
