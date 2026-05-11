namespace RiskManagement.Services.Email;

/// <summary>
/// E-posta yapılandırması — veritabanında "email_settings" key'i altında saklanır.
/// ConfigService üzerinden okunur/yazılır; yeniden başlatma gerektirmez.
/// </summary>
public class EmailSettings
{
    public bool   Enabled     { get; set; } = false;
    public string Host        { get; set; } = "";
    public int    Port        { get; set; } = 587;
    public bool   UseSsl      { get; set; } = true;
    public string Username    { get; set; } = "";
    public string Password    { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName    { get; set; } = "RedZone Risk Sistemi";
    public string BaseUrl     { get; set; } = "http://localhost:5000";
}
