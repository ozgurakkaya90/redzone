using System.ComponentModel.DataAnnotations;

namespace RiskManagement.Models;

public class EthicsReport
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Code { get; set; } = "";

    /// <summary>
    /// Anonim durum sorgusu için yüksek-entropili gizli takip token'ı.
    /// Sıralı/tahmin edilebilir <see cref="Code"/> yerine durum URL'sinde kullanılır,
    /// böylece kod enumerasyonuyla başkalarının ihbarları okunamaz.
    /// </summary>
    [MaxLength(64)]
    public string? TrackingToken { get; set; }

    [Required, MaxLength(200)]
    public string Subject { get; set; } = "";

    [Required, MaxLength(4000)]
    public string Description { get; set; } = "";

    [MaxLength(100)]
    public string? ReportCategory { get; set; }

    // pending→ethics_board_notified→disciplinary_referred|no_violation | irrelevant
    [Required, MaxLength(30)]
    public string Status { get; set; } = "pending";

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    public string? AuditDecision { get; set; }

    [MaxLength(1000)]
    public string? AuditNotes { get; set; }

    public int? AuditReviewedById { get; set; }
    public DateTime? AuditReviewedAt { get; set; }

    [MaxLength(50)]
    public string? EthicsDecision { get; set; }

    [MaxLength(1000)]
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
