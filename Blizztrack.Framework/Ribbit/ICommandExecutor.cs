namespace Blizztrack.Framework.Ribbit
{
    public interface ICommandExecutor
    {
        public abstract IAsyncEnumerable<ArraySegment<byte>> Apply(IAsyncEnumerable<ArraySegment<byte>> source, CancellationToken stoppingToken);
    }
}
