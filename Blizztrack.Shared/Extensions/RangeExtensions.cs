using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Blizztrack.Shared.Extensions
{
    public static class RangeExtensions
    {
        /// <summary>
        /// Shifts this range by a given amount.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="offset"></param>
        /// <returns>A new range.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Range Shift(this Range range, int offset)
            => new(range.Start.Value + offset, range.End.Value + offset);

        public static Range Intersection(this Range range, int targetSize, Range otherRange)
        {
            var (start, length) = range.GetOffsetAndLength(targetSize);
            var end = start + length;

            var (otherStart, otherLength) = otherRange.GetOffsetAndLength(targetSize);
            var otherEnd = otherStart + otherLength;

            start = Math.Max(start, otherStart);
            end = Math.Min(end, otherEnd);
            if (end < start)
                return default;

            return new Range(start, end);
        }

        /// <summary>
        /// Computes the minimal intersecting range of this range and N other ranges over a buffer of <paramref name="targetSize"/> elements.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="targetSize"></param>
        /// <param name="ranges"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Range Intersection(this Range range, int targetSize, params ReadOnlySpan<Range> ranges)
        {
            if (ranges.Length == 0)
                return range;

            // TODO: Vectorize? Not ideal because this is an horizontal operation...
            // Also, what's the point if N is less than a SIMD lane ...

            var (start, length) = range.GetOffsetAndLength(targetSize);
            var end = start + length;

            foreach (var itr in ranges)
            {
                var (itrStart, itrLength) = itr.GetOffsetAndLength(targetSize);
                var itrEnd = itrStart + itrLength;

                start = Math.Max(start, itrStart);
                end = Math.Min(end, itrEnd);

                if (start > end)
                    return default;
            }

            return new Range(start, end);
        }

        /// <summary>
        /// Rebases this range to the start of the given range.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="newBase">The range to treat as a base.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Range Rebase(this Range range, Range newBase)
            => range.Shift(newBase.Start.Value);

        #region Trim
        public static Range Trim<T>(this Range range, ReadOnlySpan<T> reference, Func<T, bool> filter)
            => TrimCore(range, reference, filter, true, true);

        public static Range Trim<T>(this Range range, ReadOnlySpan<T> reference, IEqualityComparer<T> cmp, params ReadOnlySpan<T> items)
            => TrimCore(range, reference, cmp, items, true, true);

        public static Range Trim<T>(this Range range, ReadOnlySpan<T> reference, params ReadOnlySpan<T> items)
            => TrimCore(range, reference, EqualityComparer<T>.Default, items, true, true);
        #endregion

        #region Trim left
        public static Range TrimLeft<T>(this Range range, ReadOnlySpan<T> reference, Func<T, bool> filter)
            => TrimCore(range, reference, filter, true, false);

        public static Range TrimLeft<T>(this Range range, ReadOnlySpan<T> reference, IEqualityComparer<T> cmp, params ReadOnlySpan<T> items)
            => TrimCore(range, reference, cmp, items, true, false);

        public static Range TrimLeft<T>(this Range range, ReadOnlySpan<T> reference, params ReadOnlySpan<T> items)
            => TrimCore(range, reference, EqualityComparer<T>.Default, items, true, false);
        #endregion

        #region Trim right
        public static Range TrimRight<T>(this Range range, ReadOnlySpan<T> reference, Func<T, bool> filter)
            => TrimCore(range, reference, filter, false, true);

        public static Range TrimRight<T>(this Range range, ReadOnlySpan<T> reference, IEqualityComparer<T> cmp, params ReadOnlySpan<T> items)
            => TrimCore(range, reference, cmp, items, false, true);

        public static Range TrimRight<T>(this Range range, ReadOnlySpan<T> reference, params ReadOnlySpan<T> items)
            => TrimCore(range, reference, EqualityComparer<T>.Default, items, false, true);
        #endregion

        private static Range TrimCore<T>(Range range, ReadOnlySpan<T> reference, Func<T, bool> filter, bool left, bool right)
        {
            var startOffset = range.Start.Value;
            var endOffset = range.End.Value - 1;

            while (startOffset <= endOffset)
            {
                var anyIteratorChanged = false;
                if (left && filter(reference[startOffset]))
                {
                    anyIteratorChanged = true;
                    ++startOffset;
                }

                if (right && filter(reference[endOffset]))
                {
                    anyIteratorChanged = true;
                    --endOffset;
                }

                if (!anyIteratorChanged)
                    break;
            }

            return new(startOffset, endOffset + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Range TrimCore<T>(Range range, ReadOnlySpan<T> reference, IEqualityComparer<T> cmp, ReadOnlySpan<T> items, bool left, bool right)
        {
            var startOffset = range.Start.Value;
            var endOffset = range.End.Value - 1;

            while (startOffset <= endOffset)
            {
                if (left)
                {
                    for (var i = 0; i < items.Length && cmp.Equals(reference[startOffset], items[i]); ++i)
                        ++startOffset;
                }

                if (right)
                {
                    for (var i = 0; i < items.Length && cmp.Equals(reference[endOffset], items[i]); ++i)
                        --endOffset;
                }
            }

            return new(startOffset, endOffset + 1);
        }
    }
}
