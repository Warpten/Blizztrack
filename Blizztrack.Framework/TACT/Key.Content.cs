using System.Diagnostics;

namespace Blizztrack.Framework.TACT
{
    /// <summary>
    /// Tag interface for content keys.
    /// </summary>
    public interface IContentKey : IKey { }

    public interface IContentKey<T> : IKey<T>, IContentKey where T : IContentKey<T>, allows ref struct
    {
        public static abstract bool operator ==(T left, T right);
        public static abstract bool operator !=(T left, T right);
    }

    /// <summary>
    /// A stack-allocated, non-owning variation of <see cref="ContentKey" />.
    /// </summary>
    /// <param name="data">The span of data that represents a content key.</param>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly ref struct ContentKeyRef(ReadOnlySpan<byte> data) : IKeyView<ContentKeyRef, ContentKey>, IContentKey<ContentKeyRef>
    {
        private readonly ReadOnlySpan<byte> _data = data;

        public byte this[int offset] => _data[offset];
        public int Length => _data.Length;

        public unsafe ReadOnlySpan<byte> AsSpan() => _data;
        public bool SequenceEqual<U>(U other) where U : notnull, IKey, allows ref struct => _data.SequenceEqual(other.AsSpan());
        public string AsHexString() => Convert.ToHexStringLower(_data);

        public static bool operator ==(ContentKeyRef left, ContentKeyRef right) => left._data.SequenceEqual(right._data);
        public static bool operator !=(ContentKeyRef left, ContentKeyRef right) => !left._data.SequenceEqual(right._data);

        static ContentKeyRef IKey<ContentKeyRef>.From(ReadOnlySpan<byte> data) => new(data);

        public ContentKey AsOwned() => new(_data);

        public override bool Equals(object? obj) => obj is IKey key && SequenceEqual(key);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.AddBytes(_data);
            return hc.ToHashCode();
        }

        public bool Equals(ContentKeyRef other) => other._data.SequenceEqual(_data);

        public string DebuggerDisplay => AsHexString();
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct ContentKey(byte[] data) : IOwnedKey<ContentKey, ContentKeyRef>, IContentKey<ContentKey>
    {
        private readonly byte[] _data = data;
        public byte this[int offset] => _data[offset];
        public int Length => _data.Length;

        public static ContentKey Zero { get; } = new([]);

        public ContentKey(ReadOnlySpan<byte> data) : this(data.ToArray()) { }

        public unsafe ReadOnlySpan<byte> AsSpan() => _data;
        public bool SequenceEqual<U>(U other) where U : notnull, IKey, allows ref struct => _data.AsSpan().SequenceEqual(other.AsSpan());
        public string AsHexString() => Convert.ToHexStringLower(_data);

        public static bool operator ==(ContentKey left, ContentKey right) => left._data?.SequenceEqual(right._data ?? []) ?? (right._data == null);
        public static bool operator !=(ContentKey left, ContentKey right) => !(left == right);

        static ContentKey IKey<ContentKey>.From(ReadOnlySpan<byte> data) => new(data);

        public override bool Equals(object? obj) => obj is IKey key && key.AsSpan().SequenceEqual(_data);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.AddBytes(_data);
            return hc.ToHashCode();
        }

        public bool Equals(ContentKey other) => other._data.SequenceEqual(_data);

        public ContentKeyRef AsRef() => new(_data);

        public string DebuggerDisplay => AsHexString();
    }
}
