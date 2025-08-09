using System.Diagnostics;

namespace Blizztrack.Framework.TACT
{

    /// <summary>
    /// Tag interface for encoding keys.
    /// </summary>
    public interface IEncodingKey : IKey;

    public interface IEncodingKey<T> : IKey<T>, IEncodingKey where T : IEncodingKey<T>, allows ref struct
    {
        public static abstract bool operator ==(T left, T right);
        public static abstract bool operator !=(T left, T right);
    }

    /// <summary>
    /// A stack-allocated, non-owning variation of <see cref="EncodingKey" />.
    /// </summary>
    /// <param name="data">The span of data that represents a content key.</param>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly ref struct EncodingKeyRef(ReadOnlySpan<byte> data) : IKeyView<EncodingKeyRef, EncodingKey>, IEncodingKey<EncodingKeyRef>
    {
        private readonly ReadOnlySpan<byte> _data = data;
        public byte this[int offset] => _data[offset];
        public int Length => _data.Length;

        internal unsafe EncodingKeyRef(byte* keyData, int keyLength) : this(new(keyData, keyLength)) { }

        public unsafe ReadOnlySpan<byte> AsSpan() => _data;
        public bool SequenceEqual<U>(U other) where U : notnull, IKey, allows ref struct => _data.SequenceEqual(other.AsSpan());
        public string AsHexString() => Convert.ToHexStringLower(_data);

        public static bool operator ==(EncodingKeyRef left, EncodingKeyRef right) => left._data.SequenceEqual(right._data);
        public static bool operator !=(EncodingKeyRef left, EncodingKeyRef right) => !left._data.SequenceEqual(right._data);

        static EncodingKeyRef IKey<EncodingKeyRef>.From(ReadOnlySpan<byte> data) => new(data);

        public EncodingKey AsOwned() => new(_data);

        public override bool Equals(object? obj) => obj is IKey key && key.AsSpan().SequenceEqual(_data);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.AddBytes(_data);
            return hc.ToHashCode();
        }

        public bool Equals(EncodingKeyRef other) => other._data.SequenceEqual(_data);

        public string DebuggerDisplay => AsHexString();
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct EncodingKey(byte[] data) : IOwnedKey<EncodingKey, EncodingKeyRef>, IEncodingKey<EncodingKey>
    {
        private readonly byte[] _data = data;
        public byte this[int offset] => _data[offset];
        public int Length => _data.Length;

        public static EncodingKey Zero { get; } = new([]);

        public EncodingKey(ReadOnlySpan<byte> data) : this(data.ToArray()) { }

        public unsafe ReadOnlySpan<byte> AsSpan() => _data;
        public bool SequenceEqual<U>(U other) where U : notnull, IKey, allows ref struct => _data.AsSpan().SequenceEqual(other.AsSpan());
        public string AsHexString() => Convert.ToHexStringLower(_data);

        public static bool operator ==(EncodingKey left, EncodingKey right) => left._data?.SequenceEqual(right._data ?? []) ?? (right._data == null);
        public static bool operator !=(EncodingKey left, EncodingKey right) => !(left == right);

        static EncodingKey IKey<EncodingKey>.From(ReadOnlySpan<byte> data) => new(data);

        public override bool Equals(object? obj) => obj is IKey key && key.AsSpan().SequenceEqual(_data);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.AddBytes(_data);
            return hc.ToHashCode();
        }

        public bool Equals(EncodingKey other) => other._data.SequenceEqual(_data);

        public EncodingKeyRef AsRef() => new(_data);

        public string DebuggerDisplay => AsHexString();
    }

}
