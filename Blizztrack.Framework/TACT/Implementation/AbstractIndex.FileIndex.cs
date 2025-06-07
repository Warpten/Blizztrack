using Blizztrack.Shared.Extensions;
using Blizztrack.Framework.TACT.Resources;

using System.Diagnostics;

using static Blizztrack.Framework.TACT.Implementation.IIndex;

namespace Blizztrack.Framework.TACT.Implementation
{
    public sealed class FileIndex : AbstractIndex
    {
        public FileIndex(ResourceHandle resourceHandle) : base(resourceHandle)
        {
        }

        internal FileIndex(ResourceHandle resource, in Footer footer) : base(resource, footer)
        {
        }

        // [ N - Key ] [ 4 - Size ]

        internal override unsafe Enumerator AsEnumerable(MappedMemory memory, int pageSize, int pageCount, int keyBytes, int offsetBytes, int sizeBytes)
            => new(memory, pageSize, pageCount, keyBytes, 0, sizeBytes, -1, &ParseEntry);

        private static Entry ParseEntry(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes, int archiveIndex)
        {
            Debug.Assert(sizeBytes >= 4);

            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data[keyBytes..].ReadInt32BE();

            return new(key, -1, encodedSize, -1);
        }

        protected override Entry ParseEntry(ReadOnlySpan<byte> entry)
            => ParseEntry(entry, _footer.KeyBytes, 0, _footer.SizeBytes, -1);
    }
}
