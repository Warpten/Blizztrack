using System.Threading.Channels;

namespace Blizztrack.IO.Channels
{
    public static class ChannelReaderExtensions
    {
        public static ChannelReader<T>[] Multicast<T>(this ChannelReader<T> source, int count, CancellationToken stoppingSource, Func<int, Channel<T>>? channelSupplier = default)
        {
            channelSupplier ??= _ => Channel.CreateUnbounded<T>();

            var channels = new Channel<T>[count];
            for (var i = 0; i < count; ++i)
                channels[i] = channelSupplier(i);

            _ = Task.Run(async () =>
            {
                await foreach (var item in source.ReadAllAsync(stoppingSource))
                    for (var i = 0; i < count; ++i)
                        await channels[i].Writer.WriteAsync(item, stoppingSource);
            }, stoppingSource).ContinueWith(task =>
            {
                for (var i = 0; i < count; ++i)
                    channels[i].Writer.TryComplete(task.Exception);
            }, stoppingSource);

            return [.. channels.Select(c => c.Reader)];
        }
    }
}
