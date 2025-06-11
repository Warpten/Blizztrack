using Blizztrack.Shared.Extensions;

using System.Diagnostics;
using System.Runtime.CompilerServices;

using static Blizztrack.Shared.Extensions.BinarySearchExtensions;
using Blizztrack.Shared;
using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT.Resources;
using System.Text;

namespace Blizztrack.Framework.TACT.Implementation
{
    /// <summary>
    /// An implementation of an encoding file.
    /// </summary>
    public sealed class Encoding : IResourceParser<Encoding>
    {
        private readonly IBinaryDataSupplier _dataSupplier;
        private readonly EncodingSchema _header;
        private readonly Lazy<string[]> _encodingSpecs;

        #region IResourceParser
        public static Encoding OpenResource(ResourceHandle decompressedHandle)
            => Open(decompressedHandle.ToMemoryMappedData());

        public static Encoding OpenCompressedResource(ResourceHandle compressedHandle)
            => Open(BLTE.Parse(compressedHandle).ToDataSupplier());
        #endregion

        public static Encoding Open<T>(T dataSource) where T : IBinaryDataSupplier
        {
            var (version, header) = ReadHeader(dataSource);

            return new(dataSource, version, header, () =>
            {
                var encodingSpecs = dataSource[header.EncodingSpec];
                var accumulator = new List<string>();

                while (encodingSpecs.Length != 0)
                {
                    var entry = encodingSpecs.ReadCString();

                    accumulator.Add(entry);
                    encodingSpecs = encodingSpecs[(entry.Length + 1)..];
                }

                return [.. accumulator];
            });
        }

        private Encoding(IBinaryDataSupplier dataSource, int version, EncodingSchema header, Func<string[]> encodingSpecs)
        {
            _dataSupplier = dataSource;

            _header = header;
            _encodingSpecs = new (encodingSpecs);
        }

        /// <summary>
        /// Returns an object able to enumerate entries in this file.
        /// </summary>
        public Accessor<Entry> Entries => new(this, _header.CEKey);

        /// <summary>
        /// Returns an object able to enumerate specification references in this file.
        /// </summary>
        public Accessor<Spec> Specifications => new(this, _header.EKeySpec);

        /// <summary>
        /// Attempts to find a record associated with a content key.
        /// </summary>
        /// <typeparam name="T">The concrete type of the content key.</typeparam>
        /// <param name="contentKey">The content key to look for.</param>
        /// <returns>A record. If the content key can't be found, returns <see langword="default"/>.</returns>
        public Entry FindContentKey<K>(K contentKey) where K : IContentKey<K>, IKey, allows ref struct
        {
            var targetPage = _header.CEKey.ResolvePage(_dataSupplier, contentKey.AsSpan());
            while (targetPage.Length != 0)
            {
                var (entry, entrySize) = ParseEntry(targetPage, _header.EKeySize, _header.CKeySize);
                Debug.Assert(entrySize > 0);

                targetPage = targetPage[entrySize..];

                if (entry.Count == 0)
                    continue;

                if (entry.ContentKey.SequenceEqual(contentKey))
                    return entry;
            }

            return default;
        }

        /// <summary>
        /// Returns the encoding specification for a given encoding key.
        /// </summary>
        /// <param name="encodingKey">The encoding key to look for.</param>
        /// <returns>A record. If the encoding key can't be found, returns <see langword="default"/>.</returns>
        public Spec FindSpecification<K>(K encodingKey) where K : IEncodingKey<K>, IKey, allows ref struct
        {
            var targetPage = _header.EKeySpec.ResolvePage(_dataSupplier, encodingKey.AsSpan());
            while (targetPage.Length != 0)
            {
                var (specification, specSize) = ParseSpecification(targetPage, _header.EKeySize, _header.CKeySize);
                if (specification.Key.SequenceEqual(encodingKey))
                    return specification;

                targetPage = targetPage[specSize..];
            }

            return default;
        }

        private static ParsedValue<Spec> ParseSpecification(ReadOnlySpan<byte> fileData, int ekeySize, int ckeySize)
        {
            if (fileData.Length < ekeySize + 4 + 5)
                return new (default, 0);

            var encodingKey = fileData[..ekeySize].AsKey<EncodingKeyRef>();
            var specIndex = fileData[ekeySize..].ReadInt32BE();
            var encodedFileSize = (ulong) fileData[(ekeySize + 4)..].ReadInt40BE();

            return new(new(encodingKey, specIndex, encodedFileSize), ekeySize + 4 + 5);
        }

