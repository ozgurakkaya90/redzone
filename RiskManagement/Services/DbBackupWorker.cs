namespace RiskManagement.Services;

/// <summary>
/// Yapılandırılan saatte günde bir kez DB yedeği alır (admin Sistem Yapılandırması'ndan açılır).
/// 15 dakikada bir kontrol eder; saat geldiyse ve o gün henüz alınmadıysa çalışır. Tarih-guard'ı
/// (backup_last_run) sayesinde app pool recycle/restart'tan etkilenmez. Başarısızsa o günü
/// işaretlemez → bir sonraki kontrol (geçici DB blip'i geçince) yeniden dener.
/// </summary>
public sealed class DbBackupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DbBackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("DB yedekleme servisi başladı.");
        await Task.Delay(TimeSpan.FromMinutes(2), ct); // uygulama tam ayağa kalksın

        while (!ct.IsCancellationRequested)
        {
            try { Tick(); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "DbBackupWorker hatası");
            }
            await Task.Delay(TimeSpan.FromMinutes(15), ct);
        }
    }

    private void Tick()
    {
        using var scope = scopeFactory.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<ConfigService>();

        if (!cfg.Get<bool>("backup_enabled")) return;

        var timeStr = cfg.Get<string>("backup_time");
        if (!TimeSpan.TryParse(string.IsNullOrWhiteSpace(timeStr) ? "02:00" : timeStr, out var schedule))
            schedule = new TimeSpan(2, 0, 0);

        var todayKey = DateTime.Today.ToString("yyyyMMdd");
        if (cfg.Get<string>("backup_last_run") == todayKey) return; // bugün alındı
        if (DateTime.Now.TimeOfDay < schedule) return;              // saat henüz gelmedi

        var svc    = scope.ServiceProvider.GetRequiredService<DbBackupService>();
        var result = svc.Run();

        cfg.Set("backup_last_at", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
        if (result.Success)
        {
            // Başarıda bugünü işaretle — bir daha alma. Başarısızsa işaretleme → tekrar denenir.
            cfg.Set("backup_last_run", todayKey);
            cfg.Set("backup_last_status",
                $"OK — {System.IO.Path.GetFileName(result.FilePath)} ({Math.Round(result.SizeBytes / 1024.0 / 1024.0, 2)} MB)");
        }
        else
        {
            cfg.Set("backup_last_status", $"HATA — {result.Error}");
        }
    }
}
