using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

public class AuthServicePrincipalTests
{
    private static AuthService BuildSvc(AppDbContext db)
        => new(db, new MemoryCache(new MemoryCacheOptions()));

    // ── BuildPrincipal → UserFromPrincipal roundtrip ──────────────────────────

    [Fact]
    public void BuildAndParse_SingleRole_Roundtrip()
    {
        var db   = TestDb.Create();
        var svc  = BuildSvc(db);
        var user = new User
        {
            Id = 42, Username = "test", FullName = "Test User",
            UserRoles = [new UserRole { RoleName = "risk_manager" }],
        };

        var principal = svc.BuildPrincipal(user);
        var parsed    = AuthService.UserFromPrincipal(principal);

        Assert.NotNull(parsed);
        Assert.Equal(42,            parsed.Id);
        Assert.Equal("test",        parsed.Username);
        Assert.Equal("Test User",   parsed.FullName);
        Assert.True(parsed.HasRole("risk_manager"));
    }

    [Fact]
    public void BuildAndParse_MultiRole_AllRolesPreserved()
    {
        var db  = TestDb.Create();
        var svc = BuildSvc(db);
        var user = new User
        {
            Id = 7, Username = "multi", FullName = "Multi User",
            UserRoles =
            [
                new UserRole { RoleName = "auditor" },
                new UserRole { RoleName = "finding_owner" },
                new UserRole { RoleName = "risk_owner" },
            ],
        };

        var principal = svc.BuildPrincipal(user);
        var parsed    = AuthService.UserFromPrincipal(principal);

        Assert.NotNull(parsed);
        Assert.True(parsed.HasRole("auditor"));
        Assert.True(parsed.HasRole("finding_owner"));
        Assert.True(parsed.HasRole("risk_owner"));
        Assert.False(parsed.HasRole("admin"));
    }

    [Fact]
    public void BuildAndParse_DeptOrgCompany_IdsPreserved()
    {
        var db  = TestDb.Create();
        var svc = BuildSvc(db);
        var user = new User
        {
            UserRoles        = [new UserRole { RoleName = "user" }],
            UserDepartments  = [new UserDepartment  { DepartmentId  = 10 }],
            UserOrganizations= [new UserOrganization{ OrganizationId = 20 }],
            UserCompanies    = [new UserCompany     { CompanyId      = 30 }],
        };

        var principal = svc.BuildPrincipal(user);
        var parsed    = AuthService.UserFromPrincipal(principal);

        Assert.NotNull(parsed);
        Assert.Contains(10, parsed.AllDepartmentIds);
        Assert.Contains(20, parsed.AllOrganizationIds);
        Assert.Contains(30, parsed.AllCompanyIds);
    }

    [Fact]
    public void UserFromPrincipal_Unauthenticated_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated
        Assert.Null(AuthService.UserFromPrincipal(principal));
    }

    [Fact]
    public void UserFromPrincipal_Null_ReturnsNull()
    {
        Assert.Null(AuthService.UserFromPrincipal(null));
    }

    // ── HasPermission ─────────────────────────────────────────────────────────

    [Fact]
    public void HasPermission_Admin_AlwaysTrue()
    {
        var db   = TestDb.Create();
        var svc  = BuildSvc(db);
        var user = new User { Id = 1, Role = "admin", UserRoles = [new UserRole { RoleName = "admin" }] };
        db.Users.Add(user); db.SaveChanges();

        Assert.True(svc.HasPermission(user, "risk.read"));
        Assert.True(svc.HasPermission(user, "risk.modify"));
        Assert.True(svc.HasPermission(user, "ethics.board_review"));
    }

    [Fact]
    public void HasPermission_RegularUser_OnlyAllowedPerms()
    {
        var db   = TestDb.Create();
        var svc  = BuildSvc(db);
        var user = new User { Id = 2, Role = "user", UserRoles = [new UserRole { UserId = 2, RoleName = "user" }] };
        db.Users.Add(user);
        db.RolePermissions.Add(new RolePermission { Role = "user", Permission = "risk.read" });
        db.RolePermissions.Add(new RolePermission { Role = "user", Permission = "risk.write" });
        db.SaveChanges();

        Assert.True(svc.HasPermission(user,  "risk.read"));
        Assert.True(svc.HasPermission(user,  "risk.write"));
        Assert.False(svc.HasPermission(user, "risk.modify"));
        Assert.False(svc.HasPermission(user, "audit.read"));
    }

    // ── User.HasAnyRole / AllRoleNames ────────────────────────────────────────

    [Fact]
    public void HasAnyRole_PrimaryRole_MatchesWithoutUserRoles()
    {
        var user = new User { Role = "committee" };
        Assert.True(user.HasAnyRole("committee", "admin"));
        Assert.False(user.HasAnyRole("user", "auditor"));
    }

    [Fact]
    public void AllRoleNames_NoDuplicates_WhenPrimaryAndUserRolesSame()
    {
        var user = new User
        {
            Role = "auditor",
            UserRoles = [new UserRole { RoleName = "auditor" }],
        };
        Assert.Single(user.AllRoleNames);
    }

    // ── Pasif kullanıcı — yerel giriş engeli ─────────────────────────────────

    [Fact]
    public async Task ValidateAsync_InactiveUser_ReturnsNull()
    {
        var db  = TestDb.Create();
        var svc = BuildSvc(db);

        db.Users.Add(new User
        {
            Username     = "eski_calisan",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("sifre123"),
            FullName     = "Eski Çalışan",

            Role         = "user",
            IsActive     = false,   // pasife alındı
        });
        db.SaveChanges();

        var result = await svc.ValidateAsync("eski_calisan", "sifre123");

        // Doğru şifre olsa bile pasif kullanıcı giriş yapamamalı
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_LdapUser_LocalLoginReturnsNull()
    {
        var db  = TestDb.Create();
        var svc = BuildSvc(db);

        db.Users.Add(new User
        {
            Username     = "ldap_user",
            PasswordHash = "$ldap$",
            FullName     = "AD Kullanıcısı",

            Role         = "user",
            IsActive     = true,
        });
        db.SaveChanges();

        // Herhangi bir şifreyle yerel giriş denenmeli → null, exception değil
        var result = await svc.ValidateAsync("ldap_user", "herhangi_sifre");
        Assert.Null(result);

        var result2 = await svc.ValidateAsync("ldap_user", "$ldap$");
        Assert.Null(result2);
    }

    [Fact]
    public async Task ValidateAsync_ActiveUser_ReturnsUser()
    {
        var db  = TestDb.Create();
        var svc = BuildSvc(db);

        db.Users.Add(new User
        {
            Username     = "aktif_kullanici",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("sifre123"),
            FullName     = "Aktif",

            Role         = "user",
            IsActive     = true,
        });
        db.SaveChanges();

        var result = await svc.ValidateAsync("aktif_kullanici", "sifre123");

        Assert.NotNull(result);
        Assert.Equal("aktif_kullanici", result.Username);
    }
}
