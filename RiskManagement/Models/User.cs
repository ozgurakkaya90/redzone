namespace RiskManagement.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = "";
    public string Department { get; set; } = "";
    public string Role { get; set; } = "user";
    public string AuthType { get; set; } = "local";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Nav
    public ICollection<Risk> ProposedRisks { get; set; } = [];
    public ICollection<Risk> OwnedRisks { get; set; } = [];
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
