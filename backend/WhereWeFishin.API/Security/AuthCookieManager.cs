using Microsoft.AspNetCore.Http;

namespace WhereWeFishin.API.Security;

public static class AuthCookieManager
{
    public const string CookieName = "auth";

    public static void SetAuthCookie(HttpResponse response, string token, DateTime expiresAtUtc, bool isHttps)
    {
        response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = new DateTimeOffset(expiresAtUtc.ToUniversalTime())
        });
    }

    public static void ExpireAuthCookie(HttpResponse response)
    {
        response.Headers.Append("Set-Cookie", $"{CookieName}=; Max-Age=0; Path=/");
    }

    public static string? ReadTokenFromRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            const string bearerPrefix = "Bearer ";
            var headerValue = authorizationHeader.ToString();
            if (headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = headerValue[bearerPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    return token;
                }
            }
        }

        if (request.Cookies.TryGetValue(CookieName, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken;
        }

        return null;
    }
}
