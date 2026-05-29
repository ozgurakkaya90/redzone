using System.Collections.Concurrent;
using System.Threading.Channels;

namespace RiskManagement.Services.Email;

public enum EmailLogStatus { Pending, Sent, Failed, Skipped }

public record EmailLogEntry(
    DateTime Timestamp,
    string To,
    string Subject,
    EmailLogStatus Status,
    string? Error = null,
    int Attempt = 1);

/// <summary>
/// Fire-and-forget e-posta kuyruğu + son gönderim logları.
/// Singleton olarak kaydedilir; hem iş servisleri hem de EmailWorker bu nesneyi paylaşır.
/// </summary>
public sealed class EmailQueue
{
    private const int MaxLogEntries = 200;

    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private readonly ConcurrentQueue<EmailLogEntry> _log = new();

    public ChannelWriter<EmailMessage> Writer => _channel.Writer;
    public ChannelReader<EmailMessage> Reader  => _channel.Reader;

    public int PendingCount => _channel.Reader.Count;

    public void Enqueue(EmailMessage msg) =>
        _channel.Writer.TryWrite(msg);

    public void Log(EmailLogEntry entry)
    {
        _log.Enqueue(entry);
        // Sabit boyutu koru
        while (_log.Count > MaxLogEntries)
            _log.TryDequeue(out _);
    }

    public IReadOnlyList<EmailLogEntry> GetLog() =>
        _log.Reverse().ToList();
}
