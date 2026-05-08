using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RiskManagement.Data;
using RiskManagement.Services;

namespace RiskManagement.Pages;

public class ResetPasswordModel(AppDbContext db, AuthService auth) : PageModel
{
    public string Error        { get; private set; } = "";
    public string Token        { get; private set; } = "";
    public bool   Success      { get; private set; }
    public bool   TokenInvalid { get; private set; }

    public void OnGet(string token)
    {
        Token = token ?? "";
        if (ValidToken(Token) == null)
            TokenInvalid = true;
    }

    public IActionResult OnPost(string token, string password, string confirm)
    {
        Token = token ?? "";

        var record = ValidToken(Token);
        if (record == null) { TokenInvalid = true; return Page(); }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            Error = "Şifre en az 8 karakter olmalıdır.";
            return Page();
        }
        if (password != confirm)
        {
            Error = "Şifreler eşleşmiyor.";
            return Page();
        }

        var user = db.Users.Find(record.UserId);
        if (user == null) { TokenInvalid = true; return Page(); }

        user.PasswordHash  = auth.HashPassword(password);
        foreach (var resetToken in db.PasswordResetTokens.Where(t => t.UserId == user.Id && !t.Used))
            resetToken.Used = true;
        db.SaveChanges();

        Success = true;
        return Page();
    }

    private RiskManagement.Models.PasswordResetToken? ValidToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var tokenHash = AuthService.HashResetToken(token);
        return db.PasswordResetTokens.FirstOrDefault(t =>
            t.Token == tokenHash && !t.Used && t.ExpiresAt > DateTime.UtcNow);
    }
}
