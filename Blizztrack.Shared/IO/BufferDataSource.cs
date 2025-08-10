using System.Runtime.CompilerServices;

namespace Blizztrack.Shared.IO
{
    public readonly struct BufferDataSource(byte[] dataBuffer) : IDataSource
    {
        public void Dispose() { }

        public ReadOnlySpan<byte> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => dataBuffer.AsSpan(range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> Slice(int offset, int length) => dataBuffer.AsSpan().Slice(offset, length);

        public byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => dataBuffer[index];
        }

        public byte this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => dataBuffer[index];
        }
    }
    public readonly ref struct SpanDataSource(ReadOnlySpan<byte> dataBuffer) : IDataSource
    {
        private readonly ReadOnlySpan<byte> _dataBuffer = dataBuffer;

        public void Dispose() { }

        public ReadOnlySpan<byte> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataBuffer[range];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> Slice(int offset, int length) => _dataBuffer.Slice(offset, length);

        public byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataBuffer[index];
        }

        public byte this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataBuffer[index];
        }
    }

    public static partial class BufferDataSourceExtensions
    {
        public static BufferDataSource ToDataSource(this byte[] data) => new(data);
        public static SpanDataSource ToDataSource(this ReadOnlySpan<byte> data) => new(data);
    }
}
