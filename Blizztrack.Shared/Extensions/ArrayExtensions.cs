using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Shared.Extensions
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Returns an element in an array, bypassing bounds check automatically inserted by the JITter.
        /// 
        /// <para>
        /// This is semantically equivalent to <c>arr[index]</c> but prevents the JIT from emitting bounds checks.
        /// </para>
        /// 
        /// <para>
        /// Note that in return no guarantees are made and you should always make sure the <paramref name="index"/> is within bounds
        /// yourself.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr">The array to index.</param>
        /// <param name="index">The index of the element to return.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this T[] arr, int index)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), index);

        /// <summary>
        /// Returns an element in a span, bypassing bounds check.
        /// 
        /// <para>
        /// This is semantically equivalent to <c>span[index]</c> but bypasses bounds checks.
        /// </para>
        /// 
        /// <para>
        /// Note that in return no guarantees are made and you should always make sure the <paramref name="index"/> is within bounds
        /// yourself.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="span">The span to index.</param>
        /// <param name="index">The index of the element to return.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this scoped ref Span<T> span, int index)
            => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);

        /// <summary>
        /// Returns an element in a span, bypassing bounds check.
        /// 
        /// <para>
        /// This is semantically equivalent to <c>span[index]</c> but bypasses bounds checks.
        /// </para>
        /// 
        /// <para>
        /// Note that in return no guarantees are made and you should always make sure the <paramref name="index"/> is within bounds
        /// yourself.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="span">The span to index.</param>
        /// <param name="index">The index of the element to return.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this scoped ref ReadOnlySpan<T> arr, int index)
            => ref Unsafe.Add(ref MemoryMarshal.GetReference(arr), index);
    }
}
