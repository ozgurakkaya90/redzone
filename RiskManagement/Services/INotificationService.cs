namespace RiskManagement.Services;

public interface INotificationService
{
    // ── Risk ──────────────────────────────────────────────────────────────────
    Task NotifyRiskProposedAsync(int riskId, string riskCode, string riskTitle);
    Task NotifyStatusChangedAsync(int riskId, string riskCode, string oldStatus, string newStatus, int? ownerId);
    Task NotifyOwnerAssignedAsync(int riskId, string riskCode, string riskTitle, int newOwnerId);
    Task NotifyActionDueSoonAsync(int actionId, string riskCode, string description, DateOnly dueDate, int? responsibleUserId);

    // ── Denetim ───────────────────────────────────────────────────────────────
    Task NotifyFindingAssignedAsync(string findingCode, string findingTitle, int ownerId);
    Task NotifyClosureRequestedAsync(string findingCode, string findingTitle, int auditorId);
    Task NotifyClosureDecidedAsync(string findingCode, string findingTitle, string decision, int ownerId);
    Task NotifyFindingDueSoonAsync(string findingCode, string findingTitle, DateOnly dueDate, int ownerId);

    // ── Etik ──────────────────────────────────────────────────────────────────
    Task NotifyEthicsSubmittedAsync(string ethicsCode, string subject);
    Task NotifyEthicsReviewedAsync(string ethicsCode, string subject, string stage);

    // ── Test ──────────────────────────────────────────────────────────────────
    Task SendTestAsync(string toAddress, string toName);
}
