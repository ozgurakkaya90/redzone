using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RiskManagement.Data;
using RiskManagement.Models;

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

// Sidebar görev rozeti, tüm oturum boyunca yaşayan MainLayout tarafından çağrılır.
// Circuit-ömrü paylaşımlı context yerine IDbContextFactory ile her çağrıda kısa-ömürlü
// context kullanılır; böylece her-zaman-açık sidebar sorgusu, sayfa sorgularıyla aynı
// context üzerinde örtüşmez. Servis tamamen salt-okunurdur (DTO döndürür, SaveChanges yok).
public class TaskService(IDbContextFactory<AppDbContext> dbFactory, ConfigService config, IMemoryCache cache)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    // Cache key: rol + userId — farklı kullanıcılar asla aynı key'i paylaşmaz
    private static string CacheKey(int userId, string role) => $"tasks:u{userId}:r{role}";

    /// <summary>Cache'i geçersiz kıl (örn. yeni risk eklendikten sonra çağırılabilir).</summary>
    public void InvalidateCache(int userId, string role) =>
        cache.Remove(CacheKey(userId, role));

    public List<UserTask> GetTasksForUser(int userId, string role)
    {
        var key = CacheKey(userId, role);
        if (cache.TryGetValue(key, out List<UserTask>? cached) && cached is not null)
            return cached;

        using var db = dbFactory.CreateDbContext();
        var result = ComputeTasksForUser(db, userId, role)
            .GroupBy(t => t.Url)
            .Select(g => g.First())
            .ToList();
        cache.Set(key, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            Size = 1,
        });
        return result;
    }

    public List<UserTask> GetTasksForUser(User user)
    {
        // Union tasks across all roles the user holds, deduplicated by Url
        var allRoles = user.AllRoleNames.ToList();
        if (allRoles.Count == 0) return [];

        var seen = new HashSet<string>();
        var combined = new List<UserTask>();
        foreach (var role in allRoles)
        {
            var roleTasks = GetTasksForUser(user.Id, role);
            foreach (var t in roleTasks)
                if (seen.Add(t.Url)) combined.Add(t);
        }
        return combined;
    }

    private List<UserTask> ComputeTasksForUser(AppDbContext db, int userId, string role)
    {
        var tasks = new List<UserTask>();

        // ── Risk tasks ──────────────────────────────────────────────

        if (role is "admin" or "committee")
        {
            // 4 ayrı sorgu yerine tek sorguda tüm durumlara göre grupla
            var adminStatuses = new[] { RiskStatus.Proposed, RiskStatus.AwaitingApproval, RiskStatus.UnderReview, RiskStatus.Controlled };
            var adminRisks = db.Risks
                .Where(r => adminStatuses.Contains(r.Status))
                .Select(r => new { r.Id, r.Code, r.Title, r.Status })
                .ToList()
                .GroupBy(r => r.Status)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var r in adminRisks.GetValueOrDefault(RiskStatus.Proposed, []))
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "İnceleme bekliyor", $"/risk/{r.Id}",
                    "#7c3aed", "#faf5ff", "Risk İnceleme"));

            foreach (var r in adminRisks.GetValueOrDefault(RiskStatus.AwaitingApproval, []))
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Komite onayı bekliyor", $"/risk/{r.Id}",
                    "#7c3aed", "#faf5ff", "Risk Onayı"));

            foreach (var r in adminRisks.GetValueOrDefault(RiskStatus.UnderReview, []))
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "İlk değerlendirme bekliyor", $"/risk/{r.Id}",
                    "#0369a1", "#eff6ff", "Risk Değerlendirme"));

            foreach (var r in adminRisks.GetValueOrDefault(RiskStatus.Controlled, []))
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Kalıntı risk değerlendirmesi bekliyor", $"/risk/{r.Id}",
                    "#0f766e", "#f0fdfa", "Risk Değerlendirme"));
        }

        if (role is "admin" or "risk_owner" or "committee")
        {
            // 3 ayrı sorgu yerine tek sorguda kapsama ve duruma göre filtrele
            var ownerStatuses = new[] { RiskStatus.Approved, RiskStatus.StrategySet, RiskStatus.ResidualEvaluated };
            var ownerRisks = db.Risks
                .Where(r => ownerStatuses.Contains(r.Status)
                    && (r.OwnerId == userId || role == "admin" || role == "committee"))
                .Select(r => new { r.Id, r.Code, r.Title, r.Status })
                .ToList()
                .GroupBy(r => r.Status)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var r in ownerRisks.GetValueOrDefault(RiskStatus.Approved, []))
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Strateji ve sorumlu birim belirlenmesi gerekiyor", $"/risk/{r.Id}",
                    "#92400e", "#fffbeb", "Strateji Belirleme"));

            foreach (var r in ownerRisks.GetValueOrDefault(RiskStatus.StrategySet, []))
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Kontrol eklenmesi gerekiyor", $"/risk/{r.Id}",
                    "#0369a1", "#eff6ff", "Kontrol Ekleme"));

            foreach (var r in ownerRisks.GetValueOrDefault(RiskStatus.ResidualEvaluated, []))
                tasks.Add(new("", $"{r.Code} — {r.Title}",
                    "Aksiyon planı oluşturulması gerekiyor", $"/risk/{r.Id}",
                    "#166534", "#f0fdf4", "Aksiyon Planı"));
        }

        // Vadesi geçmiş aksiyon planları (planned veya in_progress)
        var overdueActions = db.ActionPlans
            .Where(a => (a.Status == ActionStatus.Planned || a.Status == ActionStatus.InProgress)
                && a.DueDate < DateOnly.FromDateTime(DateTime.Today)
                && (a.CreatedById == userId || role == "admin"))
            .Select(a => new { a.RiskId, RiskCode = a.Risk!.Code, RiskTitle = a.Risk.Title,
                               a.Description, a.DueDate })
            .ToList();
        foreach (var a in overdueActions)
            tasks.Add(new("", $"{a.RiskCode} — {a.Description}",
                $"Aksiyon vadesi geçti: {a.DueDate:dd.MM.yyyy}", $"/risk/{a.RiskId}",
                "#991b1b", "#fef2f2", "Geciken Aksiyon", "urgent"));

        // ── Audit tasks ─────────────────────────────────────────────

        if (role is "admin" or "audit_manager")
        {
            var closureRequests = db.ClosureRequests
                .Where(c => c.Status == ClosureRequestStatus.Pending)
                .Select(c => new { c.Id, c.FindingId, c.Finding!.Code, c.Finding.Title })
                .ToList();
            foreach (var c in closureRequests)
                tasks.Add(new("", $"{c.Code} — {c.Title}",
                    "Kapanış başvurusu onay bekliyor", $"/audit/findings/{c.FindingId}",
                    "#065f46", "#f0fdf4", "Kapanış Onayı"));
        }

        if (role is "admin" or "auditor" or "audit_manager")
        {
            var myOpenFindings = db.AuditFindings
                .Where(f => f.Status == FindingStatus.Open && f.AuditorId == userId)
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
            var assigned = db.AuditFindings
                .Where(f => f.OwnerId == userId && f.Status == FindingStatus.Open)
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

        if (role is "admin" or "audit_manager" or "ethics_board")
        {
            // 2 ayrı sorgu yerine tek sorguda iki durumu birlikte al
            var ethicsStatuses = new[] { EthicsStatus.Pending, EthicsStatus.EthicsBoardNotified };
            var ethicsReports = db.EthicsReports
                .Where(r => ethicsStatuses.Contains(r.Status))
                .Select(r => new { r.Id, r.Code, r.Subject, r.Status })
                .ToList();

            foreach (var r in ethicsReports.Where(r => r.Status == EthicsStatus.Pending
                && role is "admin" or "audit_manager"))
                tasks.Add(new("", $"{r.Code} — {r.Subject}",
                    "Denetim değerlendirmesi bekliyor", $"/ethics/reports/{r.Id}",
                    "#7c3aed", "#faf5ff", "Etik Değerlendirme"));

            foreach (var r in ethicsReports.Where(r => r.Status == EthicsStatus.EthicsBoardNotified
                && role is "admin" or "ethics_board"))
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
                .Where(r => r.Status != RiskStatus.Rejected && r.Status != RiskStatus.Proposed
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

        // ── Admin: bekleyen şifre sıfırlama talepleri ───────────────
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
