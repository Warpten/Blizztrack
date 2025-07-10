using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Blizztrack.Shared.Extensions
{
    public static class IntegerExtensions
    {

        /// <summary>
        /// Unsafely promotes the boolean to an integer, bypassing normally emitted branching code.
        /// 
        /// <para>
        /// The result of this method can't be specified when truthy booleans are different than 1.
        /// </para>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UnsafePromote(this bool value)
        {
            // Masking to a single bit is necessary everywhere to account for non-normalized inputs
            // (Unfortunately wastes cycles)

#pragma warning disable CS0162 // Unreachable code detected
            if (sizeof(bool) == sizeof(byte))
            {
                return Unsafe.As<bool, byte>(ref value) & 0b1;
            }
            else if (sizeof(bool) == sizeof(short))
            {
                return Unsafe.As<bool, short>(ref value) & 0b1;
            }
            else if (sizeof(bool) == sizeof(int))
            {
                return Unsafe.As<bool, int>(ref value) & 0b1; ;
            }
            else
            {
                return value ? 1 : 0;
            }
#pragma warning restore CS0162 // Unreachable code detected
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long NativeToLittleEndian(this long value)
            => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NativeToLittleEndian(this int value)
            => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short NativeToLittleEndian(this short value)
            => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NativeToLittleEndian(this ulong value)
            => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint NativeToLittleEndian(this uint value)
            => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort NativeToLittleEndian(this ushort value)
            => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long NativeToBigEndian(this long value)
            => !BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NativeToBigEndian(this int value)
            => !BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short NativeToBigEndian(this short value)
            => !BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NativeToBigEndian(this ulong value)
            => !BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint NativeToBigEndian(this uint value)
            => !BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort NativeToBigEndian(this ushort value)
            => !BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    }
}
