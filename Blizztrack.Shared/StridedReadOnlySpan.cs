using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Shared
{
    public readonly ref struct StridedReadOnlySpan<T>(ReadOnlySpan<T> data, int stride)
    {
        public readonly ReadOnlySpan<T> Data = data;
        private readonly int _stride = stride;

        public readonly int Count = data.Length / stride;
        public readonly ReadOnlySpan<T> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Data.Slice(index * _stride, _stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SequenceEqual(StridedReadOnlySpan<T> other) => _stride == other._stride && Data.SequenceEqual(other.Data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Enumerator GetEnumerator() => new(this);

        public ref struct Enumerator
        {
            private readonly StridedReadOnlySpan<T> _data;
            private int _index = 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(StridedReadOnlySpan<T> data) => _data = data;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++_index < _data.Count;

            public readonly ReadOnlySpan<T> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _data[_index];
            }
        }
    }
}