        private static ParsedValue<Entry> ParseEntry(ReadOnlySpan<byte> fileData, int ekeySize, int ckeySize)
        {
            var keyCount = fileData[0];
            var entrySize = 1 + 5 + ckeySize + ekeySize * keyCount;

            if (fileData.Length < entrySize)
                return new(default, 0);

            var recordData = fileData.Slice(1, 5 + ckeySize + ekeySize * keyCount);

            if (keyCount == 0)
                return new (default, 0); // Should technically return entrySize, but this is a page end marker record.

            var recordContentKey = recordData.Slice(5, ckeySize);
            var recordEncodingKeys = recordData.Slice(5 + ckeySize, keyCount * ekeySize);
            var fileSize = (ulong) recordData.ReadInt40BE();

            return new (new (recordContentKey, keyCount, recordEncodingKeys, fileSize), entrySize);
        }

        private static unsafe (int, EncodingSchema) ReadHeader<T>(T dataSource) where T : IBinaryDataSupplier
        {
            if (dataSource[0] != 0x45 || dataSource[1] != 0x4E)
                throw new InvalidOperationException("Invalid signature in encoding file");

            var version = dataSource[0x02];
            if (version != 1)
                throw new InvalidOperationException("Unsupported version in encoding");

            var hashSizeCKey = dataSource[0x03];
            var hashSizeEKey = dataSource[0x04];
            var ckeyPageSize = dataSource[0x05..].ReadUInt16BE() * 1024;
            var ekeyPageSize = dataSource[0x07..].ReadUInt16BE() * 1024;
            var ckeyPageCount = dataSource[0x09..].ReadInt32BE();
            var ekeyPageCount = dataSource[0x0D..].ReadInt32BE();
            Debug.Assert(dataSource[0x11] == 0x00);
            var especBlockSize = dataSource[0x12..].ReadInt32BE();

            Range especRange = new (22, 22 + especBlockSize);
            Range ckeyHeaderRange = new (especRange.End, especRange.End.Value + ckeyPageCount * (hashSizeCKey + 0x10));
            Range ckeyRange = new (ckeyHeaderRange.End, ckeyHeaderRange.End.Value + ckeyPageSize * ckeyPageCount);
            Range ekeyHeaderRange = new (ckeyRange.End, ckeyRange.End.Value + ekeyPageCount * (hashSizeEKey + 0x10));
            Range ekeyRange = new (ekeyHeaderRange.End, ekeyHeaderRange.End.Value + ekeyPageSize * ekeyPageCount);

            return (version, new (
                hashSizeCKey,
                hashSizeEKey,
                especRange,
                new(ckeyHeaderRange, ckeyRange, hashSizeCKey + 0x10, ckeyPageSize, &ParseEntry),
                new(ekeyHeaderRange, ekeyRange, hashSizeEKey + 0x10, ekeyPageSize, &ParseSpecification)
            ));
        }

#pragma warning disable CS0660, CS0661 // Irrelevant on ref structs because ref structs can't be boxed.

        public readonly ref struct Accessor<E> where E : notnull, allows ref struct
        {
            private readonly Encoding _encoding;
            private readonly TableSchema<E> _schema;

            internal Accessor(Encoding encoding, TableSchema<E> schema)
            {
                _encoding = encoding;
                _schema = schema;
            }

            /// <summary>
            /// Enumerates every entry within the associated file.
            /// </summary>
            /// <remarks>The iterator returned by this accessor has <see cref="IDisposable"/> semantics and <b>must</b> be used with a <see langword="using"/> statement.</remarks>
            public readonly Enumerator<E> Enumerate()
                => _schema.Enumerate(_encoding._dataSupplier, _encoding._header.EKeySize, _encoding._header.CKeySize);

            /// <summary>
            /// Provides access to individual pages within the associated file.
            /// </summary>
            public readonly PagesAccessor<E> Pages => new(_encoding, _schema);
        }

        public readonly ref struct PagesAccessor<E> where E : notnull, allows ref struct
        {
            private readonly Encoding _encoding;
            private readonly TableSchema<E> _schema;

            public int Count => _schema.PageCount;

            internal PagesAccessor(Encoding encoding, TableSchema<E> schema)
            {
                _encoding = encoding;
                _schema = schema;
            }

            /// <summary>
            /// Provides access to every entry within this page.
            /// </summary>
            /// <param name="index"></param>
            /// <remarks>The iterator returned by this accessor has <see cref="IDisposable"/> semantics and <b>must</b> be used with a <see langword="using"/> statement.</remarks>
            public readonly Enumerator<E> this[int index]
                => _schema.EnumeratePage(_encoding._dataSupplier, _encoding._header.EKeySize, _encoding._header.CKeySize, index);
        }

