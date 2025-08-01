﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Shared.Extensions
{
    public static class BinarySearchExtensions
    {
        public enum Ordering
        {
            Less,
            Equal,
            Greater,
        }

        public static Ordering ToOrdering(this int comparison)
            => comparison switch
            {
                > 0 => Ordering.Greater,
                < 0 => Ordering.Less,
                0 => Ordering.Equal,
            };

        public delegate Ordering BinarySearchComparer<T>(scoped ref T left, scoped ref T right) where T : allows ref struct;
        public delegate Ordering BinarySearchSpanComparer<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right);

        /// <inheritdoc cref="BinarySearchBy{T}(ReadOnlySpan{T}, BinarySearchComparer{T}, ref T)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearchBy<T>(this StridedReadOnlySpan<T> haystack, BinarySearchSpanComparer<T> cmp, ReadOnlySpan<T> needle)
        {
            var size = haystack.Count;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                var ordering = cmp(haystack[mid], needle);

                switch (ordering)
                {
                    case Ordering.Less:
                        left = mid + 1;
                        break;
                    case Ordering.Greater:
                        right = mid;
                        break;
                    case Ordering.Equal:
                        Debug.Assert(mid < haystack.Count);
                        return mid;
                }

                size = right - left;
            }

            Debug.Assert(left <= haystack.Count);
            return -(left + 1);
        }

        /// <summary>
        /// Performs a binary search on a <paramref name="haystack"/>, returning the index of the <paramref name="needle"/> if possible.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="haystack">A buffer of sorted data to sift through.</param>
        /// <param name="cmp">A predicate of the form <c>cmp(<paramref name="haystack"/>[i], <paramref name="needle"/>)</c>.</param>
        /// <param name="needle">The <c>needle</c> to search for.</param>
        /// <returns>The index of the item that was found, or the insertion point that maintains ordering negated.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearchBy<T>(this ReadOnlySpan<T> haystack, BinarySearchComparer<T> cmp, scoped ref T needle)
        {
            var size = haystack.Length;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                // If you need to ask, ReadOnlySpan.Item(int) doesn't allow the return value to be taken by ref.
                var ordering = cmp(ref Unsafe.Add(ref MemoryMarshal.GetReference(haystack), mid), ref needle);

                switch (ordering)
                {
                    case Ordering.Less:
                        left = mid + 1;
                        break;
                    case Ordering.Greater:
                        right = mid;
                        break;
                    case Ordering.Equal:
                        Debug.Assert(mid < haystack.Length);
                        return mid;
                }

                size = right - left;
            }

            Debug.Assert(left <= haystack.Length);
            return -(left + 1);
        }

        // ^^^ Binary search / Lower bound vvv

        /// <inheritdoc cref="LowerBoundBy{T}(ReadOnlySpan{T}, BinarySearchComparer{T}, ref T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LowerBoundBy<T>(this StridedReadOnlySpan<T> haystack, BinarySearchSpanComparer<T> cmp, ReadOnlySpan<T> needle)
        {
            var size = haystack.Count;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                var ordering = cmp(haystack[mid], needle);

                switch (ordering)
                {
                    case Ordering.Less:
                        left = mid + 1;
                        break;
                    case Ordering.Greater:
                    case Ordering.Equal:
                        right = mid;
                        break;
                }

                size = right - left;
            }

            return left;
        }

        /// <summary>
        /// Searches for the first element in <paramref name="haystack"/> that is <b>not</b> ordered before <paramref name="needle"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="haystack">A range of elements to examine.</param>
        /// <param name="cmp">A binary predicate that returns true if the first argument is ordered before the second.</param>
        /// <param name="needle">The value to compare the elements to.</param>
        /// <returns>The index of the first element in range that is not ordered before the needle, or an index past the end.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LowerBoundBy<T>(this ReadOnlySpan<T> haystack, BinarySearchComparer<T> cmp, scoped ref T needle)
        {
            var size = haystack.Length;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                // If you need to ask, ReadOnlySpan.Item(int) doesn't allow the return value to be taken by ref.
                var ordering = cmp(ref Unsafe.Add(ref MemoryMarshal.GetReference(haystack), mid), ref needle);

                switch (ordering)
                {
                    case Ordering.Less:
                        left = mid + 1;
                        break;
                    case Ordering.Greater:
                    case Ordering.Equal:
                        right = mid;
                        break;
                }

                size = right - left;
            }

            return left;
        }

        // ^^^ Lower bound / Upper bound vvv

        /// <inheritdoc cref="UpperBoundBy{T}(ReadOnlySpan{T}, BinarySearchComparer{T}, ref T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UpperBoundBy<T>(this StridedReadOnlySpan<T> haystack, BinarySearchSpanComparer<T> cmp, ReadOnlySpan<T> needle)
        {
            var size = haystack.Count;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                var ordering = cmp(haystack[mid], needle);

                switch (ordering)
                {
                    case Ordering.Greater:
                        left = mid + 1;
                        break;
                    case Ordering.Less:
                    case Ordering.Equal:
                        right = mid;
                        break;
                }

                size = right - left;
            }

            return left;
        }

        /// <summary>
        /// Searches for the first element in <paramref name="haystack"/> that is ordered after <paramref name="needle"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="haystack">A range of elements to examine.</param>
        /// <param name="cmp">A binary predicate that returns if the first argument is ordered before the second.</param>
        /// <param name="needle">The value to compare the elements to.</param>
        /// <returns>The index of the first element in range that is not ordered before the needle, or an index past the end.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UpperBoundBy<T>(this ReadOnlySpan<T> haystack, BinarySearchComparer<T> cmp, scoped ref T needle)
        {
            var size = haystack.Length;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                // If you need to ask, ReadOnlySpan.Item(int) doesn't allow the return value to be taken by ref.
                var ordering = cmp(ref Unsafe.Add(ref MemoryMarshal.GetReference(haystack), mid), ref needle);

                switch (ordering)
                {
                    case Ordering.Greater:
                        left = mid + 1;
                        break;
                    case Ordering.Less:
                    case Ordering.Equal:
                        right = mid;
                        break;
                }

                size = right - left;
            }

            return left;
        }

        // ^^^ Upper bound / Array shorthands vvv

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UpperBoundBy<T>(this T[] haystack, BinarySearchComparer<T> cmp, scoped ref T needle)
            => UpperBoundBy(haystack.AsSpan(), cmp, ref needle);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LowerBoundBy<T>(this T[] haystack, BinarySearchComparer<T> cmp, scoped ref T needle)
            => LowerBoundBy(haystack.AsSpan(), cmp, ref needle);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearchBy<T>(this T[] haystack, BinarySearchComparer<T> cmp, scoped ref T needle)
            => BinarySearchBy(haystack.AsSpan(), cmp, ref needle);

        public delegate Ordering BinarySearchPredicate<T>(ref T entry);

        /// <summary>
        /// Performs a binary search with the given predicate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U">An argument to carry around to the predicate.</typeparam>
        /// <param name="cmp">A predicate to use to determine ordering.</param>
        /// <returns>The index of a corresponding entry or -1 if none was found.</returns>
        public static int BinarySearchBy<T>(this T[] array, BinarySearchPredicate<T> cmp)
        {
            var size = array.Length;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                var ordering = cmp(ref array.UnsafeIndex(mid));

                left = ordering switch
                {
                    Ordering.Less => mid + 1,
                    _ => left
                };

                right = ordering switch
                {
                    Ordering.Greater => mid,
                    _ => right
                };

                if (ordering == Ordering.Equal)
                {
                    Debug.Assert(mid < array.Length);
                    return mid;
                }

                size = right - left;
            }

            Debug.Assert(left <= array.Length);
            return left;
        }
    }
}
