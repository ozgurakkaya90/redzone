using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;

namespace RiskManagement.Pages;

public class ForgotPasswordModel(AppDbContext db) : PageModel
{
    public string Error     { get; private set; } = "";
    public string Username  { get; private set; } = "";
    public bool   Submitted { get; private set; }

    public void OnGet() { }

    public IActionResult OnPost(string username)
    {
        Username = username?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(Username))
        {
            Error = "Kullanıcı adı boş bırakılamaz.";
            return Page();
        }

        var user = db.Users.FirstOrDefault(u => u.Username == Username && u.IsActive);

        if (user != null)
        {
            var recentRequestCutoff = DateTime.UtcNow - AuthService.PasswordResetRequestCooldown;
            var hasRecentRequest = db.PasswordResetTokens.Any(t =>
                t.UserId == user.Id && !t.Used && t.CreatedAt >= recentRequestCutoff);
            if (hasRecentRequest)
            {
                Submitted = true;
                return Page();
            }

            // Invalidate previous unused tokens while keeping an audit trail
            var old = db.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.Used)
                .ToList();
            foreach (var token in old)
                token.Used = true;

            var resetToken = AuthService.GenerateResetToken();

            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId    = user.Id,
                Token     = AuthService.HashResetToken(resetToken),
                ExpiresAt = DateTime.UtcNow.Add(AuthService.PasswordResetTokenLifetime),
            });
            db.SaveChanges();
        }

        // Always show the same confirmation — don't reveal whether user exists
        Submitted = true;
        return Page();
    }
}
