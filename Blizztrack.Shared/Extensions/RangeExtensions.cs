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
        public static Range Shift(this Range range, int offset)
            => new(range.Start.Value + offset, range.End.Value + offset);

        /// <summary>
        /// Rebases this range to the start of the given range.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="newBase">The range to treat as a base.</param>
        /// <returns></returns>
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
