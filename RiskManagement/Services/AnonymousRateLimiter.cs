using Microsoft.Extensions.Caching.Memory;

namespace RiskManagement.Services;

/// <summary>
/// Blazor Server'da SignalR üzerinden gelen anonim risk önerileri için
/// IP tabanlı rate limiting. Middleware'e bağlanamayan sayfalarda kullanılır.
/// </summary>
public class AnonymousRateLimiter(IMemoryCache cache)
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public bool IsAllowed(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress)) return true;

        var key = $"anon_risk:{ipAddress}";
        var count = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return 0;
        });

        if (count >= MaxAttempts) return false;

        cache.Set(key, count + 1, Window);
        return true;
    }

    public TimeSpan GetRetryAfter(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress)) return TimeSpan.Zero;
        var key = $"anon_risk:{ipAddress}";
        if (cache.TryGetValue<AbsoluteExpiry>(key + "_exp", out var expiry))
            return expiry.ExpiresAt - DateTime.UtcNow;
        return Window;
    }

    private record AbsoluteExpiry(DateTime ExpiresAt);
}
