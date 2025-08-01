﻿using Blizztrack.Shared.Extensions;

using System.Diagnostics;
using System.Runtime.CompilerServices;

using static Blizztrack.Shared.Extensions.BinarySearchExtensions;
using Blizztrack.Shared;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Shared.IO;

namespace Blizztrack.Framework.TACT.Implementation
{
    /// <summary>
    /// An implementation of an encoding file.
    /// </summary>
    public sealed class Encoding : IResourceParser<Encoding>, IDisposable
    {
        private readonly IDataSource _dataSupplier;
        private readonly EncodingSchema _header;
        private readonly Lazy<string[]> _encodingSpecs;

        #region IResourceParser
        public static Encoding OpenResource(ResourceHandle decompressedHandle)
            => Open(decompressedHandle.ToMappedDataSource());

        public static Encoding OpenCompressedResource(ResourceHandle compressedHandle)
            => Open(BLTE.Parse(compressedHandle).ToDataSource());
        #endregion

        public static Encoding Open<T>(T dataSource)
            where T : notnull, IDataSource
        {
            ReadOnlySpan<byte> mappedHandle = dataSource[..22];
            var (version, header) = ReadHeader(mappedHandle);

            return new(dataSource, version, header, () =>
            {
                var encodingSpecs = dataSource[header.EncodingSpec];
                var accumulator = new List<string>();

                var encodingCursor = encodingSpecs[..];
                while (encodingCursor.Length != 0)
                {
                    var entry = encodingCursor.ReadCString();

                    accumulator.Add(entry);
                    encodingCursor = encodingCursor[(entry.Length + 1)..];
                }

                return [.. accumulator];
            });
        }

        private Encoding(IDataSource dataSource, int version, EncodingSchema header, Func<string[]> encodingSpecs)
        {
            _dataSupplier = dataSource;

            _header = header;
            _encodingSpecs = new (encodingSpecs);
        }

        public void Dispose() => _dataSupplier.Dispose();

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
        public Entry FindContentKey<K>(in K contentKey) where K : IContentKey<K>, IKey, allows ref struct
        {
            var targetPage = _header.CEKey.ResolvePage(_dataSupplier, contentKey.AsSpan());
            while (targetPage.Length != 0)
            {
                // TODO: Figure out a way to binary search through this
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

        private static unsafe (int, EncodingSchema) ReadHeader(ReadOnlySpan<byte> mappedHandle)
        {
            if (mappedHandle[0] != 0x45 || mappedHandle[1] != 0x4E)
                throw new InvalidOperationException("Invalid signature in encoding file");

            var version = mappedHandle[0x02];
            if (version != 1)
                throw new InvalidOperationException("Unsupported version in encoding");

            var hashSizeCKey = mappedHandle[0x03];
            var hashSizeEKey = mappedHandle[0x04];
            var ckeyPageSize = mappedHandle[0x05..].ReadUInt16BE() * 1024;
            var ekeyPageSize = mappedHandle[0x07..].ReadUInt16BE() * 1024;
            var ckeyPageCount = mappedHandle[0x09..].ReadInt32BE();
            var ekeyPageCount = mappedHandle[0x0D..].ReadInt32BE();
            Debug.Assert(mappedHandle[0x11] == 0x00);
            var especBlockSize = mappedHandle[0x12..].ReadInt32BE();

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
            /// If the <see cref="Encoding"/> file that is passed to this method does not match the one that was used to obtain this instance,
            /// the results of this call are unspecified.
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

            public EncodingKey[] Keys
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    var keys = new EncodingKey[Count];
                    for (var i = 0; i < Count; ++i)
                        keys[i] = _encodingKeys[i].AsKey<EncodingKey>();
                    return keys;
                }
            }

            public EncodingKeyRef this[int index] => _encodingKeys[index].AsKey<EncodingKeyRef>();
            public EncodingKeyRef this[System.Index index] => this[index.GetOffset(Count)];

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

            public readonly Enumerator<E> Enumerate(IDataSource fileData, int ekeySize, int ckeySize)
                => new(fileData, _pages, _pageSize, ekeySize, ckeySize, parser);

            public readonly Enumerator<E> EnumeratePage(IDataSource fileData, int ekeySize, int ckeySize, int pageIndex)
                => new(fileData, new Range(_pages.Start.Value + pageIndex * _pageSize, _pages.Start.Value + (pageIndex + 1) * _pageSize), _pageSize, ekeySize, ckeySize, parser);

            public readonly ReadOnlySpan<byte> ResolvePage(IDataSource fileData, ReadOnlySpan<byte> needle)
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
            private readonly IDataSource _memoryManager;
            private readonly delegate*<ReadOnlySpan<byte>, int, int, ParsedValue<E>> _parser;
            private readonly int _ekeySize;
            private readonly int _ckeySize;

            private readonly int _pageStart; // Start of all pages
            private readonly int _pageSize;  // Size of a page
            private int _pageIndex = 0;      // Current page index
            private readonly int _pageCount; // Amount of pages

            private int _entryOffset = 0;    // Offset of the current entry
            private int _entrySize = 0;

            internal Enumerator(IDataSource memoryManager, Range pages, int pageSize,
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
}
