using Blizztrack.Framework.Ribbit;

using System.Threading.Channels;

using Version = Blizztrack.Framework.Ribbit.Version;

namespace Blizztrack.Services
{
    /// <summary>
    /// Event bus service.
    /// </summary>
    public class MediatorService
    {
        public readonly ProductBus Products = new();
    }

    public readonly struct ProductBus()
    {
        /// <summary>
        /// A channel that publishes a value whenever a CDN configuration for a product is updated.
        /// </summary>
        public readonly Channel<(string Product, CDN CDN)> OnCDNs = Channel.CreateBounded<(string, CDN)>(new BoundedChannelOptions(128)
        {
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        /// <summary>
        /// A channel that publishes a value whenever a version configuration for a product is updated.
        /// </summary>
        public readonly Channel<(string Product, Version Versions)> OnVersions = Channel.CreateBounded<(string, Version)>(new BoundedChannelOptions(128)
        {
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        /// <summary>
        /// A channel that publishes a value whenever a BGDL configuration for a product is updated.
        /// </summary>
        public readonly Channel<(string Product, Version BGDL)> OnBGDL = Channel.CreateBounded<(string, Version)>(new BoundedChannelOptions(128)
        {
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        /// <summary>
        /// A channel that publishes a value whenever either of the sequence numbers changed.
        /// <para>
        /// The payload for this channel consists of the name of the product that changed as well as all last up-to-date sequence numbers
        /// for this product. They are ordered according to <see cref="SequenceNumberType" />, that is, to recover the BGDL sequence number, one
        /// would write <c>sequenceNumbers[(int) <see cref="SequenceNumberType.BGDL"/>]</c>. A value of <c>0</c> should be ignored.
        /// </para>
        /// </summary>
        public readonly Channel<(string Product, int[] SequenceNumbers)> OnSummary = Channel.CreateBounded<(string, int[])>(new BoundedChannelOptions(128)
        {
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        public static async Task<Channel<V>> CombineLatest<T, U, V>(Channel<T> left, Channel<U> right, Func<T, U, V> combiner,
            Func<Channel<V>> channelSupplier,
            CancellationToken stoppingToken = default)
        {
            var outboundChannel = channelSupplier();

            var leftTask = left.Reader.ReadAllAsync(stoppingToken).GetAsyncEnumerator(stoppingToken);
            var rightTask = right.Reader.ReadAllAsync(stoppingToken).GetAsyncEnumerator(stoppingToken);

            try
            {
                var currentLeftValue = default(T?);
                var currentRightValue = default(U?);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var leftMoveNext = leftTask.MoveNextAsync(stoppingToken).AsTask();
                    var rightMoveNext = rightTask.MoveNextAsync(stoppingToken).AsTask();

                    await foreach (var completedTask in Task.WhenEach(leftMoveNext, rightMoveNext))
                    {
                        if (completedTask == leftMoveNext && leftMoveNext.Result)
                            currentLeftValue = leftTask.Current;
                        else if (completedTask == rightMoveNext && rightMoveNext.Result)
                            currentRightValue = rightTask.Current;
                    }

                    if (currentLeftValue is not null && currentRightValue is not null)
                        await outboundChannel.Writer.WriteAsync(combiner(currentLeftValue!, currentRightValue!), stoppingToken);
                }
            }
            finally
            {
                await leftTask.DisposeAsync();
                await rightTask.DisposeAsync();
            }

            outboundChannel.Writer.Complete();

            return outboundChannel;
        }
    }
}
