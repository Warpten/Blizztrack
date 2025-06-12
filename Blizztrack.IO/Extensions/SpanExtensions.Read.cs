using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.IO.Extensions
{
    public static partial class SpanExtensions
    {

        /// <summary>
        /// Reads an integer type in big-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T ReadBE<T>(this ReadOnlySpan<byte> source) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, BitConverter.IsLittleEndian);

        /// <summary>
        /// Reads an integer type in little-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T ReadLE<T>(this ReadOnlySpan<byte> source) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, !BitConverter.IsLittleEndian);

        /// <summary>
        /// Reads an integer type in whatever the system endianness is.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T ReadNative<T>(this ReadOnlySpan<byte> source) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, false);

        /// <summary>
        /// Reads an array of integer type in big-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <param name="count">The amount of values to read.</param>
        /// <returns></returns>
        public static T[] ReadBE<T>(this ReadOnlySpan<byte> source, int count) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, count, BitConverter.IsLittleEndian);

        /// <summary>
        /// Reads an array of integer type in little-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <param name="count">The amount of values to read.</param>
        /// <returns></returns>
        public static T[] ReadLE<T>(this ReadOnlySpan<byte> source, int count) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, count, !BitConverter.IsLittleEndian);

        /// <summary>
        /// Reads an array of integer type in whatever the system endianness is.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <param name="count">The amount of values to read.</param>
        /// <returns></returns>
        public static T[] ReadNative<T>(this ReadOnlySpan<byte> source, int count) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, count, false);

        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        private static T ReadEndianAware<T>(this ReadOnlySpan<byte> source, bool reverse) where T : unmanaged, IBinaryInteger<T>
        {
            Debug.Assert(source.Length >= Unsafe.SizeOf<T>());

            var value = MemoryMarshal.Read<T>(source);

            var valueSpan = MemoryMarshal.CreateSpan(ref value, 1);
            if (reverse)
                valueSpan.ReverseEndianness();

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        private static T[] ReadEndianAware<T>(this ReadOnlySpan<byte> source, int count, bool reverse) where T : unmanaged, IBinaryInteger<T>
        {
            Debug.Assert(source.Length >= Unsafe.SizeOf<T>() * count);

            var value = GC.AllocateUninitializedArray<T>(count);
            MemoryMarshal.Cast<byte, T>(source)[..count].CopyTo(value);

            if (reverse)
                value.AsSpan().ReverseEndianness();

            return value;
        }
    }
}
