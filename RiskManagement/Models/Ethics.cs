namespace RiskManagement.Models;

public class EthicsReport
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ReportCategory { get; set; }
    // pending→ethics_board_notified→disciplinary_referred|no_violation | irrelevant
    public string Status { get; set; } = "pending";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public string? AuditDecision { get; set; }
    public string? AuditNotes { get; set; }
    public int? AuditReviewedById { get; set; }
    public DateTime? AuditReviewedAt { get; set; }

    public string? EthicsDecision { get; set; }
    public string? EthicsNotes { get; set; }
    public int? EthicsReviewedById { get; set; }
    public DateTime? EthicsReviewedAt { get; set; }

    public User? AuditReviewer { get; set; }
    public User? EthicsReviewer { get; set; }
    public ICollection<EthicsAttachment> Attachments { get; set; } = [];
}

public class EthicsAttachment
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public string OriginalFilename { get; set; } = "";
    public string StoredFilename { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public EthicsReport Report { get; set; } = null!;
}
