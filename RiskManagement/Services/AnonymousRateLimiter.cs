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
    private readonly object _lock = new();

    public bool IsAllowed(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress)) return true;

        var key = $"anon_risk:{ipAddress}";

        // Lock prevents TOCTOU race: another request could slip through between
        // reading the count and writing the incremented value.
        lock (_lock)
        {
            var count = cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Window;
                return 0;
            });

            if (count >= MaxAttempts) return false;

            cache.Set(key, count + 1, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Window
            });
            return true;
        }
    }

}
