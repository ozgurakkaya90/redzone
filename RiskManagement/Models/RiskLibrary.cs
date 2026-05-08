using System.ComponentModel.DataAnnotations;

namespace RiskManagement.Models;

public class RiskLibraryItem
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(100)]
    public string? SubCategory { get; set; }

    [MaxLength(20)]
    public string SourceType { get; set; } = "internal"; // internal | external

    [MaxLength(500)]
    public string? Hazard { get; set; }

    [MaxLength(500)]
    public string? PossibleImpact { get; set; }

    [MaxLength(50)]
    public string? RiskStrategy { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    [MaxLength(200)]
    public string? ReferenceStandard { get; set; } // ISO 31000, COSO, vb.

    public bool IsActive { get; set; } = true;
    public int UsageCount { get; set; } = 0; // kaç kez envanterе eklendi
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }

    public User? CreatedBy { get; set; }
}
