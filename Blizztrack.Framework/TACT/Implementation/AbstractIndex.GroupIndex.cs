using Blizztrack.Shared.Extensions;
using Blizztrack.Framework.TACT.Resources;

using static Blizztrack.Framework.TACT.Implementation.IIndex;
using Blizztrack.Framework.IO;

namespace Blizztrack.Framework.TACT.Implementation
{
    public sealed class GroupIndex : AbstractIndex
    {
        public GroupIndex(ResourceHandle handle) : base(handle)
        {
        }

        internal GroupIndex(ResourceHandle resource, in Footer footer) : base(resource, footer)
        {
        }

        // [ N - Key ] [ 4 - Size ] [ 2 - Index ] [ 4 - Offset ]

        internal override unsafe Enumerator AsEnumerable(MappedMemory memory, int pageSize, int pageCount, int keyBytes, int offsetBytes, int sizeBytes)
            => new(memory, pageSize, pageCount, keyBytes, offsetBytes + 2, sizeBytes, -1, &ParseEntry);

        private static Entry ParseEntry(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes, int archiveIndex)
        {
            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data.Slice(keyBytes, sizeBytes).ReadInt32BE();
            archiveIndex = data.Slice(keyBytes + sizeBytes).ReadInt16BE();
            var offset = data.Slice(keyBytes + sizeBytes + 2).ReadInt32BE();

            return new(key, offset, encodedSize, archiveIndex);
        }

        protected override Entry ParseEntry(ReadOnlySpan<byte> entry)
            => ParseEntry(entry, _footer.KeyBytes, 0, _footer.SizeBytes, -1);
    }
}
