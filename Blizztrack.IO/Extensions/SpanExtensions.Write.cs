using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Blizztrack.IO.Extensions
{
    public static partial class SpanExtensions
    {        /// <summary>
             /// Writes an integer type in big-endian from the given span.
             /// </summary>
             /// <typeparam name="T">The type of value to write.</typeparam>
             /// <param name="destination"></param>
             /// <param name="value">The value to write.</param>
        public static void WriteBE<T>(this Span<byte> destination, T value) where T : unmanaged, IBinaryInteger<T>
            => WriteEndianAware(destination, value, BitConverter.IsLittleEndian);

        /// <summary>
        /// Writes an integer type in little-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to write.</typeparam>
        /// <param name="destination"></param>
        /// <param name="value">The value to write.</param>
        public static void WriteLE<T>(this Span<byte> destination, T value) where T : unmanaged, IBinaryInteger<T>
            => WriteEndianAware(destination, value, !BitConverter.IsLittleEndian);

        /// <summary>
        /// Writes an integer type in whatever the system endianness is.
        /// </summary>
        /// <typeparam name="T">The type of value to write.</typeparam>
        /// <param name="destination"></param>
        /// <param name="value">The value to write.</param>
        public static void WriteNative<T>(this Span<byte> destination, T value) where T : unmanaged, IBinaryInteger<T>
            => WriteEndianAware(destination, value, false);

        /// <summary>
        /// Writes an array of integer type in big-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to write.</typeparam>
        /// <param name="source"></param>
        /// <param name="values">The values to write.</param>
        public static void WriteBE<T>(this Span<byte> destination, T[] values) where T : unmanaged, IBinaryInteger<T>
            => WriteEndianAware(destination, values, BitConverter.IsLittleEndian);

        /// <summary>
        /// Writes an array of integer type in little-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to Write.</typeparam>
        /// <param name="source"></param>
        /// <param name="values">The values to write.</param>
        /// <returns></returns>
        public static void WriteLE<T>(this Span<byte> source, T[] values) where T : unmanaged, IBinaryInteger<T>
            => WriteEndianAware(source, values, !BitConverter.IsLittleEndian);

        /// <summary>
        /// Writes an array of integer type in whatever the system endianness is.
        /// </summary>
        /// <typeparam name="T">The type of value to Write.</typeparam>
        /// <param name="destination"></param>
        /// <param name="values">The values to write.</param>
        /// <returns></returns>
        public static void WriteNative<T>(this Span<byte> destination, T[] values) where T : unmanaged, IBinaryInteger<T>
            => WriteEndianAware(destination, values, false);

        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        private static void WriteEndianAware<T>(this Span<byte> destination, T value, bool reverse) where T : unmanaged, IBinaryInteger<T>
        {
            Debug.Assert(destination.Length >= Unsafe.SizeOf<T>());

            MemoryMarshal.Write(destination, value);
            if (reverse)
                destination[Unsafe.SizeOf<T>()..].ReverseEndianness();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        private static void WriteEndianAware<T>(this Span<byte> destination, T[] values, bool reverse) where T : unmanaged, IBinaryInteger<T>
        {
            Debug.Assert(destination.Length >= Unsafe.SizeOf<T>() * values.Length);

            MemoryMarshal.Cast<T, byte>(values.AsSpan()).CopyTo(destination);
            if (reverse)
                destination[(Unsafe.SizeOf<T>() * values.Length)..].ReverseEndianness();
        }

    }
}
