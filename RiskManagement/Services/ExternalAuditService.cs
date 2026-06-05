using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

/// <summary>
/// Dış denetim (BRC, JCI, müşteri denetimi vb.) yönetimi.
/// Yetki: kullanıcının UserExternalAuditBodies kayıtlarındaki kurumlara erişimi vardır.
/// external_audit.admin izni olan kullanıcı tüm kurumları görür.
/// </summary>
public class ExternalAuditService(AppDbContext db, AuthService auth)
{
    // ─── Kod üretimi ─────────────────────────────────────────────────────────
    public string GenerateAuditCode()
    {
        var year = DateTime.UtcNow.Year;
        return $"DD-{year}-{CounterHelper.GetNext(db, $"external-audit-{year}"):D3}";
    }

    public string GenerateFindingCode()
    {
        var year = DateTime.UtcNow.Year;
        return $"DB-{year}-{CounterHelper.GetNext(db, $"external-finding-{year}"):D3}";
    }

    // ─── Yetki ───────────────────────────────────────────────────────────────

    /// <summary>Kullanıcının erişebileceği kurum adlarını döner. admin → null = sınırsız.</summary>
    public List<string>? GetAllowedBodies(User user)
    {
        if (IsAdmin(user)) return null;
        return [.. db.UserExternalAuditBodies
            .Where(u => u.UserId == user.Id)
            .Select(u => u.AuditingBody)
            .Distinct()];
    }

    public bool CanView(User user, string body)
    {
        if (IsAdmin(user)) return true;
        return db.UserExternalAuditBodies.Any(u => u.UserId == user.Id && u.AuditingBody == body);
    }

    public bool CanEdit(User user, string body)
    {
        if (IsAdmin(user)) return true;
        if (!auth.HasPermission(user, "external_audit.write")) return false;
        return db.UserExternalAuditBodies.Any(u => u.UserId == user.Id
            && u.AuditingBody == body && u.CanEdit);
    }

    public bool IsAdmin(User user) =>
        user.HasAnyRole("admin") || auth.HasPermission(user, "external_audit.admin");

    public bool UserHasAnyBody(int userId) =>
        db.UserExternalAuditBodies.Any(b => b.UserId == userId);

