using System.Runtime.CompilerServices;

namespace Blizztrack.Shared
{
    public readonly ref struct StridedSpan<T>(Span<T> data, int stride)
    {
        private readonly Span<T> _data = data;
        private readonly int _stride = stride;

        public readonly int Count = data.Length / stride;

        public readonly Span<T> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data.Slice(index * _stride, _stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SequenceEqual(StridedSpan<T> other) => _stride == other._stride && _data.SequenceEqual(other._data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Enumerator GetEnumerator() => new(this);

        public ref struct Enumerator
        {
            private readonly StridedSpan<T> _data;
            private int _index = 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(StridedSpan<T> data) => _data = data;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++_index < _data.Count;

            public readonly Span<T> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _data[_index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator StridedReadOnlySpan<T>(StridedSpan<T> self)
            => new(self._data, self._stride);
    }
}
