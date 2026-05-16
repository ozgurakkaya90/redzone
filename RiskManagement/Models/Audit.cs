using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RiskManagement.Models;

public class AuditPlan
{
    public int Id { get; set; }
    public int Year { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User CreatedBy { get; set; } = null!;
    public ICollection<AuditPlanItem> Items { get; set; } = [];
}

public class AuditPlanItem
{
    public int Id { get; set; }
    public int AuditPlanId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(100)]
    public string? AuditType { get; set; }

    [MaxLength(200)]
    public string? AuditedUnit { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }

    public int? DepartmentId { get; set; }

    public int? ResponsibleId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    public AuditPlan Plan { get; set; } = null!;
    public Department? Department { get; set; }
    public User? Responsible { get; set; }
}

public class InternalAudit
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Code { get; set; } = "";

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Scope { get; set; }

    [Required, MaxLength(20)]
    public string Period { get; set; } = "";

    [MaxLength(50)]
    public string? AuditType { get; set; }

    [MaxLength(200)]
    public string? AuditedUnit { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "planned"; // planned|in_progress|completed

    // [NotMapped]: Bu kolonlar MySQL'de henüz yoksa EF Core SELECT'e eklemez.
    // Migration uygulandıktan sonra bu attribute'lar kaldırılacak.
    [NotMapped] public int? DepartmentId { get; set; }
    [NotMapped] public int? AuditPlanItemId { get; set; }

    public int LeadAuditorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User LeadAuditor { get; set; } = null!;
    [NotMapped] public Department? Department { get; set; }
    [NotMapped] public AuditPlanItem? AuditPlanItem { get; set; }
    public ICollection<AuditFinding> Findings { get; set; } = [];
}

public class AuditFinding
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Code { get; set; } = "";

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(20)]
    public string? Severity { get; set; }

    [MaxLength(20)]
    public string? AuditPeriod { get; set; }

    public int AuditorId { get; set; }
    public int? OwnerId { get; set; }
    public int? DepartmentId { get; set; }
    public int? InternalAuditId { get; set; }

    // Dış denetim entegrasyonu — migration uygulandı, artık EF okuyabilir.
    // "internal" | "external" — varsayılan internal (geriye dönük uyum)
    [MaxLength(20)]
    public string AuditSource { get; set; } = "internal";

    public int? ExternalAuditId { get; set; }

    // Nav property [NotMapped] — FK konfigürasyonu yok, manuel yüklenir
    [NotMapped]
    public ExternalAudit? ExternalAudit { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "open"; // open|closure_requested|closed

    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    [MaxLength(20)]
    public string? ActionDecision { get; set; } // null | "action_planned" | "risk_accepted"

    public User Auditor { get; set; } = null!;
    public User? Owner { get; set; }
    public Department? Department { get; set; }
    public InternalAudit? InternalAudit { get; set; }
    public ICollection<ClosureRequest> ClosureRequests { get; set; } = [];
    public ICollection<AuditFindingAction> Actions { get; set; } = [];
    public ICollection<FindingAttachment> Attachments { get; set; } = [];
}

public class AuditFindingAction
{
    public int Id { get; set; }
    public int FindingId { get; set; }

    [Required, MaxLength(1000)]
    public string Description { get; set; } = "";

    [MaxLength(200)]
    public string? Responsible { get; set; }

    public DateOnly? DueDate { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "planned"; // planned|in_progress|completed|cancelled

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public AuditFinding Finding { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}

public class FindingAttachment
{
    public int Id { get; set; }
    public int FindingId { get; set; }
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int UploadedById { get; set; }

    public AuditFinding Finding { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}

// ─────────────────────────────────────────────────────────────────
// Dış Denetim (BRC, JCI, müşteri denetimi vb.)
// ─────────────────────────────────────────────────────────────────
public class ExternalAudit
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Code { get; set; } = "";

    [Required, MaxLength(200)]
    public string AuditingBody { get; set; } = "";   // BRC, JCI, TSE, müşteri…

    [MaxLength(100)]
    public string? Standard { get; set; }            // "BRC v9" gibi (opsiyonel)

    [Required, MaxLength(300)]
    public string Subject { get; set; } = "";        // Denetim Konusu

    public DateOnly AuditDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public int? ResponsibleDeptId { get; set; }
    public int? ResponsibleUserId { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "planned";  // planned|in_progress|completed|closed

    [MaxLength(100)]
    public string? Score { get; set; }               // A/B/Pass vb. serbest

    [MaxLength(500)]
    public string? ReportFilePath { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Department? ResponsibleDept { get; set; }
    public User? ResponsibleUser { get; set; }
    public User CreatedBy { get; set; } = null!;

    // [NotMapped]: AuditFinding.ExternalAuditId migration uygulanana kadar NotMapped.
    [NotMapped]
    public ICollection<AuditFinding> Findings { get; set; } = [];
}

// Denetleyici kurum kataloğu (BRC, JCI…). Soft-delete: IsActive=false işaretlenir,
// eski denetim kayıtları string olarak referans tuttuğu için geriye dönük korunur.
public class ExternalAuditBody
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Kullanıcının hangi dış denetim kurumlarına erişebileceğini tutar.
// (UserId, AuditingBody) unique. CanEdit=false ise sadece görüntüler.
public class UserExternalAuditBody
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [Required, MaxLength(200)]
    public string AuditingBody { get; set; } = "";

    public bool CanEdit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

public class ClosureRequest
{
    public int Id { get; set; }
    public int FindingId { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = "";

    [MaxLength(2000)]
    public string? Evidence { get; set; }

    public int RequestedById { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending|approved|rejected

    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }

    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }

    public AuditFinding Finding { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
