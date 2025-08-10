using Blizztrack.Framework.TACT.Enums;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Framework.TACT.Structures;
using Blizztrack.Shared.Extensions;
using Blizztrack.Shared.IO;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Framework.TACT.Implementation
{
    #pragma warning disable BT003 // Type or member is obsolete

    public class Root : IResourceParser<Root>
    {
        private readonly Page[] _pages;

        public static readonly Locale AllWoW = Locale.enUS
            | Locale.koKR
            | Locale.frFR
            | Locale.deDE
            | Locale.zhCN
            | Locale.esES
            | Locale.zhTW
            | Locale.enGB
            | Locale.esMX
            | Locale.ruRU
            | Locale.ptBR
            | Locale.itIT
            | Locale.ptPT;

        #region IResourceParser
        public static Root OpenResource(ResourceHandle decompressedHandle)
            => Open(decompressedHandle.ToMappedDataSource());

        public static Root OpenCompressedResource(ResourceHandle compressedHandle)
            => Open(BLTE.Parse(compressedHandle).ToDataSource());
        #endregion

        public static Root Open<T>(T fileData) where T : IDataSource
        {
            var magic = fileData.Slice(0, 4).ReadUInt32LE();
            var (format, version, headerSize, totalFileCount, namedFileCount) = magic switch
            {
                0x4D465354 => ParseMFST(fileData),
                _ => (Format.Legacy, 0, 0, 0, 0)
            };

            // Skip the header.
            var parseCursor = fileData[headerSize..];

            var allowUnnamedFiles = format == Format.MFST && totalFileCount != namedFileCount;

            var pages = new List<Page>();
            while (parseCursor.Length != 0)
            {
                var recordCount = parseCursor.Advance(4).ReadInt32LE();
                var pageHeader = ParseManifestPageHeader(ref parseCursor, version);

                // No records in this file.
                if (recordCount == 0)
                    continue;

                // Calculate block size
                // Legacy: u32[recordCount], {MD58, u64}[recordCount]
                // MFST: u32[recordCount], MD5[recordCount], u64?[recordCount]
                var blockSize = (sizeof(uint) + MD5.Length) * recordCount;
                if (format == Format.Legacy || !(allowUnnamedFiles && !pageHeader.HasNames))
                    blockSize += sizeof(long) * recordCount;

                // Regardless of wether or not this page is parsed we need to advance the read cursor.
                var blockData = parseCursor.Advance(blockSize);

                // Read a FDID delta array from the file (+1 implied) and adjust instantly.
                var fdids = blockData.ReadInt32LE(recordCount);
#if DEBUG
                for (var i = 1; i < fdids.Length; ++i)
                    Debug.Assert(fdids[i] >= 0);
#endif

                // Adjust cursor past the FDID array
                blockData = blockData[(4 * recordCount)..];

                // Parse records according to their specification.
                var records = format switch
                {
                    Format.Legacy => ParseLegacy(ref blockData, recordCount, fdids, pageHeader),
                    Format.MFST => ParseManifest(ref blockData, recordCount, allowUnnamedFiles, fdids, pageHeader),
                    _ => throw new UnreachableException()
                };

                var page = new Page(pageHeader, records);
                pages.Add(page);
            }

            return new([.. pages]);
        }

        private Root(Page[] pages) => _pages = pages;

        public int Count => _pages.Sum(page => page.Records.Length);

        private static PageHeader ParseManifestPageHeader(ref ReadOnlySpan<byte> fileData, int version)
        {
            switch (version)
            {
                case 0:
                case 1:
                    {
                        var contentFlags = (Content) fileData.ReadUInt32LE();
                        var localeFlags = (Locale) fileData[4..].ReadUInt32LE();

                        fileData = fileData[8..];

                        return new(contentFlags, localeFlags);
                    }
                case 2:
                    {
                        var localeFlags = (Locale) fileData.ReadUInt32LE();

                        var unk1 = fileData[4..].ReadUInt32LE();
                        var unk2 = fileData[8..].ReadUInt32LE();
                        var unk3 = (uint)fileData[12] << 17;

                        fileData = fileData[13..];

                        var contentFlags = (Content)(unk1 | unk2 | unk3);

                        return new(contentFlags, localeFlags);
                    }
                default:
                    throw new NotImplementedException($"MFST version {version} is not supported");
            }
        }

        /// <summary>
        /// Finds a file given a file data ID.
        /// </summary>
        /// <param name="fileDataID">The file data ID to look for.</param>
        /// <returns>An optional record as well as the associated content and locale flags.</returns>
        public ref readonly RootRecord FindFileDataID(uint fileDataID)
        {
            foreach (ref readonly var page in _pages.AsSpan())
            {
                var fdidIndex = page.Records.BinarySearchBy((ref RootRecord record) => (record.FileDataID - (int)fileDataID).ToOrdering());
                if (fdidIndex == -1)
                    continue;

                ref var record = ref page.Records.UnsafeIndex(fdidIndex);
                if (record.FileDataID == fileDataID)
                    return ref record;
            }

            return ref Unsafe.NullRef<RootRecord>();
        }

        /// <summary>
        /// Finds a record as identified by its name hash (also known as lookup).
        /// </summary>
        /// <param name="nameHash">The hash of the file's complete path in the game's file structure.</param>
        /// <returns>An optional record as well as the associated content and locale flags.</returns>
        public ref readonly RootRecord FindHash(ulong nameHash)
        {
            foreach (ref readonly var page in _pages.AsSpan())
            {
                if (!page.Header.HasNames)
                    continue;

                for (var i = 0; i < page.Records.Length; ++i)
                {
                    ref readonly var record = ref page.Records.UnsafeIndex(i);
                    if (record.NameHash == nameHash)
                        return ref record;
                }
            }

            return ref Unsafe.NullRef<RootRecord>();
        }

        private static RootRecord[] ParseLegacy(scoped ref ReadOnlySpan<byte> dataStream, int recordCount, int[] fdids, PageHeader header)
        {
            var fdidCounter = 0;

            var records = GC.AllocateUninitializedArray<RootRecord>(recordCount);
            for (var i = 0; i < records.Length; ++i)
            {
                var contentKey = new MD5(dataStream.Advance(MD5.Length));
                var nameHash = dataStream.Advance(8).ReadUInt64LE();

                fdidCounter += fdids[i];

                records[i] = new(contentKey, nameHash, fdidCounter + i);
            }

            return records;
        }

        private static RootRecord[] ParseManifest(ref ReadOnlySpan<byte> dataStream, int recordCount,
            bool allowUnnamedFiles, int[] fdids, PageHeader pageHeader)
        {
            var fdidCounter = 0;

            // Use unsafe math to convert a ternary into a branchless version of `b ? 8 : 0`.
            var nameHashSize = (!(allowUnnamedFiles && !pageHeader.HasNames)).UnsafePromote() << 3;
            nameHashSize *= recordCount;

            var ckr = new Range(0, recordCount * MD5.Length); // Content key range
            var nhr = new Range(ckr.End.Value, ckr.End.Value + nameHashSize); // Name hash range

            var sectionContents = dataStream.Advance(nhr.End.Value);
            var contentKeys = MemoryMarshal.Cast<byte, MD5>(sectionContents[ckr]);
            // TODO: This becomes tied to platform endianness, reverse endianness as needed.
            var nameHashes = MemoryMarshal.Cast<byte, ulong>(sectionContents[nhr]);

            var records = GC.AllocateUninitializedArray<RootRecord>(recordCount);
            for (var i = 0; i < recordCount; ++i)
            {
                var nameHash = nameHashes.Length switch
                {
                    8 => nameHashes[i],
                    _ => 0uL
                };

                fdidCounter += fdids[i];

                records[i] = new(contentKeys[i], nameHash, fdidCounter + i);
            }

            return records;
        }

        private static (Format, int Version, int HeaderSize, int TotalFileCount, int NamedFileCount) ParseMFST<T>(T dataStream)
            where T : IDataSource
        {
            // Skip over magic at dataStream[0]
            Debug.Assert(dataStream[..4].ReadUInt32LE() == 0x4D465354);

            var headerSize = dataStream[4..].ReadInt32LE();
            var version = dataStream[8..].ReadInt32LE();
            if (headerSize > 1000)
                return (Format.MFST, 1, 4 * 4, headerSize, version);

            var totalFileCount = dataStream[12..].ReadInt32LE();
            var namedFileCount = dataStream[16..].ReadInt32LE();

            return (Format.MFST, version, headerSize, totalFileCount, namedFileCount);
        }

        private readonly record struct PageHeader(Content ContentFlags, Locale LocaleFlags)
        {
            public readonly bool HasNames = !ContentFlags.HasFlag(Content.NoNames);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool HasFlag(Content contentFlags) => ContentFlags.HasFlag(contentFlags);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool HasFlag(Locale localeFlags) => LocaleFlags.HasFlag(localeFlags);
        }

        private record struct Page(PageHeader Header, RootRecord[] Records);

        private enum Format
        {
            Legacy,
            MFST
        }
    }
}

#pragma warning restore BT003 // Type or member is obsolete
