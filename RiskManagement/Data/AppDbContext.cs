using Microsoft.EntityFrameworkCore;
using RiskManagement.Models;

namespace RiskManagement.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<Control> Controls => Set<Control>();
    public DbSet<ActionPlan> ActionPlans => Set<ActionPlan>();
    public DbSet<InternalAudit> InternalAudits => Set<InternalAudit>();
    public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();
    public DbSet<ClosureRequest> ClosureRequests => Set<ClosureRequest>();
    public DbSet<EthicsReport> EthicsReports => Set<EthicsReport>();
    public DbSet<EthicsAttachment> EthicsAttachments => Set<EthicsAttachment>();
    public DbSet<LdapConfiguration> LdapConfigurations => Set<LdapConfiguration>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // User
        mb.Entity<User>().HasIndex(u => u.Username).IsUnique();

        // Risk — iki farklı FK aynı tabloya
        mb.Entity<Risk>()
            .HasOne(r => r.ProposedBy).WithMany(u => u.ProposedRisks)
            .HasForeignKey(r => r.ProposedById).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Risk>()
            .HasOne(r => r.Owner).WithMany(u => u.OwnedRisks)
            .HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.SetNull);

        // Evaluation
        mb.Entity<Evaluation>()
            .HasOne(e => e.Risk).WithMany(r => r.Evaluations).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Evaluation>()
            .HasOne(e => e.EvaluatedBy).WithMany().HasForeignKey(e => e.EvaluatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Control
        mb.Entity<Control>()
            .HasOne(c => c.Risk).WithMany(r => r.Controls).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Control>()
            .HasOne(c => c.EnteredBy).WithMany().HasForeignKey(c => c.EnteredById)
            .OnDelete(DeleteBehavior.Restrict);

        // ActionPlan
        mb.Entity<ActionPlan>()
            .HasOne(a => a.Risk).WithMany(r => r.ActionPlans).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ActionPlan>()
            .HasOne(a => a.CreatedBy).WithMany().HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // InternalAudit
        mb.Entity<InternalAudit>()
            .HasOne(a => a.LeadAuditor).WithMany().HasForeignKey(a => a.LeadAuditorId)
            .OnDelete(DeleteBehavior.Restrict);

        // AuditFinding
        mb.Entity<AuditFinding>()
            .HasOne(f => f.Auditor).WithMany().HasForeignKey(f => f.AuditorId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AuditFinding>()
            .HasOne(f => f.Owner).WithMany().HasForeignKey(f => f.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AuditFinding>()
            .HasOne(f => f.InternalAudit).WithMany(a => a.Findings)
            .HasForeignKey(f => f.InternalAuditId).OnDelete(DeleteBehavior.SetNull);

        // ClosureRequest
        mb.Entity<ClosureRequest>()
            .HasOne(c => c.Finding).WithMany(f => f.ClosureRequests).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ClosureRequest>()
            .HasOne(c => c.RequestedBy).WithMany().HasForeignKey(c => c.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClosureRequest>()
            .HasOne(c => c.ReviewedBy).WithMany().HasForeignKey(c => c.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);

        // EthicsReport
        mb.Entity<EthicsReport>()
            .HasOne(e => e.AuditReviewer).WithMany().HasForeignKey(e => e.AuditReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<EthicsReport>()
            .HasOne(e => e.EthicsReviewer).WithMany().HasForeignKey(e => e.EthicsReviewedById)
            .OnDelete(DeleteBehavior.SetNull);

        // EthicsAttachment
        mb.Entity<EthicsAttachment>()
            .HasOne(a => a.Report).WithMany(r => r.Attachments).OnDelete(DeleteBehavior.Cascade);

        // RolePermission — unique constraint
        mb.Entity<RolePermission>()
            .HasIndex(rp => new { rp.Role, rp.Permission }).IsUnique();

        // AppConfig — PK is Key string
        mb.Entity<AppConfig>().HasKey(c => c.Key);
    }
}
