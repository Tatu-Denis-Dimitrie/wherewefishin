using Microsoft.Extensions.Caching.Memory;

namespace WhereWeFishin.API.Security;

public class InMemoryTokenRevocationService : ITokenRevocationService
{
    private static readonly TimeSpan MinimumRetention = TimeSpan.FromMinutes(5);
    private readonly IMemoryCache _cache;

    public InMemoryTokenRevocationService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void RevokeToken(string jti, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        var expiresAt = expiresAtUtc <= DateTime.UtcNow
            ? DateTimeOffset.UtcNow.Add(MinimumRetention)
            : new DateTimeOffset(expiresAtUtc.ToUniversalTime());

        _cache.Set(GetCacheKey(jti), true, expiresAt);
    }

    public bool IsTokenRevoked(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        return _cache.TryGetValue(GetCacheKey(jti), out _);
    }

    private static string GetCacheKey(string jti) => $"revoked-token:{jti}";
}
