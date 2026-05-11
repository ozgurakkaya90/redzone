using System.Net;
using System.Net.Mail;

namespace RiskManagement.Services.Email;

/// <summary>
/// Arka planda çalışan e-posta gönderici.
/// EmailQueue'dan mesaj okur, ayarları her gönderimde DB'den alır (yeniden başlatma gerekmez).
/// </summary>
public sealed class EmailWorker(
    EmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailWorker> logger) : BackgroundService
{
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("E-posta servisi başladı.");

        await foreach (var msg in queue.Reader.ReadAllAsync(ct))
        {
            var settings = GetSettings();

            if (!settings.Enabled)
            {
                logger.LogDebug("E-posta devre dışı — atlandı: {To}", msg.To);
                continue;
            }

            await SendWithRetryAsync(msg, settings, ct);
        }
    }

    private EmailSettings GetSettings()
    {
        using var scope = scopeFactory.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<ConfigService>();
        return cfg.GetEmailSettings();
    }

    private async Task SendWithRetryAsync(EmailMessage msg, EmailSettings cfg, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await SendAsync(msg, cfg, ct);
                logger.LogInformation("E-posta gönderildi: {To} — {Subject}", msg.To, msg.Subject);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 5);
                logger.LogWarning("E-posta başarısız (deneme {A}/{M}), {D}s sonra tekrar: {Err}",
                    attempt, MaxRetries, delay.TotalSeconds, ex.Message);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                logger.LogError("E-posta kalıcı hata: {To} — {Subject} — {Err}",
                    msg.To, msg.Subject, ex.Message);
            }
        }
    }

    private static async Task SendAsync(EmailMessage msg, EmailSettings cfg, CancellationToken ct)
    {
        using var client = new SmtpClient(cfg.Host, cfg.Port)
        {
            EnableSsl      = cfg.UseSsl,
            Credentials    = new NetworkCredential(cfg.Username, cfg.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout        = 15_000,
        };

        using var mail = new MailMessage
        {
            From       = new MailAddress(cfg.FromAddress, cfg.FromName),
            Subject    = msg.Subject,
            Body       = msg.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(new MailAddress(msg.To, msg.ToName));

        await client.SendMailAsync(mail, ct);
    }
}
