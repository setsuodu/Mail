using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Setsuodu.Mail.Samples
{
    /// <summary>
    /// Minimal HS256 JWT for local sample only (matches server Auth__JwtSecret).
    /// Not a production auth library.
    /// </summary>
    public static class SampleJwt
    {
        public static string CreateHs256(string userId, string secret, int expiresSeconds = 86400)
        {
            if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId required");
            if (string.IsNullOrEmpty(secret)) throw new ArgumentException("secret required");

            var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payloadJson =
                "{\"sub\":\"" + Escape(userId) + "\",\"iat\":" + now + ",\"exp\":" + (now + expiresSeconds) + "}";
            var payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
            var data = header + "." + payload;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var sig = Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
            return data + "." + sig;
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
