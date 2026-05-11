namespace RiskManagement.Services;

/// <summary>
/// Varsayılan no-op implementasyon — bildirim altyapısı kurulana kadar sessizce çalışır.
/// Gerçek e-posta için SmtpNotificationService gibi bir sınıf yazıp DI'da değiştirin.
/// </summary>
public class NullNotificationService(ILogger<NullNotificationService> logger) : INotificationService
{
    public Task NotifyRiskProposedAsync(int riskId, string riskCode, string riskTitle)
    {
        logger.LogInformation("Bildirim (stub): Yeni risk önerisi {Code} — {Title}", riskCode, riskTitle);
        return Task.CompletedTask;
    }

    public Task NotifyStatusChangedAsync(int riskId, string riskCode, string oldStatus, string newStatus, int? ownerId)
    {
        logger.LogInformation("Bildirim (stub): {Code} durumu {Old} → {New}", riskCode, oldStatus, newStatus);
        return Task.CompletedTask;
    }

    public Task NotifyOwnerAssignedAsync(int riskId, string riskCode, string riskTitle, int newOwnerId)
    {
        logger.LogInformation("Bildirim (stub): {Code} sahibi userId={Owner}", riskCode, newOwnerId);
        return Task.CompletedTask;
    }

    public Task NotifyActionDueSoonAsync(int actionId, string riskCode, string description, DateOnly dueDate, int? responsibleUserId)
    {
        logger.LogInformation("Bildirim (stub): {Code} aksiyonu {Date} vadeli", riskCode, dueDate);
        return Task.CompletedTask;
    }
}
