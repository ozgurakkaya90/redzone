using Microsoft.Extensions.Caching.Memory;

namespace RiskManagement.Services;

/// <summary>
/// Blazor Server'da SignalR üzerinden gelen anonim risk önerileri için
/// IP tabanlı rate limiting. Middleware'e bağlanamayan sayfalarda kullanılır.
/// </summary>
public class AnonymousRateLimiter(IMemoryCache cache)
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan WindowLength = TimeSpan.FromMinutes(10);
    private readonly object _lock = new();

    public bool IsAllowed(string? ipAddress, string bucket = "anon_risk")
    {
        if (string.IsNullOrEmpty(ipAddress)) return true;

        var key = $"{bucket}:{ipAddress}";

        // Lock prevents TOCTOU race: another request could slip through between
        // reading the count and writing the incremented value.
        lock (_lock)
        {
            // Pencere bitişi ilk istekte sabitlenir; sonraki istekler onu kaydırmaz.
            // Aksi halde her denemede AbsoluteExpiration yenilenir ve saldırgan
            // pencereyi süresiz kaydırarak "Window içinde MaxAttempts" garantisini deler.
            var window = cache.Get<Window>(key)
                ?? new Window { Count = 0, ExpiresAt = DateTimeOffset.UtcNow + WindowLength };

            if (window.Count >= MaxAttempts) return false;

            window.Count++;
            cache.Set(key, window, new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = window.ExpiresAt
            });
            return true;
        }
    }

    private sealed class Window
    {
        public int Count;
        public DateTimeOffset ExpiresAt;
    }
}
