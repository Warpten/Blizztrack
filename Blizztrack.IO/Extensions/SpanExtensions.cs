using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Blizztrack.IO.Extensions
{
    public static partial class SpanExtensions
    {
        public static Span<byte> AsBytes<T>(this Span<T> span) where T : unmanaged
            => MemoryMarshal.AsBytes(span);

        public static Range[] Split<T>(this ReadOnlySpan<T> source, T delimiter, bool removeEmptyEntries = true)
            where T : IEquatable<T>
        {
            if (source.Length == 0)
                return [];

            var list = new List<Range>();
            for (var i = 0; i < source.Length;)
            {
                var delimiterIndex = source[i..].IndexOf(delimiter);
                if (delimiterIndex == -1)
                    delimiterIndex = source.Length;

                if (!removeEmptyEntries || delimiterIndex != i + 1)
                    list.Add(new Range(i, delimiterIndex));

                i = delimiterIndex + 1;
            }

            return [.. list];
        }
    }
}
