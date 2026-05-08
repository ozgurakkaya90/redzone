using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

public class AuditAuthorizationTests
{
    [Fact]
    public void GetFindingsForUser_ScopesResultsByRoleAndAssignment()
    {
        using var db = SeedAuditData();
        var service = new AuditService(db);

        Assert.Equal(2, service.GetFindingsForUser(99, "admin").Count);
        Assert.Single(service.GetFindingsForUser(1, "auditor"));
        Assert.Single(service.GetFindingsForUser(2, "auditor"));
        Assert.Single(service.GetFindingsForUser(3, "finding_owner"));
        Assert.Empty(service.GetFindingsForUser(4, "user"));
    }

    [Fact]
    public void GetAuditForUser_ReturnsNullForUnassignedAuditor()
    {
        using var db = SeedAuditData();
        var service = new AuditService(db);

        Assert.NotNull(service.GetAuditForUser(10, 1, "auditor"));
        Assert.NotNull(service.GetAuditForUser(10, 99, "audit_manager"));
        Assert.Null(service.GetAuditForUser(10, 4, "auditor"));
    }

    [Fact]
    public void GetFindingActionsForUser_ScopesActionsWithFindingAccess()
    {
        using var db = SeedAuditData();
        var service = new AuditService(db);

        Assert.Equal(2, service.GetFindingActionsForUser(99, "audit_manager").Count);
        Assert.Single(service.GetFindingActionsForUser(2, "auditor"));
        Assert.Single(service.GetFindingActionsForUser(3, "finding_owner"));
        Assert.Empty(service.GetFindingActionsForUser(5, "finding_owner"));
    }

    private static RiskManagement.Data.AppDbContext SeedAuditData()
    {
        var db = TestDb.Create();
        db.Users.AddRange(
            new User { Id = 1, Username = "lead", FullName = "Lead Auditor", Role = "auditor" },
            new User { Id = 2, Username = "auditor", FullName = "Assigned Auditor", Role = "auditor" },
            new User { Id = 3, Username = "owner", FullName = "Finding Owner", Role = "finding_owner" },
            new User { Id = 4, Username = "outsider", FullName = "Outside User", Role = "user" },
            new User { Id = 99, Username = "manager", FullName = "Audit Manager", Role = "audit_manager" });

        db.InternalAudits.Add(new InternalAudit
        {
            Id = 10,
            Code = "ID-SEC-001",
            Title = "Restricted audit",
            Period = "2026",
            LeadAuditorId = 1
        });

        db.AuditFindings.AddRange(
            new AuditFinding
            {
                Id = 100,
                Code = "B-SEC-001",
                Title = "Assigned finding",
                AuditorId = 2,
                OwnerId = 3,
                InternalAuditId = 10
            },
            new AuditFinding
            {
                Id = 101,
                Code = "B-SEC-002",
                Title = "Other finding",
                AuditorId = 4,
                OwnerId = 4
            });

        db.AuditFindingActions.AddRange(
            new AuditFindingAction { Id = 1000, FindingId = 100, Description = "Fix control", CreatedById = 2 },
            new AuditFindingAction { Id = 1001, FindingId = 101, Description = "Other action", CreatedById = 4 });

        db.SaveChanges();
        return db;
    }
}
