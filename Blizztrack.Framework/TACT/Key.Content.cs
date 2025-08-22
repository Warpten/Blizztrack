using Blizztrack.Framework.TACT.Views;

using Pidgin.Expression;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.TACT
{
    /// <summary>
    /// Tag interface for content keys.
    /// </summary>
    public interface IContentKey<T> : IKey<T> where T : struct, IContentKey<T>, allows ref struct;

    [DebuggerTypeProxy(typeof(DebugView))]
    public readonly struct ContentKey(byte[] data) : IContentKey<ContentKey>, IOwnedKey<ContentKey>
    {
        private readonly byte[] _data = data;
        public byte this[int offset] => _data[offset];
        public int Length => _data.Length;

        public static ContentKey Zero { get; } = new([]);

        public ContentKey(ReadOnlySpan<byte> data) : this(data.ToArray()) { }

        public unsafe ReadOnlySpan<byte> AsSpan() => _data;
        public string AsHexString() => Convert.ToHexStringLower(_data);

        static ContentKey IKey<ContentKey>.From(ReadOnlySpan<byte> data) => new(data);

        public static implicit operator Views.ContentKey(ContentKey self) => new(self._data);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(ContentKey self) => !self._data.AsSpan().ContainsAnyExcept([(byte)0]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator true(ContentKey self) => self._data.AsSpan().ContainsAnyExcept([(byte)0]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator false(ContentKey self) => !self._data.AsSpan().ContainsAnyExcept([(byte)0]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !(ContentKey self) => !self._data.AsSpan().ContainsAnyExcept([(byte)0]);
    }

    public static class ContentKeyExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SequenceEqual<T, U>(this T self, in U other)
            where T : struct, IContentKey<T>, allows ref struct
            where U : struct, IContentKey<U>, allows ref struct
            => self.AsSpan().SequenceEqual(other.AsSpan());
    }

    namespace Views
    {
        /// <summary>
        /// A stack-allocated, non-owning variation of <see cref="ContentKey" />.
        /// </summary>
        /// <param name="data">The span of data that represents a content key.</param>
        [DebuggerTypeProxy(typeof(DebugView))]
        public readonly ref struct ContentKey(ReadOnlySpan<byte> data) : IContentKey<ContentKey>
        {
            private readonly ReadOnlySpan<byte> _data = data;

            public byte this[int offset] => _data[offset];
            public int Length => _data.Length;

            public unsafe ReadOnlySpan<byte> AsSpan() => _data;
            public string AsHexString() => Convert.ToHexStringLower(_data);

            public TACT.ContentKey Upgrade() => new(_data);

            static ContentKey IKey<ContentKey>.From(ReadOnlySpan<byte> data) => new(data);


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator true(ContentKey self) => self._data.ContainsAnyExcept([(byte)0]);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator false(ContentKey self) => !self._data.ContainsAnyExcept([(byte)0]);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !(ContentKey self) => !self._data.ContainsAnyExcept([(byte)0]);
        }
    }

    file class DebugView(ReadOnlySpan<byte> data)
    {
        private readonly byte[] _dataBuffer = [.. data];

        public override string ToString() => Convert.ToHexStringLower(_dataBuffer);
    }
}
