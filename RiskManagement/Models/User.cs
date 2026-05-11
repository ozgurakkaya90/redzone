using System.ComponentModel.DataAnnotations;

namespace RiskManagement.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; } = "";

    public string? PasswordHash { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = "";

    [MaxLength(200)]
    public string Department { get; set; } = "";  // legacy display string

    public int? DepartmentId { get; set; }
    public int? OrganizationId { get; set; }
    public int? CompanyId { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [Required, MaxLength(30)]
    public string Role { get; set; } = "user";    // primary/display role

    [Required, MaxLength(10)]
    public string AuthType { get; set; } = "local";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Nav — tekil (primary)
    public Models.Department? DepartmentNav { get; set; }
    public Models.Organization? OrganizationNav { get; set; }
    public Models.Company? CompanyNav { get; set; }

    // Nav — çoklu atamalar
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserDepartment> UserDepartments { get; set; } = [];
    public ICollection<UserOrganization> UserOrganizations { get; set; } = [];
    public ICollection<UserCompany> UserCompanies { get; set; } = [];

    public ICollection<Risk> ProposedRisks { get; set; } = [];
    public ICollection<Risk> OwnedRisks { get; set; } = [];

    // Yardımcılar
    public bool HasRole(string role) =>
        Role == role || UserRoles.Any(r => r.RoleName == role);

    public bool HasAnyRole(params string[] roles) =>
        roles.Contains(Role) || UserRoles.Any(r => roles.Contains(r.RoleName));

    public IEnumerable<string> AllRoleNames =>
        UserRoles.Select(r => r.RoleName)
                 .Prepend(Role)
                 .Where(r => !string.IsNullOrEmpty(r))
                 .Distinct();

    public IEnumerable<int> AllDepartmentIds =>
        UserDepartments.Select(d => d.DepartmentId)
                       .Concat(DepartmentId.HasValue ? [DepartmentId.Value] : Array.Empty<int>())
                       .Distinct();

    public IEnumerable<int> AllOrganizationIds =>
        UserOrganizations.Select(o => o.OrganizationId)
                         .Concat(OrganizationId.HasValue ? [OrganizationId.Value] : Array.Empty<int>())
                         .Distinct();

    public IEnumerable<int> AllCompanyIds =>
        UserCompanies.Select(c => c.CompanyId)
                     .Concat(CompanyId.HasValue ? [CompanyId.Value] : Array.Empty<int>())
                     .Distinct();
}

// ── Çoklu atama join tabloları ───────────────────────────────────────────────

public class UserRole
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string RoleName { get; set; } = "";
    public User User { get; set; } = null!;
}

public class UserDepartment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int DepartmentId { get; set; }
    public User User { get; set; } = null!;
    public Department Department { get; set; } = null!;
}

public class UserOrganization
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int OrganizationId { get; set; }
    public User User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}

public class UserCompany
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public User User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}

public class PasswordResetToken
{
    public int Id { get; set; }
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
