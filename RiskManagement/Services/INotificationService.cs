namespace RiskManagement.Services;

/// <summary>
/// Risk yönetimi olaylarında bildirim gönderir.
/// Gerçek e-posta/SMS implementasyonu için bu arayüzü implement edin ve DI'a kaydedin.
/// </summary>
public interface INotificationService
{
    /// <summary>Yeni risk önerisi geldiğinde risk yöneticilerine bildirir.</summary>
    Task NotifyRiskProposedAsync(int riskId, string riskCode, string riskTitle);

    /// <summary>Risk durumu değiştiğinde risk sahibine ve ilgililere bildirir.</summary>
    Task NotifyStatusChangedAsync(int riskId, string riskCode, string oldStatus, string newStatus, int? ownerId);

    /// <summary>Risk sahibi atandığında ilgili kullanıcıya bildirir.</summary>
    Task NotifyOwnerAssignedAsync(int riskId, string riskCode, string riskTitle, int newOwnerId);

    /// <summary>Aksiyon planı vadesi yaklaştığında sorumluya bildirir (N gün kala).</summary>
    Task NotifyActionDueSoonAsync(int actionId, string riskCode, string description, DateOnly dueDate, int? responsibleUserId);
}
