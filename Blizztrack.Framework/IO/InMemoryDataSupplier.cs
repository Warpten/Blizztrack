using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.IO
{
    public readonly struct InMemoryDataSupplier(byte[] data) : IBinaryDataSupplier<InMemoryDataSupplier>
    {
        private readonly byte[] _data = data;

        public ReadOnlySpan<byte> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data[range];
        }

        public readonly byte this[int offset]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data[offset];
        }

        public readonly byte this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data[index];
        }

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data.Length;
        }

        public static implicit operator ReadOnlySpan<byte>(InMemoryDataSupplier value)
            => value._data;
    }

    public static partial class BinaryDataSupplierExtensions
    {
        public static InMemoryDataSupplier ToDataSupplier(this byte[] data) => new(data);
    }
}

