using System.Threading.Channels;
using NewHarian.Domain.Entities;

namespace NewHarian.Infrastructure.Logging;

/// <summary>Bounded channel between DbLogger and LogWriterHostedService.</summary>
public sealed class AppLogQueue
{
    private readonly Channel<AppLogEntry> _channel = Channel.CreateBounded<AppLogEntry>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryWrite(AppLogEntry entry) => _channel.Writer.TryWrite(entry);

    public ChannelReader<AppLogEntry> Reader => _channel.Reader;
}
