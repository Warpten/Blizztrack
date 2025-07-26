using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Shared.Extensions
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Returns an element in an array, bypassing bounds check.
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

        /// <inheritdoc cref="UnsafeIndex{T}(T[], int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this T[] arr, Index index)
            => ref UnsafeIndex(arr, index.GetOffset(arr.Length));

        /// <summary>
        /// Returns a range of elements in an array, bypassing bounds check.
        /// 
        /// <para>
        /// This is semantically equivalent to <c>arr[range]</c> but prevents the JIT from emitting bounds checks.
        /// </para>
        /// 
        /// <para>
        /// Note that in return no guarantees are made and you should always make sure the <paramref name="range"/> is within bounds
        /// yourself.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr">The span to index.</param>
        /// <param name="range">The index of the element to return.</param>
        /// <returns></returns>
        public static Span<T> UnsafeIndex<T>(this T[] arr, Range range)
        {
            var (index, count) = range.GetOffsetAndLength(arr.Length);
            return MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), index), count);
        }

        /// <summary>
        /// Returns an element in a span, bypassing bounds check.
        /// 
        /// <para>
        /// This is semantically equivalent to <c>span[index]</c> but prevents the JIT from emitting bounds checks.
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

        /// <inheritdoc cref="UnsafeIndex{T}(ref Span{T}, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this scoped ref Span<T> span, Index index)
            => ref UnsafeIndex(ref span, index.GetOffset(span.Length));

        /// <inheritdoc cref="UnsafeIndex{T}(ref Span{T}, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this scoped ref ReadOnlySpan<T> span, int index)
            => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);

        /// <inheritdoc cref="UnsafeIndex{T}(ref Span{T}, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this scoped ref ReadOnlySpan<T> span, Index index)
            => ref UnsafeIndex(ref span, index.GetOffset(span.Length));

        /// <summary>
        /// Returns a range of elements in a span, bypassing bounds check.
        /// 
        /// <para>
        /// This is semantically equivalent to <c>span[range]</c> but prevents the JIT from emitting bounds checks.
        /// </para>
        /// 
        /// <para>
        /// Note that in return no guarantees are made and you should always make sure the <paramref name="range"/> is within bounds
        /// yourself.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="span">The span to index.</param>
        /// <param name="range">The index of the element to return.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> UnsafeIndex<T>(this scoped ref Span<T> span, Range range)
        {
            var (index, count) = range.GetOffsetAndLength(span.Length);
            return MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index), count);
        }

        /// <inheritdoc cref="UnsafeIndex{T}(ref Span{T}, Range)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<T> UnsafeIndex<T>(this scoped ref ReadOnlySpan<T> span, Range range)
        {
            var (index, count) = range.GetOffsetAndLength(span.Length);
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index), count);
        }
    }
}
