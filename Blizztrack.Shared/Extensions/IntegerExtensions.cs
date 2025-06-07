using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Blizztrack.Shared.Extensions
{
    public static class IntegerExtensions
    {
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
