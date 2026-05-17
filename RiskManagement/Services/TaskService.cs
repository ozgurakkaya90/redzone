using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;

namespace RiskManagement.Services;

public record UserTask(
    string Icon,
    string Title,
    string Detail,
    string Url,
    string Color,   // CSS color for the left border / badge
    string Bg,
    string Category,
    string Priority = "normal"   // "urgent" | "normal"
);

public class TaskService(AppDbContext db, ConfigService config)
{
    public List<UserTask> GetTasksForUser(int userId, string role)
    {
        var tasks = new List<UserTask>();

        // ── Risk tasks ──────────────────────────────────────────────

        if (role is "admin" or "committee")
        {
            // Risks proposed, waiting for review
            var proposed = db.Risks
                .Where(r => r.Status == "proposed")
                .Select(r => new { r.Id, r.Code, r.Title })
                .ToList();
            foreach (var r in proposed)
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "İnceleme bekliyor", $"/risk/{r.Id}",
                    "#7c3aed", "#faf5ff", "Risk İnceleme"));

            // Risks awaiting committee approval
            var awaitingApproval = db.Risks
                .Where(r => r.Status == "awaiting_approval")
                .Select(r => new { r.Id, r.Code, r.Title })
                .ToList();
            foreach (var r in awaitingApproval)
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Komite onayı bekliyor", $"/risk/{r.Id}",
                    "#7c3aed", "#faf5ff", "Risk Onayı"));

            // Risks under review, waiting for initial evaluation
            var underReview = db.Risks
                .Where(r => r.Status == "under_review")
                .Select(r => new { r.Id, r.Code, r.Title })
                .ToList();
            foreach (var r in underReview)
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "İlk değerlendirme bekliyor", $"/risk/{r.Id}",
                    "#0369a1", "#eff6ff", "Risk Değerlendirme"));

            // Residual evaluation needed
            var controlled = db.Risks
                .Where(r => r.Status == "controlled")
                .Select(r => new { r.Id, r.Code, r.Title })
                .ToList();
            foreach (var r in controlled)
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Kalıntı risk değerlendirmesi bekliyor", $"/risk/{r.Id}",
                    "#0f766e", "#f0fdfa", "Risk Değerlendirme"));
        }

        if (role is "admin" or "risk_owner" or "committee")
        {
            // Approved risks waiting for strategy + responsible unit
            var approved = db.Risks
                .Where(r => r.Status == "approved"
                    && (r.OwnerId == userId || role == "admin" || role == "committee"))
                .Select(r => new { r.Id, r.Code, r.Title })
                .ToList();
            foreach (var r in approved)
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Strateji ve sorumlu birim belirlenmesi gerekiyor", $"/risk/{r.Id}",
                    "#92400e", "#fffbeb", "Strateji Belirleme"));

            // Strategy set, waiting for controls
            var strategySet = db.Risks
                .Where(r => r.Status == "strategy_set"
                    && (r.OwnerId == userId || role == "admin" || role == "committee"))
                .Select(r => new { r.Id, r.Code, r.Title })
                .ToList();
            foreach (var r in strategySet)
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Kontrol eklenmesi gerekiyor", $"/risk/{r.Id}",
                    "#0369a1", "#eff6ff", "Kontrol Ekleme"));

            // Residual evaluated, waiting for action plan
            var residualEval = db.Risks
                .Where(r => r.Status == "residual_evaluated"
                    && (r.OwnerId == userId || role == "admin" || role == "committee"))
                .Select(r => new { r.Id, r.Code, r.Title })
                .ToList();
            foreach (var r in residualEval)
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Aksiyon planı oluşturulması gerekiyor", $"/risk/{r.Id}",
                    "#166534", "#f0fdf4", "Aksiyon Planı"));
        }

        // Overdue action plans the user created or is responsible for
        var overdueActions = db.ActionPlans
            .Include(a => a.Risk)
            .Where(a => a.Status == "planned"
                && a.DueDate < DateOnly.FromDateTime(DateTime.Today)
                && (a.CreatedById == userId || role == "admin"))
            .Select(a => new { a.RiskId, RiskCode = a.Risk.Code, RiskTitle = a.Risk.Title,
                               a.Description, a.DueDate })
            .ToList();
        foreach (var a in overdueActions)
            tasks.Add(new("", $"{a.RiskCode} — {a.Description}",
                $"Aksiyon vadesi geçti: {a.DueDate:dd.MM.yyyy}", $"/risk/{a.RiskId}",
                "#991b1b", "#fef2f2", "Geciken Aksiyon", "urgent"));

        // ── Audit tasks ─────────────────────────────────────────────

        if (role is "admin" or "audit_manager")
        {
            // Closure requests pending approval
            var closureRequests = db.ClosureRequests
                .Include(c => c.Finding)
                .Where(c => c.Status == "pending")
                .Select(c => new { c.Id, c.FindingId, c.Finding.Code, c.Finding.Title })
                .ToList();
            foreach (var c in closureRequests)
                tasks.Add(new("", $"{c.Code} — {c.Title}",
                    "Kapanış başvurusu onay bekliyor", $"/audit/findings/{c.FindingId}",
                    "#065f46", "#f0fdf4", "Kapanış Onayı"));
        }

        if (role is "admin" or "auditor" or "audit_manager")
        {
            // Open findings the user is the auditor on
            var myOpenFindings = db.AuditFindings
                .Where(f => f.Status == "open" && f.AuditorId == userId)
                .Select(f => new { f.Id, f.Code, f.Title, f.DueDate })
                .ToList();
            foreach (var f in myOpenFindings)
            {
                var overdue = f.DueDate.HasValue && f.DueDate.Value < DateOnly.FromDateTime(DateTime.Today);
                tasks.Add(new("", $"{f.Code} — {f.Title}",
                    overdue ? $"Vadesi geçmiş: {f.DueDate:dd.MM.yyyy}" : "Açık bulgu — aksiyon alınması gerekiyor",
                    $"/audit/findings/{f.Id}",
                    overdue ? "#991b1b" : "#0369a1",
                    overdue ? "#fef2f2" : "#eff6ff",
                    "Denetim Bulgusu",
                    overdue ? "urgent" : "normal"));
            }
        }

        if (role is "finding_owner")
        {
            // Findings assigned to this user as owner
            var assigned = db.AuditFindings
                .Where(f => f.OwnerId == userId && f.Status == "open")
                .Select(f => new { f.Id, f.Code, f.Title, f.DueDate })
                .ToList();
            foreach (var f in assigned)
            {
                var overdue = f.DueDate.HasValue && f.DueDate.Value < DateOnly.FromDateTime(DateTime.Today);
                tasks.Add(new("", $"{f.Code} — {f.Title}",
                    overdue ? $"Vadesi geçmiş: {f.DueDate:dd.MM.yyyy}" : "Size atanan açık bulgu",
                    $"/audit/findings/{f.Id}",
                    overdue ? "#991b1b" : "#92400e",
                    overdue ? "#fef2f2" : "#fffbeb",
                    "Atanan Bulgu",
                    overdue ? "urgent" : "normal"));
            }
        }

        // ── Ethics tasks ─────────────────────────────────────────────

        if (role is "admin" or "audit_manager")
        {
            var pendingAuditReview = db.EthicsReports
                .Where(r => r.Status == "pending")
                .Select(r => new { r.Id, r.Code, r.Subject })
                .ToList();
            foreach (var r in pendingAuditReview)
                tasks.Add(new("", $"{r.Code} — {r.Subject}",
                    "Denetim değerlendirmesi bekliyor", $"/ethics/reports/{r.Id}",
                    "#7c3aed", "#faf5ff", "Etik Değerlendirme"));
        }

        if (role is "admin" or "ethics_board")
        {
            var pendingBoardReview = db.EthicsReports
                .Where(r => r.Status == "ethics_board_notified")
                .Select(r => new { r.Id, r.Code, r.Subject })
                .ToList();
            foreach (var r in pendingBoardReview)
                tasks.Add(new("", $"{r.Code} — {r.Subject}",
                    "Kurul değerlendirmesi bekliyor", $"/ethics/reports/{r.Id}",
                    "#7c3aed", "#faf5ff", "Etik Kurul"));
        }

        // ── Periyodik inceleme vadesi geçmiş riskler ────────────────────
        if (role is "admin" or "risk_manager" or "committee")
        {
            var thresholdDays = config.Get<int>("review_threshold_days");
            if (thresholdDays <= 0) thresholdDays = 90;
            var cutoff = DateTime.UtcNow.AddDays(-thresholdDays);

            var overdueReviews = db.Risks
                .Where(r => r.Status != "rejected" && r.Status != "proposed"
                    && (r.LastReviewedAt == null || r.LastReviewedAt < cutoff))
                .Select(r => new { r.Id, r.Code, r.Title, r.LastReviewedAt })
                .ToList();

            foreach (var r in overdueReviews)
            {
                var lastStr = r.LastReviewedAt.HasValue
                    ? $"Son inceleme: {r.LastReviewedAt.Value.ToLocalTime():dd.MM.yyyy}"
                    : "Henüz incelenmedi";
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    $"Periyodik inceleme vadesi geçti ({thresholdDays} gün) — {lastStr}",
                    $"/risk/{r.Id}", "#b45309", "#fffbeb", "Periyodik İnceleme"));
            }
        }

        // ── Admin: pending password reset requests ───────────────────
        if (role is "admin")
        {
            var resets = (
                from t in db.PasswordResetTokens
                join u in db.Users on t.UserId equals u.Id
                where !t.Used && t.ExpiresAt > DateTime.UtcNow
                select new { t.ExpiresAt, u.FullName, u.Username }
            ).ToList();

            foreach (var r in resets)
                tasks.Add(new("", $"{r.FullName} ({r.Username})",
                    $"Şifre sıfırlama talebi — son geçerlilik {r.ExpiresAt.ToLocalTime():dd.MM.yyyy HH:mm}",
                    "/admin/users",
                    "#92400e", "#fffbeb", "Şifre Sıfırlama"));
        }

        return tasks;
    }
}
