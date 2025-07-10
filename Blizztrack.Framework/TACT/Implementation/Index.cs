using Blizztrack.Shared;
using Blizztrack.Shared.Extensions;
using Blizztrack.Shared.IO;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static Blizztrack.Framework.TACT.Implementation.IIndex;
using static Blizztrack.Framework.TACT.Implementation.Index;

namespace Blizztrack.Framework.TACT.Implementation
{
    public class Index : IIndex
    {
        private IDataSource _dataSource;
        private readonly unsafe delegate*<ReadOnlySpan<byte>, int, int, int, InternalEntry> _recordParser;
        private readonly (int K, int O, int S) _entrySchema;
        private readonly int _pageSize;
        private readonly int _pageCount;
        private readonly int _entrySize;
        private readonly int _entriesPerPage;
        private readonly int _entriesLastPage;

        public static unsafe Index Open<T>(T dataSource, EncodingKey key) where T : IDataSource
        {
            var footer = MemoryMarshal.Read<Footer>(dataSource[^Unsafe.SizeOf<Footer>()..]);
            delegate*<ReadOnlySpan<byte>, int, int, int, InternalEntry> recordParser = footer.OffsetBytes switch
            {
                6 => &Shared.ParseGroupIndex,
                5 => &Shared.ParseFileIndex,
                _ => &Shared.ParseArchiveIndex
            };

            return new Index(dataSource, key, footer, recordParser);
        }

        private unsafe Index(IDataSource dataSource, EncodingKey key, Footer footer,
            delegate*<ReadOnlySpan<byte>, int, int, int, InternalEntry> recordParser)
        {
            _dataSource = dataSource;
            _entrySchema = (footer.KeyBytes, footer.OffsetBytes, footer.SizeBytes);
            _pageSize = footer.PageSize << 10;
            _entrySize = footer.KeyBytes + footer.SizeBytes + footer.OffsetBytes;
            _entriesPerPage = _pageSize / _entrySize;
            _entriesLastPage = footer.NumElements - (_pageCount - 1) * _entriesPerPage;
            _pageCount = (int) Math.Ceiling((double) footer.NumElements / _entriesPerPage);
            _recordParser = recordParser;

            Key = key;
            Count = footer.NumElements;
        }

        public EncodingKey Key { get; init; }
        public int Count { get; init; }

        public unsafe Enumerator Entries
            => new (_dataSource, [Key], _pageSize, _pageCount, _entrySchema, _recordParser);

        public unsafe Entry FindEncodingKey<T>(T encodingKey)
            where T : notnull, IEncodingKey<T>, allows ref struct
        {
            StridedReadOnlySpan<byte> toc = _dataSource.Slice(_pageCount * _pageSize, _entrySchema.K * _pageCount)
                .WithStride(_entrySchema.K);

            var blockIndex = toc.LowerBoundBy((lhs, rhs) => lhs.SequenceCompareTo(rhs).ToOrdering(), encodingKey.AsSpan());
            if (blockIndex >= toc.Count)
                return default;

            StridedReadOnlySpan<byte> blockData = _dataSource
                .Slice(blockIndex * _pageSize, (blockIndex != _pageCount - 1 ? _entriesPerPage : _entriesLastPage) * _entrySize)
                .WithStride(_entrySize);

            var candidateIndex = blockData.LowerBoundBy((lhs, rhs) => lhs[..rhs.Length].SequenceCompareTo(rhs).ToOrdering(), encodingKey.AsSpan());
            if (candidateIndex >= blockData.Count)
                return default;

            var entry = blockData[candidateIndex];
            if (!entry[.._entrySchema.K].SequenceEqual(encodingKey.AsSpan()))
                return default;

            var rawEntry = _recordParser(entry, _entrySchema.K, _entrySchema.O, _entrySchema.S);
            return new Entry(rawEntry.EncodingKey, rawEntry.Offset, rawEntry.Length, Key);
        }

        internal readonly ref struct InternalEntry(EncodingKeyRef key, int offset, long length, int archiveIndex)
        {
            public readonly EncodingKeyRef EncodingKey = key;
            public readonly int Offset = offset;
            public readonly long Length = length;
            public readonly int ArchiveIndex = archiveIndex;

            public static implicit operator bool(InternalEntry entry) => entry.EncodingKey != default!;
        }
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct Footer
    {
        // public fixed byte TocHash[8]; // See Checksum comment
        // public byte Revision;
        // public fixed byte Flags[2];
        public byte PageSize;
        public byte OffsetBytes;
        public byte SizeBytes;
        public byte KeyBytes;
        public byte HashBytes;
        public int NumElements;

        // Note: proper implementations should keep trying until the actual checksum size is found, as it's not constant.
        // The client tries to read this with 16 bytes first and backs down until Checksum matches the MD5 for the footer
        // with the checksum zeroed. If the hash bytes are less than the typical size of a MD5 digest, the bottom bytes
        // should be checked.
        // I am not spec accurate. I am speed.
        public fixed byte Checksum[8];
    }

    file static class Shared
    {
        public static InternalEntry ParseGroupIndex(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes)
        {
            // [ N - Key ] [ 4 - Size ] [ 2 - Index ] [ 4 - Offset ]
            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data.Slice(keyBytes, sizeBytes).ReadInt32BE();
            var archiveIndex = data.Slice(keyBytes + sizeBytes).ReadInt16BE();
            var offset = data.Slice(keyBytes + sizeBytes + 2).ReadInt32BE();

            return new(key, offset, encodedSize, archiveIndex);
        }

        public static InternalEntry ParseFileIndex(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes)
        {
            // [ N - Key ] [ 4 - Size ]
            Debug.Assert(sizeBytes >= 4);

            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data[keyBytes..].ReadInt32BE();

            return new(key, -1, encodedSize, 0);
        }

        public static InternalEntry ParseArchiveIndex(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes)
        {
            // [ N - Key ] [ 4 - Size ] [ 4 - Offset ]
            Debug.Assert(sizeBytes >= 4);
            Debug.Assert(offsetBytes >= 4);

            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data.Slice(keyBytes, sizeBytes).ReadInt32BE();
            var offset = data.Slice(keyBytes + sizeBytes, offsetBytes).ReadInt32BE();

            return new(key, offset, encodedSize, 0);
        }

    }
}
