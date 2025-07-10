namespace Blizztrack.Shared.IO
{
    public interface IDataSource : IDisposable
    {
        public ReadOnlySpan<byte> this[Range range] { get; }
        public ReadOnlySpan<byte> Slice(int offset, int length);

        public byte this[Index index] { get; }
        public byte this[int index] { get; }
    }
}
