using Blizztrack.Framework.TACT.Resources;

using static Blizztrack.Framework.TACT.Implementation.AbstractIndex;

namespace Blizztrack.Framework.TACT.Implementation
{
    public interface IIndex
    {
        public readonly ref struct Entry
        {
            internal Entry(EncodingKeyRef key, int offset, long length, int archiveIndex)
            {
                EncodingKey = key;
                Offset = offset;
                Length = length;
                ArchiveIndex = archiveIndex;
            }

            /// <summary>
            /// The content key for this record.
            /// </summary>
            public readonly EncodingKeyRef EncodingKey;

            /// <summary>
            /// The offset, in bytes, of the resource within its archive.
            /// </summary>
            public readonly int Offset;

            /// <summary>
            /// The length, in bytes, of the resource within its archive.
            /// </summary>
            public readonly long Length;

            /// <summary>
            /// The index of the archive within the server configuration.
            /// </summary>
            public readonly int ArchiveIndex;

            public static implicit operator bool(Entry entry) => entry.EncodingKey != default!;
        }

        /// <summary>
        /// Resolves an encoding key within this index.
        /// </summary>
        /// <typeparam name="T">The concrete type of the encoding key.</typeparam>
        /// <param name="encodingKey">The encoding key.</param>
        /// <returns>An index entry, or <see langword="default" /> if no entry matching the given key could be found.</returns>
        public Entry FindEncodingKey<T>(T encodingKey) where T : IEncodingKey<T>, IKey, allows ref struct;

        /// <summary>
        /// Returns an implementation of <see cref="IIndex" /> that best matches the layout of the given file.
        /// </summary>
        /// <param name="resourceHandle">A handle to the resource.</param>
        /// <param name="archiveIndex">An (optional) archive index.</param>
        /// <returns></returns>
        public static IIndex Create(ResourceHandle resourceHandle, short archiveIndex = -1)
        {
            resourceHandle.Read(^20, out Footer footer);

            return footer.OffsetBytes switch
            {
                6 => new GroupIndex(resourceHandle, footer),
                5 => new FileIndex(resourceHandle, footer),
                _ => new ArchiveIndex(resourceHandle, footer, archiveIndex)
            };
        }
    }
}
