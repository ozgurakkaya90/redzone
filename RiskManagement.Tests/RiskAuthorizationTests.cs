using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

public class RiskAuthorizationTests
{
    [Fact]
    public void CanAccessRisk_AllowsOnlyExpectedPeople()
    {
        using var db = TestDb.Create();
        db.Organizations.Add(new Organization { Id = 10, Name = "Operations" });
        db.Departments.Add(new Department { Id = 20, Name = "Safety", OrganizationId = 10, ManagerUserId = 4 });
        db.SaveChanges();

        var cfg = TestDb.CreateConfigService(db);
        var service = new RiskService(db, new RiskCalculator(cfg), new AuthService(db, new MemoryCache(new MemoryCacheOptions())));
        var risk = new Risk
        {
            Id = 100,
            Code = "R-SEC-001",
            Title = "Restricted risk",
            OrganizationId = 10,
            DepartmentId = 20,   // dept 20'nin müdürü userId=4
            ProposedById = 2,
            OwnerId = 3
        };

        Assert.True(service.CanAccessRisk(risk, 99, "admin"));
        Assert.True(service.CanAccessRisk(risk, 99, "committee"));
        Assert.True(service.CanAccessRisk(risk, 2,  "user"));       // önerici
        Assert.True(service.CanAccessRisk(risk, 3,  "risk_owner")); // sahip
        Assert.True(service.CanAccessRisk(risk, 4,  "user"));       // dept müdürü
        Assert.False(service.CanAccessRisk(risk, 5, "user"));       // ilgisiz kullanıcı
    }

    [Fact]
    public void GetByIdForUser_ReturnsNullForUnrelatedUser()
    {
        using var db = TestDb.Create();
        db.Organizations.Add(new Organization { Id = 10, Name = "Operations" });
        db.Risks.Add(new Risk
        {
            Id = 100,
            Code = "R-SEC-001",
            Title = "Restricted risk",
            OrganizationId = 10,
            ProposedById = 2,
            OwnerId = 3
        });
        db.SaveChanges();

        var cfg = TestDb.CreateConfigService(db);
        var service = new RiskService(db, new RiskCalculator(cfg), new AuthService(db, new MemoryCache(new MemoryCacheOptions())));

        Assert.NotNull(service.GetByIdForUser(100, 2, "user"));
        Assert.Null(service.GetByIdForUser(100, 5, "user"));
    }
}
