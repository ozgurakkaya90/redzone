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
    public DbSet<AuditPlan> AuditPlans => Set<AuditPlan>();
    public DbSet<AuditPlanItem> AuditPlanItems => Set<AuditPlanItem>();
    public DbSet<InternalAudit> InternalAudits => Set<InternalAudit>();
    public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();
    public DbSet<ClosureRequest> ClosureRequests => Set<ClosureRequest>();
    public DbSet<AuditFindingAction> AuditFindingActions => Set<AuditFindingAction>();
    public DbSet<FindingAttachment> FindingAttachments => Set<FindingAttachment>();
    public DbSet<FindingActivityLog> FindingActivityLogs => Set<FindingActivityLog>();
    public DbSet<RiskFindingLink> RiskFindingLinks => Set<RiskFindingLink>();
    public DbSet<EthicsReport> EthicsReports => Set<EthicsReport>();
    public DbSet<EthicsAttachment> EthicsAttachments => Set<EthicsAttachment>();
    public DbSet<LdapConfiguration> LdapConfigurations => Set<LdapConfiguration>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<CustomRole> CustomRoles => Set<CustomRole>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<ConfigChangeLog> ConfigChangeLogs => Set<ConfigChangeLog>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<DatabaseConnectionConfig> DatabaseConnections => Set<DatabaseConnectionConfig>();
    public DbSet<RiskReview> RiskReviews => Set<RiskReview>();
    public DbSet<RiskAuditLog> RiskAuditLogs => Set<RiskAuditLog>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();
    public DbSet<UserOrganization> UserOrganizations => Set<UserOrganization>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<RiskLibraryItem> RiskLibraryItems => Set<RiskLibraryItem>();
    public DbSet<ExternalAudit> ExternalAudits => Set<ExternalAudit>();
    public DbSet<ExternalAuditBody> ExternalAuditBodies => Set<ExternalAuditBody>();
    public DbSet<UserExternalAuditBody> UserExternalAuditBodies => Set<UserExternalAuditBody>();
    public DbSet<McpApiKey> McpApiKeys => Set<McpApiKey>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // User
        mb.Entity<User>().HasIndex(u => u.Username).IsUnique();
        mb.Entity<User>().HasOne(u => u.DepartmentNav).WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<User>().HasOne(u => u.OrganizationNav).WithMany().HasForeignKey(u => u.OrganizationId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<User>().HasOne(u => u.CompanyNav).WithMany().HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.SetNull);

        // Risk — iki farklı FK aynı tabloya
        mb.Entity<Risk>()
            .HasOne(r => r.ProposedBy).WithMany(u => u.ProposedRisks)
            .HasForeignKey(r => r.ProposedById).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Risk>()
            .HasOne(r => r.Owner).WithMany(u => u.OwnedRisks)
            .HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Risk>()
            .HasOne(r => r.Organization).WithMany().HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Risk>()
            .HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        // Pasife alınmamış (mevcut) tüm riskler aktif kabul edilir — DB default true.
        mb.Entity<Risk>().Property(r => r.IsActive).HasDefaultValue(true);
        mb.Entity<RiskReview>()
            .HasOne(rv => rv.Risk).WithMany(r => r.Reviews).HasForeignKey(rv => rv.RiskId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<RiskReview>()
            .HasOne(rv => rv.CreatedBy).WithMany().HasForeignKey(rv => rv.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<RiskAuditLog>()
            .HasOne(l => l.Risk).WithMany(r => r.AuditLogs).HasForeignKey(l => l.RiskId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<RiskAuditLog>()
            .HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

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
        mb.Entity<Control>()
            .HasOne(c => c.OwnerDept).WithMany().HasForeignKey(c => c.OwnerDeptId)
            .OnDelete(DeleteBehavior.SetNull);

        // ActionPlan
        mb.Entity<ActionPlan>()
            .HasOne(a => a.Risk).WithMany(r => r.ActionPlans).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ActionPlan>()
            .HasOne(a => a.CreatedBy).WithMany().HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ActionPlan>()
            .HasOne(a => a.OwnerDept).WithMany().HasForeignKey(a => a.OwnerDeptId)
            .OnDelete(DeleteBehavior.SetNull);

        // AuditPlan
        mb.Entity<AuditPlan>()
            .HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AuditPlan>()
            .HasIndex(p => p.Year);
        mb.Entity<AuditPlanItem>()
            .HasOne(i => i.Plan).WithMany(p => p.Items).HasForeignKey(i => i.AuditPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<AuditPlanItem>()
            .HasOne(i => i.Responsible).WithMany().HasForeignKey(i => i.ResponsibleId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AuditPlanItem>()
            .HasOne(i => i.Department).WithMany().HasForeignKey(i => i.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        // InternalAudit
        mb.Entity<InternalAudit>()
            .HasOne(a => a.LeadAuditor).WithMany().HasForeignKey(a => a.LeadAuditorId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<InternalAudit>()
            .HasOne(a => a.Department).WithMany().HasForeignKey(a => a.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<InternalAudit>()
            .HasOne(a => a.AuditPlanItem).WithMany().HasForeignKey(a => a.AuditPlanItemId)
            .OnDelete(DeleteBehavior.SetNull);

        // AuditFinding
        mb.Entity<AuditFinding>()
            .HasOne(f => f.Auditor).WithMany().HasForeignKey(f => f.AuditorId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AuditFinding>()
            .HasOne(f => f.Owner).WithMany().HasForeignKey(f => f.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AuditFinding>()
            .HasOne(f => f.Department).WithMany().HasForeignKey(f => f.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AuditFinding>()
            .HasOne(f => f.InternalAudit).WithMany(a => a.Findings)
            .HasForeignKey(f => f.InternalAuditId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AuditFinding>()
            .HasOne(f => f.ExternalAudit).WithMany(e => e.Findings)
            .HasForeignKey(f => f.ExternalAuditId).OnDelete(DeleteBehavior.SetNull);

        // ClosureRequest
        mb.Entity<ClosureRequest>()
            .HasOne(c => c.Finding).WithMany(f => f.ClosureRequests).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ClosureRequest>()
            .HasOne(c => c.RequestedBy).WithMany().HasForeignKey(c => c.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClosureRequest>()
            .HasOne(c => c.ReviewedBy).WithMany().HasForeignKey(c => c.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);

        // AuditFindingAction
        mb.Entity<AuditFindingAction>()
            .HasOne(a => a.Finding).WithMany(f => f.Actions).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<AuditFindingAction>()
            .HasOne(a => a.CreatedBy).WithMany().HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // FindingAttachment
        mb.Entity<FindingAttachment>()
            .HasOne(a => a.Finding).WithMany(f => f.Attachments).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<FindingAttachment>()
            .HasOne(a => a.UploadedBy).WithMany().HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        // RiskFindingLink
        mb.Entity<RiskFindingLink>()
            .HasOne(l => l.Risk).WithMany(r => r.FindingLinks).HasForeignKey(l => l.RiskId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<RiskFindingLink>()
            .HasOne(l => l.Finding).WithMany(f => f.RiskLinks).HasForeignKey(l => l.FindingId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<RiskFindingLink>()
            .HasOne(l => l.CreatedBy).WithMany().HasForeignKey(l => l.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<RiskFindingLink>()
            .HasIndex(l => new { l.RiskId, l.FindingId }).IsUnique();

        // FindingActivityLog
        mb.Entity<FindingActivityLog>()
            .HasOne(l => l.Finding).WithMany(f => f.ActivityLogs).HasForeignKey(l => l.FindingId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<FindingActivityLog>()
            .HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // EthicsReport
        mb.Entity<EthicsReport>()
            .HasIndex(e => e.TrackingToken);
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

        // CustomRole — unique slug
        mb.Entity<CustomRole>()
            .HasIndex(cr => cr.Name).IsUnique();

        // AppConfig — PK is Key string
        mb.Entity<AppConfig>().HasKey(c => c.Key);

        // Counter table for atomic sequence generation
        mb.Entity<Counter>().HasKey(c => c.Key);

        // Department
        mb.Entity<Department>()
            .HasOne(d => d.Manager).WithMany().HasForeignKey(d => d.ManagerUserId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Department>()
            .HasOne(d => d.Organization).WithMany(o => o.Departments).HasForeignKey(d => d.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        // Organization
        mb.Entity<Organization>()
            .HasOne(o => o.Company).WithMany(c => c.Organizations).HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        // UserRole — (UserId, RoleName) unique
        mb.Entity<UserRole>()
            .HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserRole>()
            .HasIndex(ur => new { ur.UserId, ur.RoleName }).IsUnique();

        // UserDepartment — (UserId, DepartmentId) unique
        mb.Entity<UserDepartment>()
            .HasOne(ud => ud.User).WithMany(u => u.UserDepartments).HasForeignKey(ud => ud.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserDepartment>()
            .HasOne(ud => ud.Department).WithMany().HasForeignKey(ud => ud.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserDepartment>()
            .HasIndex(ud => new { ud.UserId, ud.DepartmentId }).IsUnique();

        // UserOrganization — (UserId, OrganizationId) unique
        mb.Entity<UserOrganization>()
            .HasOne(uo => uo.User).WithMany(u => u.UserOrganizations).HasForeignKey(uo => uo.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserOrganization>()
            .HasOne(uo => uo.Organization).WithMany().HasForeignKey(uo => uo.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserOrganization>()
            .HasIndex(uo => new { uo.UserId, uo.OrganizationId }).IsUnique();

        // UserCompany — (UserId, CompanyId) unique
        mb.Entity<UserCompany>()
            .HasOne(uc => uc.User).WithMany(u => u.UserCompanies).HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserCompany>()
            .HasOne(uc => uc.Company).WithMany().HasForeignKey(uc => uc.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserCompany>()
            .HasIndex(uc => new { uc.UserId, uc.CompanyId }).IsUnique();

        // RiskLibraryItem
        mb.Entity<RiskLibraryItem>()
            .HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<RiskLibraryItem>()
            .HasIndex(r => r.Category);
        mb.Entity<RiskLibraryItem>()
            .HasIndex(r => r.IsActive);

        // ExternalAudit
        mb.Entity<ExternalAudit>()
            .HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ExternalAudit>()
            .HasOne(e => e.ResponsibleDept).WithMany().HasForeignKey(e => e.ResponsibleDeptId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<ExternalAudit>()
            .HasOne(e => e.ResponsibleUser).WithMany().HasForeignKey(e => e.ResponsibleUserId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<ExternalAudit>().HasIndex(e => e.AuditingBody);
        mb.Entity<ExternalAudit>().HasIndex(e => e.Status);
        mb.Entity<ExternalAudit>().HasIndex(e => e.AuditDate);

        // ExternalAuditBody
        mb.Entity<ExternalAuditBody>().HasIndex(b => b.Name).IsUnique();
        mb.Entity<ExternalAuditBody>().HasIndex(b => b.IsActive);

        // UserExternalAuditBody
        mb.Entity<UserExternalAuditBody>()
            .HasOne(u => u.User).WithMany().HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserExternalAuditBody>()
            .HasIndex(u => new { u.UserId, u.AuditingBody }).IsUnique();

        // McpApiKey — tablo raw SQL migration ile oluşturulduğundan EF Core'a AUTO_INCREMENT'ı bildirmek gerekiyor
        mb.Entity<McpApiKey>()
            .Property(k => k.Id).ValueGeneratedOnAdd();
        mb.Entity<McpApiKey>()
            .HasOne(k => k.CreatedBy).WithMany().HasForeignKey(k => k.CreatedById)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<McpApiKey>()
            .HasOne(k => k.ScopeUser).WithMany().HasForeignKey(k => k.ScopeUserId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<McpApiKey>()
            .HasIndex(k => k.KeyPrefix);

        // ── Performans indexleri ─────────────────────────────────────────────
        // NOT: Status/Code sütunları MySQL'de longtext — prefix olmadan indexlenemez.
        // Bu HasIndex çağrıları snapshot tutarlılığı için bırakılıyor; gerçek index
        // oluşturma AddPerformanceIndexes migration'ında kasıtlı olarak atlandı.
        // Kolon tipleri MaxLength ile düzeltildiğinde bu indexler otomatik etkin olacak.
        mb.Entity<Risk>().HasIndex(r => r.Status);
        mb.Entity<Risk>().HasIndex(r => r.ProposedAt);
        mb.Entity<Risk>().HasIndex(r => r.Code);
        mb.Entity<RiskAuditLog>().HasIndex(l => new { l.RiskId, l.Timestamp });
        mb.Entity<AuditFinding>().HasIndex(f => f.Status);
        mb.Entity<AuditFinding>().HasIndex(f => f.Code);
        mb.Entity<ActionPlan>().HasIndex(a => new { a.DueDate, a.Status });
        mb.Entity<AuditFindingAction>().HasIndex(a => new { a.DueDate, a.Status });
    }
}
