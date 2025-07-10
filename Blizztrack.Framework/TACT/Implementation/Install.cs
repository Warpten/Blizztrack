using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Shared.Extensions;
using Blizztrack.Shared.IO;

using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.TACT.Implementation
{
    public class Install : IResourceParser<Install>
    {
        /// <summary>
        /// An array of all known tags within this file.
        /// </summary>
        public readonly Tag[] Tags;

        /// <summary>
        /// An array of all known entries within this file.
        /// </summary>
        public readonly Entry[] Entries;

        /// <summary>
        /// The version of this manifest file.
        /// </summary>
        public readonly int Version;

        #region IResourceParser
        public static Install OpenResource(ResourceHandle decompressedHandle)
            => Open(decompressedHandle.ToMappedDataSource());

        public static Install OpenCompressedResource(ResourceHandle compressedHandle)
            => Open(BLTE.Parse(compressedHandle).ToDataSource());
        #endregion

        public static Install Open<T>(T fileData) where T : IDataSource
        {
            if (fileData[0] != 0x49 || fileData[1] != 0x4E)
                throw new InvalidOperationException(); // TODO: Throw a better exception

            var version = fileData[2];
            var hashSize = fileData[3];
            var tagCount = fileData[4..].ReadUInt16BE();
            var entryCount = fileData[6..].ReadInt32BE();

            var bytesPerTag = (entryCount + 7) >> 3;

            var tags = GC.AllocateUninitializedArray<Tag>(tagCount);

            var fileIterator = fileData[10..];
            for (var i = 0; i < tagCount; ++i)
            {
                var tagName = fileIterator.ReadUntil(0);
                fileIterator = fileIterator[(tagName.Length + 1)..];

                var tagType = fileIterator.ReadUInt16BE();
                var tagBits = fileIterator.Slice(2, bytesPerTag);

                fileIterator = fileIterator[(2 + bytesPerTag)..];

                tags.UnsafeIndex(i) = new Tag(System.Text.Encoding.ASCII.GetString(tagName), tagType, tagBits);
            }

            var entries = GC.AllocateUninitializedArray<Entry>(entryCount);
            for (var i = 0; i < entryCount; ++i)
            {
                var entryName = fileIterator.ReadUntil(0);
                fileIterator = fileIterator[(entryName.Length + 1)..];

                var contentHash = fileIterator[..hashSize];
                fileIterator = fileIterator[hashSize..];

                var fileSize = fileIterator.ReadUInt32BE();
                fileIterator = fileIterator[4..];

                entries.UnsafeIndex(i) = new Entry(i, System.Text.Encoding.ASCII.GetString(entryName), new ContentKey(contentHash), fileSize);
            }

            return new Install(version, tags, entries);
        }

        private Install(int version, Tag[] tags, Entry[] entries)
        {
            Version = version;
            Tags = tags;
            Entries = entries;
        }

        /// <summary>
        /// Provides an enumeration of all the tags that are applied on a particular entry.
        /// </summary>
        /// <param name="entry">A record for a file.</param>
        /// <returns></returns>
        /// <remarks>Results of this method cannot be trusted if the given <see cref="Entry" /> was not supplied by this <see cref="Install" /> object.</remarks>
        public IEnumerable<Tag> EnumerateTags(Entry entry)
        {
            for (var i = 0; i < Tags.Length; ++i)
            {
                ref var tag = ref Tags.UnsafeIndex(i);
                if (tag.Flags[entry.Index])
                    yield return tag;
            }
        }

        /// <summary>
        /// A lightweight type that describes a record within an install file.
        /// </summary>
        /// <param name="index">The index of the entry within the file.</param>
        /// <param name="name">The name of the file this entry designates.</param>
        /// <param name="contentKey">The content key of the file this entry designates.</param>
        /// <param name="size">The size of the file this entry designates.</param>
        public readonly struct Entry(int index, string name, ContentKey contentKey, uint size)
        {
            internal readonly int Index = index;
            public readonly string Name = name;
            public readonly ContentKey ContentKey = contentKey;
            public readonly uint Size = size;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool Matches(ref Tag tag) => tag.Flags[Index];
        }

        /// <summary>
        /// A tag that is applied to various files.
        /// </summary>
        /// <param name="name">The name of the tag</param>
        /// <param name="type">The type of the tag.</param>
        /// <param name="flags">A bitset that indicates which files have this tag applied to them.</param>
        public readonly struct Tag(string name, ushort type, ReadOnlySpan<byte> flags)
        {
            /// <summary>
            /// The name of this tag.
            /// </summary>
            public readonly string Name = name;

            /// <summary>
            /// The type of this tag.
            /// </summary>
            public readonly ushort Type = type;

            /// <summary>
            /// A bitset that can be used to determine if a <see cref="Entry" /> has the current tag applied to it.
            /// </summary>
            public readonly Flags Flags = new(flags);
        }

        public readonly struct Flags
        {
            private readonly byte[] _flags; // Bits, stored as integers.
            private readonly int Length; // Length, in bits.

            internal Flags(ReadOnlySpan<byte> flags)
            {
                Length = flags.Length * 8;
                _flags = flags.ToArray();
            }

            public readonly bool this[int index]
            {
                get
                {
                    if (index >= Length || index < 0)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    // The bit layout actually is
                    // [   0-th byte   ] [      1-th byte      ]
                    //  7 6 5 4 3 2 1 0   15 14 13 12 11 10 9 8

                    var referenceIndex = index / 8;
                    var referenceMask = _flags.UnsafeIndex(referenceIndex);

                    var targetMask = 1u << (7 - index % 8);
                    return (referenceMask & targetMask) != 0;
                }
            }
        }
    }
}
