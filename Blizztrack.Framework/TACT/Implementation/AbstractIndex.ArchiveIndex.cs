using Blizztrack.Shared.Extensions;
using Blizztrack.Framework.TACT.Resources;

using System.Diagnostics;

using static Blizztrack.Framework.TACT.Implementation.IIndex;
using Blizztrack.Framework.IO;

namespace Blizztrack.Framework.TACT.Implementation
{
    public sealed class ArchiveIndex : AbstractIndex
    {
        public readonly short Index;

        public ArchiveIndex(ResourceHandle resourceHandle, short archiveIndex) : base(resourceHandle)
            => Index = archiveIndex;

        internal ArchiveIndex(ResourceHandle resource, in Footer footer, short archiveIndex) : base(resource, footer)
            => Index = archiveIndex;

        // [ N - Key ] [ 4 - Size ] [ 4 - Offset ]

        internal override unsafe Enumerator AsEnumerable(MappedMemory memory, int pageSize, int pageCount, int keyBytes, int offsetBytes, int sizeBytes)
            => new(memory, pageSize, pageCount, keyBytes, offsetBytes, sizeBytes, Index, &ParseEntry);

        private static Entry ParseEntry(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes, int archiveIndex)
        {
            Debug.Assert(sizeBytes >= 4);
            Debug.Assert(offsetBytes >= 4);

            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data.Slice(keyBytes, sizeBytes).ReadInt32BE();
            var offset = data.Slice(keyBytes + sizeBytes, offsetBytes).ReadInt32BE();

            return new(key, offset, encodedSize, archiveIndex);
        }

        protected override Entry ParseEntry(ReadOnlySpan<byte> entry)
            => ParseEntry(entry, _footer.KeyBytes, _footer.OffsetBytes, _footer.SizeBytes, Index);
    }
}
