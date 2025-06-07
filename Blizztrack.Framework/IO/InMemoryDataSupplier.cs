namespace Blizztrack.Framework.IO
{
    public struct InMemoryDataSupplier(byte[] data) : IBinaryDataSupplier
    {
        public ReadOnlySpan<byte> this[Range range] => data[range];

        public byte this[int offset] => data[offset];

        public byte this[Index index] => data[index];

        public int Length => data.Length;
    }
}

