using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.IO.Extensions
{
    public static partial class BinaryIntegerExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReverseEndianness<T>(this ref T value) where T : unmanaged, IBinaryInteger<T>
        {
            var dataSpan = MemoryMarshal.CreateSpan(ref value, 1);
            dataSpan.ReverseEndianness();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReverseEndianness<T>(this T[] value) where T : unmanaged, IBinaryInteger<T>
            => value.AsSpan().ReverseEndianness();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReverseEndianness<T>(this List<T> value) where T : unmanaged, IBinaryInteger<T>
            => CollectionsMarshal.AsSpan(value).ReverseEndianness();
    }
}
