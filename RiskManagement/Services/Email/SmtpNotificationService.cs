using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services.Email;

/// <summary>
/// INotificationService'in gerçek SMTP implementasyonu.
/// Ayarları DB'den (ConfigService) alır — yeniden başlatma gerekmez.
/// </summary>
public sealed class SmtpNotificationService(
    AppDbContext db,
    EmailQueue queue,
    ConfigService cfg,
    ILogger<SmtpNotificationService> logger) : INotificationService
{
    private EmailSettings _cfg => cfg.GetEmailSettings();

    // ── Yeni risk önerisi → tüm risk yöneticilerine ────────────────────────
    public async Task NotifyRiskProposedAsync(int riskId, string riskCode, string riskTitle)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("risk_proposed")) return;

        var proposer = await db.Risks
            .Where(r => r.Id == riskId)
            .Select(r => r.ProposedBy!.FullName ?? r.ProposerName ?? "Anonim")
            .FirstOrDefaultAsync() ?? "Anonim";

        var (subject, html) = EmailTemplates.RiskProposed(riskCode, riskTitle, proposer, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_risk_proposed_subject"),
            cfg.Get<string>("email_tpl_risk_proposed_body"));

        var recipients = await GetRoleRecipientsAsync(
            Roles.RiskManager, Roles.Admin, Roles.Committee);

        EnqueueAll(recipients, subject, html, $"risk-proposed:{riskId}");
    }

    // ── Durum değişikliği → risk sahibine ─────────────────────────────────
    public async Task NotifyStatusChangedAsync(
        int riskId, string riskCode, string oldStatus, string newStatus, int? ownerId)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("status_changed") || ownerId == null) return;

        var riskTitle = await db.Risks
            .Where(r => r.Id == riskId)
            .Select(r => r.Title)
            .FirstOrDefaultAsync() ?? riskCode;

        var (subject, html) = EmailTemplates.StatusChanged(
            riskCode, riskTitle,
            RiskStatus.Label(oldStatus),
            RiskStatus.Label(newStatus),
            _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_status_changed_subject"),
            cfg.Get<string>("email_tpl_status_changed_body"));

        var owner = await GetUserAsync(ownerId.Value);
        if (owner != null)
            Enqueue(owner, subject, html);
    }

    // ── Risk sahibi atandı → yeni sahibine ────────────────────────────────
    public async Task NotifyOwnerAssignedAsync(
        int riskId, string riskCode, string riskTitle, int newOwnerId)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("owner_assigned")) return;

        var (subject, html) = EmailTemplates.OwnerAssigned(riskCode, riskTitle, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_owner_assigned_subject"),
            cfg.Get<string>("email_tpl_owner_assigned_body"));

        var owner = await GetUserAsync(newOwnerId);
        if (owner != null)
            Enqueue(owner, subject, html);
    }

    // ── Aksiyon vadesi → sorumlu kullanıcıya ──────────────────────────────
    public async Task NotifyActionDueSoonAsync(
        int actionId, string riskCode, string description,
        DateOnly dueDate, int? responsibleUserId)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("action_due") || responsibleUserId == null) return;

        var daysLeft = dueDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        var (subject, html) = EmailTemplates.ActionDueSoon(
            riskCode, description,
            dueDate.ToString("dd.MM.yyyy"),
            daysLeft, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_action_due_subject"),
            cfg.Get<string>("email_tpl_action_due_body"));

        var user = await GetUserAsync(responsibleUserId.Value);
        if (user != null)
            Enqueue(user, subject, html);
    }

    // ── Bulgu atama → bulgu sahibine ──────────────────────────────────────
    public async Task NotifyFindingAssignedAsync(string findingCode, string findingTitle, int ownerId)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("finding_assigned")) return;
        var (subject, html) = EmailTemplates.FindingAssigned(findingCode, findingTitle, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_finding_assigned_subject"),
            cfg.Get<string>("email_tpl_finding_assigned_body"));
        var owner = await GetUserAsync(ownerId);
        if (owner != null) Enqueue(owner, subject, html);
    }

    // ── Kapatma başvurusu → denetçiye ─────────────────────────────────────
    public async Task NotifyClosureRequestedAsync(string findingCode, string findingTitle, int auditorId)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("closure_requested")) return;
        var (subject, html) = EmailTemplates.ClosureRequested(findingCode, findingTitle, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_closure_requested_subject"),
            cfg.Get<string>("email_tpl_closure_requested_body"));
        var auditor = await GetUserAsync(auditorId);
        if (auditor != null) Enqueue(auditor, subject, html);
    }

    // ── Kapatma kararı → bulgu sahibine ───────────────────────────────────
    public async Task NotifyClosureDecidedAsync(string findingCode, string findingTitle, string decision, int ownerId)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("closure_decided")) return;
        var (subject, html) = EmailTemplates.ClosureDecided(findingCode, findingTitle, decision, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_closure_decided_subject"),
            cfg.Get<string>("email_tpl_closure_decided_body"));
        var owner = await GetUserAsync(ownerId);
        if (owner != null) Enqueue(owner, subject, html);
    }

    // ── Bulgu vadesi yaklaşıyor → bulgu sahibine ──────────────────────────
    public async Task NotifyFindingDueSoonAsync(string findingCode, string findingTitle, DateOnly dueDate, int ownerId)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("finding_due")) return;
        var daysLeft = dueDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        var (subject, html) = EmailTemplates.FindingDueSoon(
            findingCode, findingTitle, dueDate.ToString("dd.MM.yyyy"), daysLeft, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_finding_due_subject"),
            cfg.Get<string>("email_tpl_finding_due_body"));
        var owner = await GetUserAsync(ownerId);
        if (owner != null) Enqueue(owner, subject, html);
    }

    // ── Yeni etik bildirim → etik kurulu + denetim yöneticilerine ────────
    public async Task NotifyEthicsSubmittedAsync(string ethicsCode, string subject)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("ethics_submitted")) return;
        var (emailSubject, html) = EmailTemplates.EthicsSubmitted(ethicsCode, subject, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_ethics_submitted_subject"),
            cfg.Get<string>("email_tpl_ethics_submitted_body"));
        var recipients = await GetRoleRecipientsAsync(Roles.EthicsBoard, Roles.AuditManager, Roles.Admin);
        EnqueueAll(recipients, emailSubject, html, $"ethics-submitted:{ethicsCode}");
    }

    // ── Etik bildirim incelendi → sonraki aşamadakilere ───────────────────
    public async Task NotifyEthicsReviewedAsync(string ethicsCode, string subject, string stage)
    {
        if (!_cfg.Enabled || !cfg.IsEmailEventEnabled("ethics_reviewed")) return;
        var stageLabel = stage == "audit" ? "Denetim İncelemesi" : "Kurul İncelemesi";
        var (emailSubject, html) = EmailTemplates.EthicsReviewed(ethicsCode, subject, stageLabel, _cfg.BaseUrl,
            cfg.Get<string>("email_tpl_ethics_reviewed_subject"),
            cfg.Get<string>("email_tpl_ethics_reviewed_body"));
        // audit review → etik kuruluna; board review → denetim yöneticilerine
        var targetRoles = stage == "audit"
            ? new[] { Roles.EthicsBoard }
            : new[] { Roles.AuditManager, Roles.Admin };
        var recipients = await GetRoleRecipientsAsync(targetRoles);
        EnqueueAll(recipients, emailSubject, html, $"ethics-reviewed:{ethicsCode}");
    }

    // ── Test e-postası (admin panelinden tetiklenir) ───────────────────────
    public Task SendTestAsync(string toAddress, string toName)
    {
        var (subject, html) = EmailTemplates.Test(_cfg.BaseUrl,
            cfg.Get<string>("email_tpl_test_subject"),
            cfg.Get<string>("email_tpl_test_body"));
        queue.Enqueue(new EmailMessage(toAddress, toName, subject, html));
        logger.LogInformation("Test e-postası kuyruğa eklendi: {To}", toAddress);
        return Task.CompletedTask;
    }

    // ── Yardımcılar ────────────────────────────────────────────────────────

    private async Task<List<(string Email, string Name)>> GetRoleRecipientsAsync(
        params string[] roles)
    {
        // Birden fazla rol atanmış kullanıcıları da kapsa
        var fromPrimary = await db.Users
            .Where(u => u.IsActive && roles.Contains(u.Role)
                     && u.Email != null && u.Email != "")
            .Select(u => new { u.Email, u.FullName })
            .ToListAsync();

        var fromRoles = await db.UserRoles
            .Where(ur => roles.Contains(ur.RoleName))
            .Join(db.Users.Where(u => u.IsActive && u.Email != null && u.Email != ""),
                  ur => ur.UserId, u => u.Id,
                  (ur, u) => new { u.Email, u.FullName })
            .ToListAsync();

        return fromPrimary.Concat(fromRoles)
            .DistinctBy(x => x.Email)
            .Select(x => (x.Email!, x.FullName))
            .ToList();
    }

    private async Task<(string Email, string Name)?> GetUserAsync(int userId)
    {
        var user = await db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.Email, u.FullName })
            .FirstOrDefaultAsync();

        if (user == null || string.IsNullOrEmpty(user.Email)) return null;
        return (user.Email, user.FullName);
    }

    private void Enqueue((string Email, string Name)? recipient,
        string subject, string html)
    {
        if (recipient == null) return;
        queue.Enqueue(new EmailMessage(recipient.Value.Email, recipient.Value.Name, subject, html));
    }

    private void EnqueueAll(List<(string Email, string Name)> recipients,
        string subject, string html, string context)
    {
        foreach (var r in recipients)
            queue.Enqueue(new EmailMessage(r.Email, r.Name, subject, html));

        if (recipients.Count == 0)
            logger.LogDebug("Bildirim alıcısı bulunamadı: {Context}", context);
    }
}
