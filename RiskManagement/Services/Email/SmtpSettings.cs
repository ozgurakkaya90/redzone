namespace RiskManagement.Services.Email;

public class SmtpSettings
{
    public bool   Enabled     { get; set; } = false;
    public string Host        { get; set; } = "";
    public int    Port        { get; set; } = 587;
    public bool   UseSsl      { get; set; } = true;
    public string Username    { get; set; } = "";
    public string Password    { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName    { get; set; } = "RedZone Risk Sistemi";

    /// <summary>Uygulamanın dış erişim adresi — e-posta linklerinde kullanılır.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5000";
}
