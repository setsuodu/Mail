using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mail.Server.Api.Auth;

/// <summary>
/// 与 MP.Auth SimpleJwt 同构：极简 HS256，全平台共用 Secret。
/// 见 https://github.com/setsuodu/MP — Infrastructure/Jwt/SimpleJwt.cs
/// </summary>
public sealed class SimpleJwt(string secret)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(secret);

    public bool TryValidate(string token, out MailJwtClaims? claims)
    {
        claims = null;
        var parts = token.Split('.');
        if (parts.Length != 3) return false;

        var signingInput = $"{parts[0]}.{parts[1]}";
        var expectedSig = Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(signingInput)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSig), Encoding.UTF8.GetBytes(parts[2])))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize(
                Base64UrlDecode(parts[1]),
                MailJwtJsonContext.Default.MailJwtClaims);
            if (parsed is null) return false;
            if (parsed.Exp > 0 && parsed.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                return false;
            claims = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}

public sealed class MailJwtClaims
{
    [JsonPropertyName("sub")]
    public string? Sub { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("exp")]
    public long Exp { get; set; }

    [JsonPropertyName("iss")]
    public string? Iss { get; set; }

    public string? UserKey =>
        !string.IsNullOrWhiteSpace(Sub) ? Sub
        : !string.IsNullOrWhiteSpace(UserId) ? UserId
        : null;
}

[JsonSerializable(typeof(MailJwtClaims))]
internal partial class MailJwtJsonContext : JsonSerializerContext
{
}
