using System.Text.Json;

namespace RiskManagement.Services;

/// <summary>
/// GitHub Releases API üzerinden son sürümü kontrol eder.
/// Opsiyonel: config'den kapatılabilir. İnternet yoksa sessizce null döner.
/// HttpClient IHttpClientFactory üzerinden yönetilir (socket exhaustion koruması).
/// </summary>
public class UpdateCheckService(ConfigService config, ILogger<UpdateCheckService> logger, HttpClient http)
{
    private const string ApiUrl = "https://api.github.com/repos/ozgurakkaya90/redzone/releases/latest";

    /// <summary>Mevcut uygulamanın sürümünü döner.</summary>
    public static string CurrentVersion()
    {
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return ver is null ? "0.0.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }

    /// <summary>
    /// GitHub'dan en son release tag'ını getirir.
    /// - config'de update_check_enabled=false ise null döner
    /// - İnternet yoksa veya hata varsa sessizce null döner (3s timeout)
    /// </summary>
    public async Task<string?> GetLatestVersionAsync()
    {
        var raw = config.Get<string>("update_check_enabled");
        if (raw is "false") return null;

        try
        {
            var json = await http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tag_name", out var tag))
            {
                var tagName = tag.GetString() ?? "";
                return tagName.TrimStart('v');
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug("Güncelleme kontrolü yapılamadı (internet veya timeout): {Msg}", ex.Message);
        }
        return null;
    }

    /// <summary>
    /// Mevcut sürümden daha yeni bir sürüm var mı?
    /// null döndüğünde kontrol yapılamadı anlamına gelir.
    /// </summary>
    public async Task<bool?> IsUpdateAvailableAsync()
    {
        var latest = await GetLatestVersionAsync();
        if (latest is null) return null;

        var current = CurrentVersion();
        try
        {
            var latestV  = new Version(latest);
            var currentV = new Version(current);
            return latestV > currentV;
        }
        catch
        {
            return null;
        }
    }
}
