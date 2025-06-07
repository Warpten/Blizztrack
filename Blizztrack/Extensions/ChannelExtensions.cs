using System.Threading.Channels;

namespace Blizztrack.Extensions
{
    public static class ChannelExtensions
    {
        public static ValueTask Push<T>(this Channel<T> channel, T value, CancellationToken stoppingToken = default)
            => channel.Writer.WriteAsync(value, stoppingToken);
    }
}
