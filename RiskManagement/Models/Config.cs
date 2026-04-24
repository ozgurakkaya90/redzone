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

public class AppConfig
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = ""; // JSON encoded
}
