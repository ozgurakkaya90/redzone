using System.Net;
using System.Net.Mail;

namespace RiskManagement.Services.Email;

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
                queue.Log(new EmailLogEntry(DateTime.Now, msg.To, msg.Subject, EmailLogStatus.Skipped,
                    "E-posta bildirimleri devre dışı"));
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
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await SendAsync(msg, cfg, ct);
                logger.LogInformation("E-posta gönderildi: {To} — {Subject}", msg.To, msg.Subject);
                queue.Log(new EmailLogEntry(DateTime.Now, msg.To, msg.Subject, EmailLogStatus.Sent, Attempt: attempt));
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                lastEx = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 5);
                logger.LogWarning("E-posta başarısız (deneme {A}/{M}), {D}s sonra tekrar: {Err}",
                    attempt, MaxRetries, delay.TotalSeconds, ex.Message);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                lastEx = ex;
                logger.LogError("E-posta kalıcı hata: {To} — {Subject} — {Err}",
                    msg.To, msg.Subject, ex.Message);
            }
        }

        queue.Log(new EmailLogEntry(DateTime.Now, msg.To, msg.Subject, EmailLogStatus.Failed,
            lastEx?.Message ?? "Bilinmeyen hata", Attempt: MaxRetries));
    }

    internal static async Task SendAsync(EmailMessage msg, EmailSettings cfg, CancellationToken ct = default)
    {
        // Port 587 STARTTLS kullanır: .NET SmtpClient EnableSsl=false iken
        // STARTTLS'i otomatik müzakere eder. EnableSsl=true yalnızca
        // port 465 (implicit SSL) için doğrudur.
        var useSsl = cfg.UseSsl || cfg.Port == 465;

        using var client = new SmtpClient(cfg.Host, cfg.Port)
        {
            EnableSsl      = useSsl,
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
