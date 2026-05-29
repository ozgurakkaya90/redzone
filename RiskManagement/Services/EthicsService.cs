using RiskManagement.Data;
using RiskManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RiskManagement.Services;

public class EthicsService(AppDbContext db, IWebHostEnvironment env, INotificationService? notifications = null,
    ILogger<EthicsService>? logger = null)
{
    private static readonly HashSet<string> AllowedExtensions =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".zip"];
    private const long MaxFileSize = 10 * 1024 * 1024;

    private string UploadsDir => Path.Combine(env.ContentRootPath, "uploads", "ethics");

    public string GenerateCode()
    {
        var year = DateTime.UtcNow.Year;
        return $"EB-{year}-{CounterHelper.GetNext(db, $"ethics-{year}"):D3}";
    }

    public IQueryable<EthicsReport> Query() => db.EthicsReports
        .Include(r => r.AuditReviewer)
        .Include(r => r.EthicsReviewer)
        .Include(r => r.Attachments);

    public List<EthicsReport> GetAll(string? status = null)
    {
        var q = Query();
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        return [.. q.OrderByDescending(r => r.SubmittedAt)];
    }

    public EthicsReport? Get(int id) => Query().FirstOrDefault(r => r.Id == id);

    public async Task<EthicsReport> SubmitAsync(string subject, string description,
        string? category, IList<IFormFile> files)
    {
        var report = new EthicsReport
        {
            Code = GenerateCode(),
            Subject = subject,
            Description = description,
            ReportCategory = string.IsNullOrEmpty(category) ? null : category,
        };
        db.EthicsReports.Add(report);
        await db.SaveChangesAsync();

        Directory.CreateDirectory(UploadsDir);
        foreach (var file in files)
        {
            if (file.Length == 0 || file.Length > MaxFileSize) continue;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) continue;

            var stored = $"{Guid.NewGuid()}{ext}";
            var path = Path.Combine(UploadsDir, stored);
            await using var fs = File.Create(path);
            await file.CopyToAsync(fs);

            db.EthicsAttachments.Add(new EthicsAttachment
            {
                ReportId = report.Id,
                OriginalFilename = file.FileName,
                StoredFilename = stored,
                FileSize = file.Length,
            });
        }
        await db.SaveChangesAsync();
        // Bildirim hatası işlemi durdurmaz — fire-and-forget, başarısızlık yutulur.
        if (notifications != null)
            _ = notifications.NotifyEthicsSubmittedAsync(report.Code, report.Subject);
        return report;
    }

    public bool AuditReview(int id, string decision, string? notes, int reviewedById)
    {
        var report = db.EthicsReports.Find(id);
        if (report == null) return false;
        // Denetim kararı whitelist.
        if (decision is not ("irrelevant" or "ethics_board_notified"))
        {
            logger?.LogWarning("Etik denetim değerlendirme reddi — geçersiz karar: {Decision}, ReportId: {ReportId}, Kullanıcı: {UserId}",
                decision, id, reviewedById);
            return false;
        }
        report.AuditDecision = decision;
        report.AuditNotes = notes;
        report.AuditReviewedById = reviewedById;
        report.AuditReviewedAt = DateTime.UtcNow;
        report.Status = decision == "irrelevant" ? "irrelevant" : "ethics_board_notified";
        db.SaveChanges();
        if (notifications != null)
            _ = notifications.NotifyEthicsReviewedAsync(report.Code, report.Subject, "audit");
        return true;
    }

    public bool EthicsReview(int id, string decision, string? notes, int reviewedById)
    {
        var report = db.EthicsReports.Find(id);
        if (report == null) return false;
        // EthicsDecision whitelist — bilinmeyen karar değerini reddet.
        if (decision is not ("no_violation" or "disciplinary_referred"))
        {
            logger?.LogWarning("Etik kurul değerlendirme reddi — geçersiz karar: {Decision}, ReportId: {ReportId}, Kullanıcı: {UserId}",
                decision, id, reviewedById);
            return false;
        }
        report.EthicsDecision = decision;
        report.EthicsNotes = notes;
        report.EthicsReviewedById = reviewedById;
        report.EthicsReviewedAt = DateTime.UtcNow;
        report.Status = decision;
        db.SaveChanges();
        if (notifications != null)
            _ = notifications.NotifyEthicsReviewedAsync(report.Code, report.Subject, "board");
        return true;
    }

    public (string Path, string OriginalName)? GetAttachmentPath(int reportId, int attachmentId)
    {
        var att = db.EthicsAttachments
            .FirstOrDefault(a => a.Id == attachmentId && a.ReportId == reportId);
        if (att == null) return null;
        var fullPath = Path.Combine(UploadsDir, att.StoredFilename);
        return File.Exists(fullPath) ? (fullPath, att.OriginalFilename) : null;
    }
}
