namespace Blizztrack.Framework.IO
{
    /// <summary>
    /// This interface is used to access binary data to be interpreted by various TACT file format implementations.
    /// </summary>
    public interface IBinaryDataSupplier
    {
        public ReadOnlySpan<byte> this[Range range] { get; }
        public byte this[int offset] { get; }
        public byte this[Index index] { get; }

        public int Length { get; }
        public ReadOnlySpan<byte> Slice(int offset, int count) => this[offset..(offset + count)];
    }

    public interface IBinaryDataSupplier<T> : IBinaryDataSupplier where T : IBinaryDataSupplier<T>, allows ref struct
    {
        public static abstract implicit operator ReadOnlySpan<byte>(T value);
    }
}

