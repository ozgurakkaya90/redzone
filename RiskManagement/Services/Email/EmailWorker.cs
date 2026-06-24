using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(cfg.FromName, cfg.FromAddress));
        mime.To.Add(new MailboxAddress(msg.ToName ?? "", msg.To));
        mime.Subject = msg.Subject;
        mime.Body = new BodyBuilder { HtmlBody = msg.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 15_000 };
        await client.ConnectAsync(cfg.Host, cfg.Port, ResolveSocketOptions(cfg), ct);
        // Sunucu kimlik doğrulama istiyorsa (Username dolu) authenticate et; aksi halde anonim relay.
        if (!string.IsNullOrEmpty(cfg.Username))
            await client.AuthenticateAsync(cfg.Username, cfg.Password, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }

    /// <summary>
    /// Porta göre TLS modu — MailKit her iki TLS türünü de doğru destekler (System.Net.Mail aksine):
    /// 465 = implicit SSL (SslOnConnect), 587/UseSsl = explicit STARTTLS (zorunlu), diğer = fırsatçı.
    /// EmailWorker ve SystemConfig SMTP testi bu tek mantığı paylaşır.
    /// </summary>
    internal static SecureSocketOptions ResolveSocketOptions(EmailSettings cfg) =>
        cfg.Port == 465                       ? SecureSocketOptions.SslOnConnect
        : cfg.UseSsl || cfg.Port == 587       ? SecureSocketOptions.StartTls
        :                                       SecureSocketOptions.StartTlsWhenAvailable;
}
