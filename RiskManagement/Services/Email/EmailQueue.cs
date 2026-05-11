using System.Threading.Channels;

namespace RiskManagement.Services.Email;

/// <summary>
/// Fire-and-forget e-posta kuyruğu.
/// Singleton olarak kaydedilir; hem iş servisleri hem de EmailWorker bu nesneyi paylaşır.
/// </summary>
public sealed class EmailQueue
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
        {
            FullMode    = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public ChannelWriter<EmailMessage> Writer => _channel.Writer;
    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public void Enqueue(EmailMessage msg) =>
        _channel.Writer.TryWrite(msg);
}
