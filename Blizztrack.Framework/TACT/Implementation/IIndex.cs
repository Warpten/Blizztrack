using Blizztrack.Shared.IO;

using static Blizztrack.Framework.TACT.Implementation.Index;

namespace Blizztrack.Framework.TACT.Implementation
{
    public interface IIndex
    {
        public EncodingKey Key { get; }

        public Entry FindEncodingKey<T>(T encodingKey)
            where T : notnull, IEncodingKey<T>, allows ref struct;

        public Enumerator Entries { get; }
        public int Count { get; }

        public readonly ref struct Entry
        {
            internal Entry(EncodingKeyRef key, int offset, long length, EncodingKey archive)
            {
                EncodingKey = key;
                Offset = offset;
                Length = length;
                Archive = archive;
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
            /// The name of the archive that contains this record.
            /// </summary>
            public readonly EncodingKey Archive;

            public static implicit operator bool(Entry entry) => entry.EncodingKey != default!;
        }

        public unsafe struct Enumerator
        {
            private readonly EncodingKey[] _archiveNames;
            private readonly IDataSource _dataSource;
            private readonly delegate*<ReadOnlySpan<byte>, int, int, int, InternalEntry> _parser;
            private readonly int _pageCount;
            private readonly int _pageSize; // Size of a single page
            private readonly (int K, int O, int S) _schema; // (key, offset, size) bytes

            private int _pageIndex = 0; // Index of the current page
            private int _entryOffset = 0; // Offset within the current page

            internal Enumerator(IDataSource dataSource, EncodingKey[] archiveNames,
                int pageSize, int pageCount,
                (int, int, int) schema,
                delegate*<ReadOnlySpan<byte>, int, int, int, InternalEntry> parser)
            {
                _dataSource = dataSource;
                _archiveNames = archiveNames;
                _parser = parser;

                _pageSize = pageSize;
                _pageCount = pageCount;

                _schema = schema;
            }

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

            public Entry Current
            {
                get
                {
                    var entrySize = _schema.K + _schema.O + _schema.S;
                    var remainingPageData = _dataSource.Slice(_pageSize * _pageIndex + _entryOffset, entrySize);
                    var rawEntry = _parser(remainingPageData, _schema.K, _schema.O, _schema.S);
                    return new Entry(rawEntry.EncodingKey, rawEntry.Offset, rawEntry.Length, _archiveNames[rawEntry.ArchiveIndex]);
                }
            }
        }

    }
}
