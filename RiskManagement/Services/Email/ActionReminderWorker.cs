using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services.Email;

/// <summary>
/// Her gün bir kez çalışır; vadesi yaklaşan aksiyon planları için sorumlu kullanıcıya
/// hatırlatma e-postası gönderir. "action_due_remind_days" ConfigService'ten okunur.
/// </summary>
public sealed class ActionReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ActionReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // İlk çalışmada 1 dakika bekle — uygulama tam ayağa kalksın
        await Task.Delay(TimeSpan.FromMinutes(1), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ActionReminderWorker hatası");
            }

            // Bir sonraki çalışmaya kadar 24 saat bekle
            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var cfg      = scope.ServiceProvider.GetRequiredService<ConfigService>();
        var db       = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var remindDays = cfg.GetActionDueRemindDays();
        var today      = DateOnly.FromDateTime(DateTime.Today);
        var deadline   = today.AddDays(remindDays);

        // Risk aksiyon hatırlatmaları
        if (cfg.IsEmailEventEnabled("action_due"))
        {
            var upcoming = await db.ActionPlans
                .Include(a => a.Risk)
                .Where(a => a.DueDate.HasValue
                         && a.DueDate.Value >= today
                         && a.DueDate.Value <= deadline
                         && (a.Status == ActionStatus.Planned || a.Status == ActionStatus.InProgress))
                .ToListAsync(ct);

            foreach (var a in upcoming)
                await notifier.NotifyActionDueSoonAsync(a.Id, a.Risk.Code, a.Description, a.DueDate!.Value, a.CreatedById);

            if (upcoming.Count > 0)
                logger.LogInformation("ActionReminderWorker: {Count} risk aksiyon hatırlatması gönderildi", upcoming.Count);
        }

        // Denetim bulgusu vadesi hatırlatmaları
        if (cfg.IsEmailEventEnabled("finding_due"))
        {
            var findings = await db.AuditFindings
                .Where(f => f.DueDate.HasValue
                         && f.DueDate.Value >= today
                         && f.DueDate.Value <= deadline
                         && f.OwnerId.HasValue
                         && f.Status != FindingStatus.Closed && f.Status != FindingStatus.ClosureRequested)
                .ToListAsync(ct);

            foreach (var f in findings)
                await notifier.NotifyFindingDueSoonAsync(f.Code, f.Title, f.DueDate!.Value, f.OwnerId!.Value);

            if (findings.Count > 0)
                logger.LogInformation("ActionReminderWorker: {Count} bulgu vadesi hatırlatması gönderildi", findings.Count);
        }
    }
}
