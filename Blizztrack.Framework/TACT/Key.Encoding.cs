using Blizztrack.Framework.TACT.Views;

using Pidgin.Expression;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.TACT
{
    /// <summary>
    /// Tag interface for encoding keys.
    /// </summary>
    public interface IEncodingKey<T> : IKey<T> where T : struct, IEncodingKey<T>, allows ref struct;

    [DebuggerTypeProxy(typeof(DebugView))]
    public readonly struct EncodingKey(byte[] data) : IEncodingKey<EncodingKey>, IOwnedKey<EncodingKey>
    {
        private readonly byte[] _data = data;
        public byte this[int offset] => _data[offset];
        public int Length => _data.Length;

        public static EncodingKey Zero { get; } = new([]);

        public EncodingKey(ReadOnlySpan<byte> data) : this(data.ToArray()) { }

        public unsafe ReadOnlySpan<byte> AsSpan() => _data;
        public string AsHexString() => Convert.ToHexStringLower(_data);

        static EncodingKey IKey<EncodingKey>.From(ReadOnlySpan<byte> data) => new(data);

        public static implicit operator Views.EncodingKey(EncodingKey self) => new(self._data);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(EncodingKey self) => !self._data.AsSpan().ContainsAnyExcept([(byte)0]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator true(EncodingKey self) => self._data.AsSpan().ContainsAnyExcept([(byte)0]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator false(EncodingKey self) => !self._data.AsSpan().ContainsAnyExcept([(byte)0]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !(EncodingKey self) => !self._data.AsSpan().ContainsAnyExcept([(byte)0]);
    }

    public static class EncodingKeyExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SequenceEqual<T, U>(this T self, in U other)
            where T : struct, IEncodingKey<T>, allows ref struct
            where U : struct, IEncodingKey<U>, allows ref struct
            => self.AsSpan().SequenceEqual(other.AsSpan());
    }

    namespace Views
    {
        /// <summary>
        /// A stack-allocated, non-owning variation of <see cref="EncodingKey" />.
        /// </summary>
        /// <param name="data">The span of data that represents an encoding key.</param>
        [DebuggerTypeProxy(typeof(DebugView))]
        public readonly ref struct EncodingKey(ReadOnlySpan<byte> data) : IEncodingKey<EncodingKey>
        {
            private readonly ReadOnlySpan<byte> _data = data;

            public byte this[int offset] => _data[offset];
            public int Length => _data.Length;

            public unsafe ReadOnlySpan<byte> AsSpan() => _data;
            public string AsHexString() => Convert.ToHexStringLower(_data);
            public TACT.EncodingKey Upgrade() => new(_data);

            static EncodingKey IKey<EncodingKey>.From(ReadOnlySpan<byte> data) => new(data);


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator true(EncodingKey self) => self._data.ContainsAnyExcept([(byte)0]);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator false(EncodingKey self) => !self._data.ContainsAnyExcept([(byte)0]);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !(EncodingKey self) => !self._data.ContainsAnyExcept([(byte)0]);
        }
    }

    file class DebugView(ReadOnlySpan<byte> data)
    {
        private readonly byte[] _dataBuffer = [.. data];

        public override string ToString() => Convert.ToHexStringLower(_dataBuffer);
    }
}
