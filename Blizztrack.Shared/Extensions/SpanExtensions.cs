using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Blizztrack.Shared.Extensions
{
    public static class SpanExtensions
    {
        #region Stride primitives
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StridedReadOnlySpan<T> WithStride<T>(this ReadOnlySpan<T> span, int stride)
            => new(span, stride);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StridedSpan<T> WithStride<T>(this Span<T> span, int stride)
            => new(span, stride);
        #endregion

        #region Read primitives
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16BE(this ReadOnlySpan<byte> source)
        => BinaryPrimitives.ReadUInt16BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16BE(this ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadInt16BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt24BE(this ReadOnlySpan<byte> source)
            => source[2] | source[1] << 8 | source[0] << 16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32BE(this ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadInt32BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32BE(this ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt32BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32LE(this ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt32LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64LE(this ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt64LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32LE(this ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadInt32LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int[] ReadInt32LE(this ReadOnlySpan<byte> source, int count)
        {
            var data = MemoryMarshal.Cast<byte, int>(source[0..(count * 4)]).ToArray();

            if (!BitConverter.IsLittleEndian)
            {
                for (var i = 0; i < data.Length; ++i)
                    data[i] = BinaryPrimitives.ReverseEndianness(data[i]);
            }

            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt40BE(this ReadOnlySpan<byte> source)
            => source[4] | source[3] << 8 | source[2] << 16 | source[1] << 24 | source[0] << 32;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadCString(this ReadOnlySpan<byte> source)
            => Encoding.UTF8.GetString(source[..source.IndexOf((byte)0)]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> ReadUntil(this ReadOnlySpan<byte> source, byte delimiter)
            => source[..source.IndexOf(delimiter)];
        #endregion

        /// <summary>
        /// Yanks <paramref name="count"/> bytes from this span, modifies
        /// this span in-place, and returns the yanked span.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> Advance(this scoped ref ReadOnlySpan<byte> data, int count)
        {
            var section = data[..count];
            data = data[count..];
            return section;
        }

        /// <summary>
        /// Yanks <paramref name="count"/> bytes from this span, modifies
        /// this span in-place, and returns the yanked span.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> Advance(this scoped ref Span<byte> data, int count)
        {
            var section = data[..count];
            data = data[count..];
            return section;
        }

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
                    delimiterIndex = source.Length - i;

                if (!removeEmptyEntries || delimiterIndex != 1)
                    list.Add(new Range(i, i + delimiterIndex));

                i += delimiterIndex + 1;
            }

            return [.. list];
        }
    }
}
