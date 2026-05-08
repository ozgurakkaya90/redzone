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
    public DbSet<AuditFindingAction> AuditFindingActions => Set<AuditFindingAction>();
    public DbSet<FindingAttachment> FindingAttachments => Set<FindingAttachment>();
    public DbSet<EthicsReport> EthicsReports => Set<EthicsReport>();
    public DbSet<EthicsAttachment> EthicsAttachments => Set<EthicsAttachment>();
    public DbSet<LdapConfiguration> LdapConfigurations => Set<LdapConfiguration>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
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
            .HasOne(f => f.Department).WithMany().HasForeignKey(f => f.DepartmentId)
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
    }
}