        /// <summary>
        /// A thin wrapper around specification information for an <see cref="IEncodingKey{T}">encoding key</see>.
        /// </summary>
        /// <remarks>If you want to persist this type, you're meant to store it as your own type. ABI stability is not guaranteed.</remarks>
        public readonly ref struct Spec
        {
            internal Spec(EncodingKeyRef key, int index, ulong fileSize)
            {
                Key = key;
                Index = index;
                FileSize = fileSize;
            }

            /// <summary>
            /// The encoding key associated with this record.
            /// </summary>
            public readonly EncodingKeyRef Key;

            /// <summary>
            /// The index of the specification string within the <see cref="Encoding"/> file.
            /// </summary>
            public readonly int Index;

            /// <summary>
            /// The compressed size of this file.
            /// </summary>
            public readonly ulong FileSize;

            /// <summary>
            /// Returns the encoding specification string corresponding to this record.
            /// </summary>
            /// <param name="encoding">The instance of the <see cref="Encoding">encoding file</see> that provided this specification.</param>
            /// <returns>A specification string, if any.</returns>
            /// <remarks>
            /// The specification string array backing up this getter is lazily-loaded. 
            /// <para>
            /// If the <see cref="Encoding{T}"/> file that is passed to
            /// this method does not match the one that was used to obtain this instance, the results of this call are unspecified.
            /// </para>
            /// </remarks>
            public readonly string GetSpecificationString(Encoding encoding) => encoding._encodingSpecs.Value[Index];

            public static bool operator ==(Spec left, Spec right) => left.Key.SequenceEqual(right.Key) && left.Index == right.Index && left.FileSize == right.FileSize;
            public static bool operator !=(Spec left, Spec right) => !(left == right);
        }

        public readonly ref struct Entry
        {
            private readonly StridedReadOnlySpan<byte> _encodingKeys;

            public readonly ContentKeyRef ContentKey;

            public readonly int Count => _encodingKeys.Count;
            public readonly ulong FileSize;

            internal unsafe Entry(ReadOnlySpan<byte> contentKey, byte keyCount, ReadOnlySpan<byte> encodingKeys, ulong fileSize)
            {
                _encodingKeys = encodingKeys.WithStride(encodingKeys.Length / keyCount);

                ContentKey = contentKey.AsKey<ContentKeyRef>();
                FileSize = fileSize;
            }

            public EncodingKeyRef this[int index] => _encodingKeys[index].AsKey<EncodingKeyRef>();
            public EncodingKeyRef this[Index index] => this[index.GetOffset(Count)];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator bool(Entry self) => self.Count != 0;

            public static bool operator ==(Entry lhs, Entry rhs) => lhs.FileSize == rhs.FileSize && lhs._encodingKeys.SequenceEqual(rhs._encodingKeys);
            public static bool operator !=(Entry lhs, Entry rhs) => !(lhs == rhs);
        }

#pragma warning restore CS0660, CS0661

        internal readonly ref struct ParsedValue<T>(T value, int length) where T : notnull, allows ref struct
        {
            public readonly T Value = value;
            public readonly int Length = length;

            public void Deconstruct(out T value, out int length)
            {
                value = Value;
                length = Length;
            }
        }

        internal unsafe readonly struct TableSchema<E>(Range header, Range pages, int headerEntrySize, int pageSize,
            delegate*<ReadOnlySpan<byte>, int, int, ParsedValue<E>> parser)
            where E : notnull, allows ref struct
        {
            private readonly Range _header = header;
            private readonly Range _pages = pages;
            private readonly int _headerEntrySize = headerEntrySize;
            private readonly int _pageSize = pageSize;

            public readonly int PageCount => (_pages.End.Value - _pages.Start.Value) / _pageSize;

            public readonly Enumerator<E> Enumerate(IBinaryDataSupplier fileData, int ekeySize, int ckeySize)
                => new(fileData, _pages, _pageSize, ekeySize, ckeySize, parser);

            public readonly Enumerator<E> EnumeratePage(IBinaryDataSupplier fileData, int ekeySize, int ckeySize, int pageIndex)
                => new(fileData, new Range(_pages.Start.Value + pageIndex * _pageSize, _pages.Start.Value + (pageIndex + 1) * _pageSize), _pageSize, ekeySize, ckeySize, parser);

            public readonly ReadOnlySpan<byte> ResolvePage(IBinaryDataSupplier fileData, ReadOnlySpan<byte> needle)
            {
                Debug.Assert(needle.Length <= _headerEntrySize);

                var entryIndex = fileData[_header].WithStride(_headerEntrySize).LowerBoundBy((itr, needle) =>
                {
                    var ordering = itr[..needle.Length].SequenceCompareTo(needle).ToOrdering();
                    return ordering switch
                    {
                        Ordering.Equal => Ordering.Less,
                        _ => ordering
                    };
                }, needle) - 1;

                if (entryIndex * _headerEntrySize > _header.End.Value)
                    return [];

                return fileData.Slice(_pages.Start.Value + _pageSize * entryIndex, _pageSize);
            }
        }

