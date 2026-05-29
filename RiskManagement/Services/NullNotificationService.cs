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

    public Task NotifyFindingAssignedAsync(string findingCode, string findingTitle, int ownerId)
    {
        logger.LogInformation("Bildirim (stub): Bulgu atandı {Code} → userId={Owner}", findingCode, ownerId);
        return Task.CompletedTask;
    }

    public Task NotifyClosureRequestedAsync(string findingCode, string findingTitle, int auditorId)
    {
        logger.LogInformation("Bildirim (stub): Kapatma başvurusu {Code} → userId={Auditor}", findingCode, auditorId);
        return Task.CompletedTask;
    }

    public Task NotifyClosureDecidedAsync(string findingCode, string findingTitle, string decision, int ownerId)
    {
        logger.LogInformation("Bildirim (stub): Kapatma kararı {Code} {Decision} → userId={Owner}", findingCode, decision, ownerId);
        return Task.CompletedTask;
    }

    public Task NotifyFindingDueSoonAsync(string findingCode, string findingTitle, DateOnly dueDate, int ownerId)
    {
        logger.LogInformation("Bildirim (stub): Bulgu vadesi yaklaşıyor {Code} {Date}", findingCode, dueDate);
        return Task.CompletedTask;
    }

    public Task NotifyEthicsSubmittedAsync(string ethicsCode, string subject)
    {
        logger.LogInformation("Bildirim (stub): Etik bildirim {Code}", ethicsCode);
        return Task.CompletedTask;
    }

    public Task NotifyEthicsReviewedAsync(string ethicsCode, string subject, string stage)
    {
        logger.LogInformation("Bildirim (stub): Etik inceleme {Code} aşama={Stage}", ethicsCode, stage);
        return Task.CompletedTask;
    }
}
