using System.ComponentModel.DataAnnotations;

namespace RiskManagement.Models;

public class LdapConfiguration
{
    public int Id { get; set; }
    public bool Enabled { get; set; } = false;
    public string? Server { get; set; }
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; } = false;
    public bool UseTls { get; set; } = false;
    public int Timeout { get; set; } = 10;
    public string? BindDn { get; set; }
    public string? BindPassword { get; set; }
    public string? UserSearchBase { get; set; }
    public string UserSearchFilter { get; set; } = "(sAMAccountName={username})";
    public string AttrDisplayName { get; set; } = "displayName";
    public string AttrEmail { get; set; } = "mail";
    public string AttrDepartment { get; set; } = "department";
    public bool AutoCreateUsers { get; set; } = false;
    public string DefaultRole { get; set; } = "user";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class RolePermission
{
    public int Id { get; set; }
    public string Role { get; set; } = "";
    public string Permission { get; set; } = "";
}

public class CustomRole
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";   // slug: "muhasebe_muduru"

    [Required, MaxLength(200)]
    public string Label { get; set; } = "";  // görünen ad: "Muhasebe Müdürü"

    public bool IsSystem { get; set; }       // true = yerleşik rol, silinemez

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppConfig
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = ""; // JSON encoded
}

public class Company
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    public ICollection<Organization> Organizations { get; set; } = [];
}

public class Organization
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public ICollection<Department> Departments { get; set; } = [];
}

public class Department
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public int? ManagerUserId { get; set; }
    public User? Manager { get; set; }
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

public class ConfigChangeLog
{
    public int Id { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = "";
    public string SettingKey { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Description { get; set; }
}

public class DatabaseConnectionConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "mysql"; // mysql, postgresql, sqlserver, sqlite
    public string Host { get; set; } = "";
    public int Port { get; set; } = 3306;
    public string DatabaseName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = false;
    public int ConnectTimeout { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTestedAt { get; set; }
    public bool? LastTestSuccess { get; set; }
    public string? LastTestMessage { get; set; }
}