        internal readonly struct EncodingSchema(int ckeySize, int ekeySize, Range encodingSpec,
            TableSchema<Entry> ceKey, TableSchema<Spec> ekeySpec)
        {
            public readonly int CKeySize = ckeySize;
            public readonly int EKeySize = ekeySize;
            public readonly Range EncodingSpec = encodingSpec;
            public readonly TableSchema<Entry> CEKey = ceKey;
            public readonly TableSchema<Spec> EKeySpec = ekeySpec;
        }


        /// <summary>
        /// Enumerates entries within a table.
        /// </summary>
        /// <remarks>This object has <see cref="IDisposable"/> semantics and <b>must</b> be used in an <c>using</c> block.</remarks>
        public unsafe struct Enumerator<E> where E : notnull, allows ref struct
        {
            private readonly IBinaryDataSupplier _memoryManager;
            private readonly delegate*<ReadOnlySpan<byte>, int, int, ParsedValue<E>> _parser;
            private readonly int _ekeySize;
            private readonly int _ckeySize;

            private readonly int _pageStart;       // Start of all pages
            private readonly int _pageSize;          // Size of a page
            private int _pageIndex = 0;              // Current page index
            private readonly int _pageCount;         // Amount of pages

            private int _entryOffset = 0;            // Offset of the current entry
            private int _entrySize = 0;

            internal Enumerator(IBinaryDataSupplier memoryManager, Range pages, int pageSize,
                int ekeySize, int ckeySize,
                delegate*<ReadOnlySpan<byte>, int, int, ParsedValue<E>> parser)
            {
                _memoryManager = memoryManager;
                _parser = parser;
                _ekeySize = ekeySize;
                _ckeySize = ckeySize;

                _pageStart = pages.Start.Value;
                _pageSize = pageSize;
                _pageCount = (pages.End.Value - pages.Start.Value) / pageSize;
            }

            public Enumerator<E> GetEnumerator() => this;

            public bool MoveNext()
            {
                if (_pageIndex >= _pageCount)
                    return false;

                // Get a span over the remainder of the page.
                var pageRemainder = _memoryManager.Slice(_pageStart + _pageSize * _pageIndex + _entryOffset, _pageSize - _entryOffset);
                (_, _entrySize) = _parser(pageRemainder, _ekeySize, _ckeySize);

                // If the current offset is past the end of the page, rebase back to the start of the page.
                if (_entrySize == 0 && _pageIndex < _pageCount)
                {
                    ++_pageIndex;
                    _entryOffset = 0;

                    return MoveNext();
                }

                _entryOffset += _entrySize;
                return _entrySize != 0;
            }

            public E Current
            {
                get
                {
                    var pageRemainder = _memoryManager.Slice(_pageStart + _pageSize * _pageIndex + _entryOffset - _entrySize, _pageSize - _entryOffset + _entrySize);
                    (var current, _) = _parser(pageRemainder, _ekeySize, _ckeySize);
                    return current;
                }
            }
        }
    }

    public static partial class EncodingExtensions
    {
        public static IChunk ToSchema(this string specification)
        {
            (_, var schema) = ParseEncodingSpec(specification.AsSpan(), 0);
            return schema;
        }

        private static (int, IChunk) ParseEncodingSpec(ReadOnlySpan<char> source, int startOffset)
        {
            switch (source[startOffset])
            {
                case 'b':
                    return ParseBlocks(source, startOffset + 1);
                case 'n':
                    return (startOffset + 1, new FlatChunk());
                case 'z':
                    return ParseCompressedBlock(source, startOffset + 1);
            }

            throw new InvalidOperationException("Non-supported espec");
        }

        private static (int, IChunk) ParseBlocks(ReadOnlySpan<char> source, int startOffset)
        {
            if (source[startOffset] != ':')
                throw new InvalidOperationException("Malformed espec");

            if (source[startOffset + 1] == '{')
            {
                var chunks = new List<IChunk>();
                while (true)
                {
                    (startOffset, var chunk) = ParseBlockSubchunk(source, startOffset + 2);
                    chunks.Add(chunk);

                    if (source[startOffset] == '}')
                        break;

                    Debug.Assert(source[startOffset] == ',');
                    ++startOffset;
                }
                
                ++startOffset;
                return (startOffset + 1, new Chunks([.. chunks]));
            }
            
            return ParseBlockSubchunk(source, startOffset + 1);
        }

