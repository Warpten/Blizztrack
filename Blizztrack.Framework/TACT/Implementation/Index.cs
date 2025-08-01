using Blizztrack.Shared.Extensions;
using Blizztrack.Shared.IO;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Blizztrack.Framework.TACT.Implementation
{
    public static class Index
    {
        public static unsafe Index<T> Open<T>(T dataSource, params EncodingKey[] keys) where T : IDataSource
        {
            var footer = MemoryMarshal.Read<Footer>(dataSource[^Unsafe.SizeOf<Footer>()..]);
            delegate*<ReadOnlySpan<byte>, int, int, int, ReadOnlySpan<EncodingKey>, IIndex.Entry> recordParser = footer.OffsetBytes switch
            {
                6 => &Shared.ParseGroupIndex,
                5 => &Shared.ParseFileIndex,
                _ => &Shared.ParseArchiveIndex
            };

            return new Index<T>(dataSource, footer, recordParser, keys);
        }
    }

    public sealed class Index<T> : IIndex<Index<T>.Enumerator>
        where T : IDataSource
    {
        private readonly T _dataSource;
        private readonly EncodingKey[] _keys;

        private readonly unsafe delegate*<ReadOnlySpan<byte>, int, int, int, ReadOnlySpan<EncodingKey>, IIndex.Entry> _recordParser;

        private readonly (int K, int O, int S) _entrySchema;
        private readonly int _pageSize;
        private readonly int _pageCount;
        private readonly int _entrySize;
        private readonly int _entriesPerPage;
        private readonly int _entriesLastPage;

        internal unsafe Index(T dataSource, Footer footer,
            delegate*<ReadOnlySpan<byte>, int, int, int, ReadOnlySpan<EncodingKey>, IIndex.Entry> recordParser,
            params EncodingKey[] archiveKeys)
        {
            _dataSource = dataSource;
            _recordParser = recordParser;

            _entrySchema = (footer.KeyBytes, footer.OffsetBytes, footer.SizeBytes);
            _pageSize = footer.PageSize << 10;
            _entrySize = footer.KeyBytes + footer.SizeBytes + footer.OffsetBytes;
            _entriesPerPage = _pageSize / _entrySize;
            _entriesLastPage = footer.NumElements - (_pageCount - 1) * _entriesPerPage;
            _pageCount = (int) Math.Ceiling((double)footer.NumElements / _entriesPerPage);

            _keys = archiveKeys;
            Count = footer.NumElements;
        }

        public unsafe Enumerator Entries => new(_dataSource, _pageSize, _pageCount, _entrySchema, _recordParser, _keys);

        public int Count { get; init; }

        public unsafe IIndex.Entry this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var indexInPage = index % _entriesPerPage;
                var pageIndex = index / _entriesPerPage;

                var entryOffset = pageIndex * _pageSize + indexInPage * _entrySize;
                return _recordParser(_dataSource.Slice(entryOffset, _entrySize), _entrySchema.K, _entrySchema.O, _entrySchema.S, _keys.AsSpan());
            }
        }

        public unsafe IIndex.Entry this[System.Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[index.GetOffset(Count)];
        }

        public unsafe IIndex.Entry FindEncodingKey<K>(in K encodingKey) where K : notnull, IEncodingKey<K>, allows ref struct
        {
            var toc = _dataSource.Slice(_pageCount * _pageSize, _entrySchema.K * _pageCount)
                .WithStride(_entrySchema.K);

            var blockIndex = toc.LowerBoundBy((lhs, rhs) => lhs.SequenceCompareTo(rhs).ToOrdering(), encodingKey.AsSpan());
            if (blockIndex >= toc.Count)
                return default;

            var blockData = _dataSource
                .Slice(blockIndex * _pageSize, (blockIndex != _pageCount - 1 ? _entriesPerPage : _entriesLastPage) * _entrySize)
                .WithStride(_entrySize);

            var candidateIndex = blockData.LowerBoundBy((lhs, rhs) => lhs[..rhs.Length].SequenceCompareTo(rhs).ToOrdering(), encodingKey.AsSpan());
            if (candidateIndex >= blockData.Count)
                return default;

            var entry = blockData[candidateIndex];
            if (!entry[.._entrySchema.K].SequenceEqual(encodingKey.AsSpan()))
                return default;

            return _recordParser(entry, _entrySchema.K, _entrySchema.O, _entrySchema.S, _keys.AsSpan());
        }

        public unsafe ref struct Enumerator : IIndexEnumerator<Enumerator>
        {
            private readonly ReadOnlySpan<EncodingKey> _archiveKeys;
            private readonly IDataSource _dataSource;
            private readonly delegate*<ReadOnlySpan<byte>, int, int, int, ReadOnlySpan<EncodingKey>, IIndex.Entry> _parser;
            private readonly int _pageCount;
            private readonly int _pageSize; // Size of a single page
            private readonly (int K, int O, int S) _schema; // (key, offset, size) bytes

            private int _pageIndex = 0; // Index of the current page
            private int _entryOffset = 0; // Offset within the current page

            internal Enumerator(IDataSource dataSource,
                int pageSize, int pageCount,
                (int, int, int) schema,
                delegate*<ReadOnlySpan<byte>, int, int, int, ReadOnlySpan<EncodingKey>, IIndex.Entry> parser,
                ReadOnlySpan<EncodingKey> archiveKeys)
            {
                _archiveKeys = archiveKeys;
                _dataSource = dataSource;
                _parser = parser;

                _pageSize = pageSize;
                _pageCount = pageCount;

                _schema = schema;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly Enumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                if (_pageIndex >= _pageCount)
                    return false;

                var entrySize = _schema.K + _schema.O + _schema.S;
                _entryOffset += entrySize;
                if (_entryOffset >= _pageSize)
                {
                    ++_pageIndex;
                    _entryOffset = 0;
                }

                return _pageIndex < _pageCount;
            }

            public unsafe IIndex.Entry Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    var entrySize = _schema.K + _schema.O + _schema.S;
                    var remainingPageData = _dataSource.Slice(_pageSize * _pageIndex + _entryOffset, entrySize);
                    return _parser(remainingPageData, _schema.K, _schema.O, _schema.S, _archiveKeys);
                }
            }
        }
    }

    file static class Shared
    {
        public unsafe static IIndex.Entry ParseGroupIndex(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes, ReadOnlySpan<EncodingKey> archiveKeys)
        {
            // [ N - Key ] [ 4 - Size ] [ 2 - Index ] [ 4 - Offset ]
            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data.Slice(keyBytes, sizeBytes).ReadInt32BE();
            var archiveIndex = data.Slice(keyBytes + sizeBytes).ReadInt16BE();
            var offset = data.Slice(keyBytes + sizeBytes + 2).ReadInt32BE();

            return new(key, offset, encodedSize, archiveKeys.UnsafeIndex(archiveIndex));
        }

        public static IIndex.Entry ParseFileIndex(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes, ReadOnlySpan<EncodingKey> _)
        {
            // [ N - Key ] [ 4 - Size ]
            Debug.Assert(sizeBytes >= 4);

            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data[keyBytes..].ReadInt32BE();

            return new(key, -1, encodedSize, EncodingKey.Zero);
        }

        public unsafe static IIndex.Entry ParseArchiveIndex(ReadOnlySpan<byte> data, int keyBytes, int offsetBytes, int sizeBytes, ReadOnlySpan<EncodingKey> archiveKeys)
        {
            // [ N - Key ] [ 4 - Size ] [ 4 - Offset ]
            Debug.Assert(sizeBytes >= 4);
            Debug.Assert(offsetBytes >= 4);

            var key = data[..keyBytes].AsKey<EncodingKeyRef>();
            var encodedSize = data.Slice(keyBytes, sizeBytes).ReadInt32BE();
            var offset = data.Slice(keyBytes + sizeBytes, offsetBytes).ReadInt32BE();

            return new(key, offset, encodedSize, archiveKeys.UnsafeIndex(0));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Footer
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
}