    // ─── Kurum Kataloğu ──────────────────────────────────────────────────────
    public List<ExternalAuditBody> GetBodies(bool includeInactive = false) =>
        [.. db.ExternalAuditBodies
            .Where(b => includeInactive || b.IsActive)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Name)];

    public ExternalAuditBody? GetBody(int id) => db.ExternalAuditBodies.Find(id);

    public ExternalAuditBody AddBody(string name, string? description = null)
    {
        var existing = db.ExternalAuditBodies.FirstOrDefault(b => b.Name == name);
        if (existing != null)
        {
            // Pasifse yeniden aktive et
            if (!existing.IsActive) { existing.IsActive = true; db.SaveChanges(); }
            return existing;
        }
        var maxOrder = db.ExternalAuditBodies.Select(b => (int?)b.SortOrder).Max() ?? 0;
        var b = new ExternalAuditBody { Name = name, Description = description, SortOrder = maxOrder + 1 };
        db.ExternalAuditBodies.Add(b);
        db.SaveChanges();
        return b;
    }

    public void UpdateBody(ExternalAuditBody body)
    {
        db.ExternalAuditBodies.Update(body);
        db.SaveChanges();
    }

    /// <summary>Soft-delete: kurum pasif işaretlenir, eski denetim kayıtları korunur.</summary>
    public void DeactivateBody(int id)
    {
        var b = db.ExternalAuditBodies.Find(id);
        if (b is null) return;
        b.IsActive = false;
        db.SaveChanges();
    }

    public void ActivateBody(int id)
    {
        var b = db.ExternalAuditBodies.Find(id);
        if (b is null) return;
        b.IsActive = true;
        db.SaveChanges();
    }

    // ─── Yetki Atamaları ─────────────────────────────────────────────────────
    public List<UserExternalAuditBody> GetUserBodies(int userId) =>
        [.. db.UserExternalAuditBodies.Where(u => u.UserId == userId).OrderBy(u => u.AuditingBody)];

    public List<UserExternalAuditBody> GetBodyUsers(string body) =>
        [.. db.UserExternalAuditBodies
            .Include(u => u.User)
            .Where(u => u.AuditingBody == body)
            .OrderBy(u => u.User.FullName)];

    public void GrantBody(int userId, string body, bool canEdit)
    {
        var existing = db.UserExternalAuditBodies
            .FirstOrDefault(u => u.UserId == userId && u.AuditingBody == body);
        if (existing != null)
        {
            existing.CanEdit = canEdit;
        }
        else
        {
            db.UserExternalAuditBodies.Add(new UserExternalAuditBody
            {
                UserId = userId,
                AuditingBody = body,
                CanEdit = canEdit
            });
        }
        db.SaveChanges();
    }

    public void RevokeBody(int userId, string body)
    {
        var ex = db.UserExternalAuditBodies
            .FirstOrDefault(u => u.UserId == userId && u.AuditingBody == body);
        if (ex is null) return;
        db.UserExternalAuditBodies.Remove(ex);
        db.SaveChanges();
    }

    // ─── Dış Denetim CRUD ────────────────────────────────────────────────────
    public IQueryable<ExternalAudit> AuditQuery() => db.ExternalAudits
        .Include(e => e.CreatedBy)
        .Include(e => e.ResponsibleUser)
        .Include(e => e.ResponsibleDept).ThenInclude(d => d!.Organization)
            .ThenInclude(o => o!.Company);

    public List<ExternalAudit> List(User user, string? bodyFilter = null,
        string? statusFilter = null, int? year = null, string? search = null)
    {
        var q = AuditQuery();
        var allowed = GetAllowedBodies(user);
        if (allowed != null) q = q.Where(e => allowed.Contains(e.AuditingBody));
        if (!string.IsNullOrEmpty(bodyFilter)) q = q.Where(e => e.AuditingBody == bodyFilter);
        if (!string.IsNullOrEmpty(statusFilter)) q = q.Where(e => e.Status == statusFilter);
        if (year.HasValue)
        {
            var start = new DateOnly(year.Value, 1, 1);
            var end = new DateOnly(year.Value, 12, 31);
            q = q.Where(e => e.AuditDate >= start && e.AuditDate <= end);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(e => e.Code.Contains(s) || e.Subject.Contains(s)
                || e.AuditingBody.Contains(s));
        }
        return [.. q.OrderByDescending(e => e.AuditDate).ThenByDescending(e => e.Id)];
    }

    public ExternalAudit? Get(int id, User user)
    {
        var a = AuditQuery().FirstOrDefault(e => e.Id == id);
        if (a is null) return null;
        if (!CanView(user, a.AuditingBody)) return null;
        return a;
    }

    public ExternalAudit Create(ExternalAudit audit)
    {
        if (string.IsNullOrEmpty(audit.Code)) audit.Code = GenerateAuditCode();
        audit.CreatedAt = DateTime.UtcNow;
        db.ExternalAudits.Add(audit);
        db.SaveChanges();
        return audit;
    }

    public void Update(ExternalAudit audit)
    {
        db.ExternalAudits.Update(audit);
        db.SaveChanges();
    }

    public void Delete(int id)
    {
        var a = db.ExternalAudits.Find(id);
        if (a is null) return;
        // Bağlı bulguları da sil — AuditFindings ExternalAuditId üzerinden raw delete
        db.Database.ExecuteSqlRaw(
            "DELETE FROM AuditFindings WHERE ExternalAuditId = {0}", id);
        db.ExternalAudits.Remove(a);
        db.SaveChanges();
    }

    // ─── Dış Denetim Bulguları ───────────────────────────────────────────────
    /// <summary>Belirli bir dış denetime ait bulguları döner.</summary>
    public List<AuditFinding> GetFindings(int externalAuditId)
    {
        try
        {
            return [.. db.AuditFindings
                .Where(f => f.ExternalAuditId == externalAuditId)
                .OrderByDescending(f => f.CreatedAt)
                .AsNoTracking()];
        }
        catch { return []; }
    }

    public AuditFinding CreateFinding(int externalAuditId, AuditFinding f, int auditorId)
    {
        f.Code = GenerateFindingCode();
        f.AuditorId = auditorId;
        f.CreatedAt = DateTime.UtcNow;
        f.AuditSource = "external";
        f.ExternalAuditId = externalAuditId;
        db.AuditFindings.Add(f);
        db.SaveChanges();
        return f;
    }

    public void UpdateFinding(AuditFinding f)
    {
        db.AuditFindings.Update(f);
        db.SaveChanges();
    }

    public void DeleteFinding(int findingId)
    {
        var f = db.AuditFindings.Find(findingId);
        if (f is null) return;
        db.AuditFindings.Remove(f);
        db.SaveChanges();
    }

    // ─── Dashboard istatistik ────────────────────────────────────────────────
    public ExternalAuditStats GetStats(User user)
    {
        var q = AuditQuery();
        var allowed = GetAllowedBodies(user);
        if (allowed != null) q = q.Where(e => allowed.Contains(e.AuditingBody));

        var list = q.AsNoTracking().ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yearStart = new DateOnly(DateTime.Today.Year, 1, 1);

        return new ExternalAuditStats
        {
            Total           = list.Count,
            Planned         = list.Count(a => a.Status == "planned"),
            InProgress      = list.Count(a => a.Status == "in_progress"),
            Completed       = list.Count(a => a.Status == "completed" || a.Status == "closed"),
            CompletedThisYear = list.Count(a => (a.Status == "completed" || a.Status == "closed")
                                                && a.AuditDate >= yearStart),
            Upcoming        = list.Count(a => a.AuditDate > today && a.Status == "planned"),
        };
    }
}

public class ExternalAuditStats
{
    public int Total { get; set; }
    public int Planned { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int CompletedThisYear { get; set; }
    public int Upcoming { get; set; }
}