        private static (int, IChunk) ParseCompressedBlock(ReadOnlySpan<char> source, int startOffset)
        {
            if (source[startOffset] != ':')
                return (startOffset, new CompressedChunk(9, 15));

            if (source[startOffset + 1] == '{')
            {
                var i = 0;
                while (source[startOffset + i + 2] >= '0' && source[startOffset + i + 2] <= '9')
                    ++i;

                var level = int.Parse(source.Slice(startOffset + 2, i));
                Debug.Assert(source[startOffset + i + 2] == ',');

                var j = 0;
                while (source[startOffset + i + 3 + j] != '}')
                    ++j;

                var bits = new Range(startOffset + i + 3, startOffset + i + 3 + j);
                if (source[bits].SequenceEqual("mpq"))
                    return (startOffset + i + 3 + j + 1, new CompressedChunk(level, -1));

                if (source[bits].SequenceEqual("zlib") || source[bits].SequenceEqual("lz4hc"))
                    throw new InvalidOperationException("Unknown compressed bits");

                return (startOffset + i + 3 + j + 1, new CompressedChunk(level, int.Parse(source[bits])));
            }
            else
            {
                var i = 0;
                while (source[startOffset + i] >= '0' && source[startOffset + i] <= '9')
                    ++i;

                var level = int.Parse(source.Slice(startOffset, i));
                return (startOffset + i, new CompressedChunk(level, 15));
            }
        }

        private static (int, IChunk) ParseBlockSubchunk(ReadOnlySpan<char> source, int startOffset)
        {
            (startOffset, var blockSize, var blockSizeUnit, var repetitionCount) = parseBlockSizeSpec(source, startOffset);
            if (source[startOffset] != '=')
                throw new InvalidOperationException("Malformed espec block subchunk");

            (startOffset, var blockSpec) = ParseEncodingSpec(source, startOffset + 1);
            return (startOffset, new BlockSizedChunk(blockSpec, blockSize, blockSizeUnit, repetitionCount));

            static (int, int, char, int) parseBlockSizeSpec(ReadOnlySpan<char> source, int startOffset)
            {
                var i = 0;
                while (source[startOffset + i] >= '0' && source[startOffset + i] <= '9')
                    ++i;

                var size = int.Parse(source.Slice(startOffset, i));
                if (source[startOffset + i] == 'K' || source[startOffset + i] == 'M')
                {
                    if (source[startOffset + i + 1] == '*')
                    {
                        var j = 0;
                        while (source[startOffset + i + 2 + j] >= '0' && source[startOffset + i + 2 + j] <= '9')
                            ++j;

                        if (j == 0)
                            return (startOffset + i + 2, size, source[startOffset + i], -1);

                        return (startOffset + i + 2 + j, size, source[startOffset + i], int.Parse(source.Slice(startOffset + i + 2, j)));
                    }

                    return (startOffset + i + 1, size, source[startOffset + i], 1);
                }

                if (source[startOffset + i] == '*')
                {
                    var j = 0;
                    while (source[startOffset + i + 1 + j] >= '0' && source[startOffset + i + 1 + j] <= '9')
                        ++j;

                    if (j == 0)
                        return (startOffset + i + 1, size, source[startOffset + i], -1);

                    return (startOffset + i + 1 + j, size, 'b', int.Parse(source.Slice(startOffset + i + 1, j)));
                }

                return (startOffset + i, size, 'b', 1);
            }
        }

        public interface IChunk;
        private record class CompressedChunk(int Level, int Bits) : IChunk
        {
            public override string ToString() => $"z:{{{Level},{Bits}}}";
        }
        private record class FlatChunk() : IChunk
        {
            public override string ToString() => "n";
        }
        private record class Chunks(IChunk[] Items) : IChunk
        {
            public override string ToString() => "b:{" + string.Join(',', Items.AsEnumerable()) + "}";
        }
        private record BlockSizedChunk(IChunk Spec, int Size, char Unit, int Repetitions) : IChunk
        {
            public override string ToString()
            {
                var sb = new StringBuilder($"{Size}");
                if (Unit != 'b') sb.Append(Unit);
                if (Repetitions < 0) sb.Append('*');
                else if (Repetitions > 1) sb.Append('*').Append(Repetitions);

                return sb.Append('=').Append(Spec).ToString();
            }
        }
    }
}
