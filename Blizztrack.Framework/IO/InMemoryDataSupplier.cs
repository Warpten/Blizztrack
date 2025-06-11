using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.IO
{
    public readonly struct InMemoryDataSupplier(byte[] data) : IBinaryDataSupplier
    {
        public ReadOnlySpan<byte> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data[range];
        }

        public readonly byte this[int offset]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data[offset];
        }

        public readonly byte this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data[index];
        }

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data.Length;
        }
    }

    public static partial class BinaryDataSupplierExtensions
    {
        public static InMemoryDataSupplier ToDataSupplier(this byte[] data) => new(data);
    }
}

