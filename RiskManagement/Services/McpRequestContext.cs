using RiskManagement.Models;

namespace RiskManagement.Services;

/// <summary>
/// Her MCP isteği için doğrulanmış API key scope'unu taşır.
/// ScopeUser null ise tam erişim (admin davranışı).
/// </summary>
public class McpRequestContext
{
    public User? ScopeUser { get; set; }
    public bool IsFullAccess => ScopeUser is null;

    public string ScopeRole => ScopeUser?.Role ?? "admin";
    public int? ScopeUserId => ScopeUser?.Id;
}
