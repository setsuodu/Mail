using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mail.Server.Api.Json;
using Mail.Server.Api.Models;

namespace Mail.Server.Api.Auth;

/// <summary>
/// HS256 validate matching client SampleJwt (no kid). No IdentityModel dependency.
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

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                Console.Error.WriteLine("[Mail] JWT format invalid");
                return null;
            }

            var signingInput = Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]);
            var actualSig = Base64UrlDecode(parts[2]);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(jwtSecret));
            var expectedSig = hmac.ComputeHash(signingInput);
            if (!CryptographicOperations.FixedTimeEquals(actualSig, expectedSig))
            {
                Console.Error.WriteLine("[Mail] JWT HMAC signature mismatch");
                return null;
            }

            var payloadBytes = Base64UrlDecode(parts[1]);
            var payload = JsonSerializer.Deserialize(payloadBytes, AppJsonContext.Default.JwtPayloadLite);
            if (payload is null)
            {
                Console.Error.WriteLine("[Mail] JWT payload deserialize null");
                return null;
            }

            if (payload.Exp > 0)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now > payload.Exp + 120)
                {
                    Console.Error.WriteLine("[Mail] JWT expired");
                    return null;
                }
            }

            var id = !string.IsNullOrWhiteSpace(payload.Sub) ? payload.Sub
                : !string.IsNullOrWhiteSpace(payload.UserId) ? payload.UserId
                : null;
            if (id is null)
                Console.Error.WriteLine("[Mail] JWT missing sub/user_id");
            return id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[Mail] JWT validate failed: " + ex.Message);
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
