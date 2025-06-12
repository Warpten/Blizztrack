using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.IO
{
    public readonly ref struct ReadOnlyStridedSpan<T>(ReadOnlySpan<T> source, int stride)
    {
        // Inlined Span<T>
        private readonly ref T _reference = ref MemoryMarshal.GetReference(source);
        private readonly int _length = source.Length;

        private readonly int _stride = stride;

        public readonly ReadOnlySpan<T> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var absoluteOffset = index * _stride;
                Debug.Assert(absoluteOffset + _stride < _length);

                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AddByteOffset(ref _reference, Unsafe.SizeOf<T>() * absoluteOffset), _stride);
            }
        }

        public readonly int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length / _stride;
        }

        public readonly ReadOnlySpan<T> this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[index.GetOffset(_length / _stride)];
        }
    }
}
