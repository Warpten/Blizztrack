
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static Blizztrack.Framework.TACT.Implementation.IIndex;
using System.Diagnostics.CodeAnalysis;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Shared.Extensions;
using Blizztrack.Shared;

namespace Blizztrack.Framework.TACT.Implementation
{
    public abstract class AbstractIndex : IIndex
    {
        public readonly short ArchiveIndex;

        protected readonly ResourceHandle _resourceHandle;
        protected readonly Footer _footer;

        private readonly int _blockSizeBytes;
        private readonly int _entrySize;
        private readonly int _entriesPerBlock;
        private readonly int _numBlocks;
        private readonly int _entriesInLastBlock;

        protected abstract Entry ParseEntry(ReadOnlySpan<byte> entry);

        protected AbstractIndex(ResourceHandle handle)
        {
            _resourceHandle = handle;
            handle.Read(^Unsafe.SizeOf<Footer>(), out _footer);

            _blockSizeBytes = _footer.BlockSize << 10;
            _entrySize = _footer.KeyBytes + _footer.SizeBytes + _footer.OffsetBytes;
            _entriesPerBlock = _blockSizeBytes / _entrySize;
            _numBlocks = (int)Math.Ceiling((double)_footer.NumElements / _entriesPerBlock);
            _entriesInLastBlock = (int)_footer.NumElements - (_numBlocks - 1) * _entriesPerBlock;
        }

        protected AbstractIndex(ResourceHandle resource, in Footer footer)
        {
            _resourceHandle = resource;
            _footer = footer;

            _blockSizeBytes = _footer.BlockSize << 10;
            _entrySize = _footer.KeyBytes + _footer.SizeBytes + _footer.OffsetBytes;
            _entriesPerBlock = _blockSizeBytes / _entrySize;
            _numBlocks = (int)Math.Ceiling((double)_footer.NumElements / _entriesPerBlock);
            _entriesInLastBlock = (int)_footer.NumElements - (_numBlocks - 1) * _entriesPerBlock;
        }

        /// <summary>
        /// Enumerates every record within this index.
        /// </summary>
        /// <remarks>The iterator returned by this method has <see cref="IDisposable"/> semantics and <b>must</b> be used with a <see langword="using"/> statement.</remarks>
        [Experimental(diagnosticId: "BT002")]
        public Enumerator Entries
            => AsEnumerable(_resourceHandle.AsMappedMemory(), _blockSizeBytes, _numBlocks, _footer.KeyBytes, _footer.OffsetBytes, _footer.SizeBytes);

        internal abstract Enumerator AsEnumerable(MappedMemory memory, int pageSize, int pageCount, int keyBytes,
            int offsetBytes, int sizeBytes);

        public Entry FindEncodingKey<T>(T encodingKey)
            where T : notnull, IEncodingKey<T>, IKey, allows ref struct
        {
            using var pageData = _resourceHandle.AsMappedMemory();

            StridedReadOnlySpan<byte> toc = pageData.Span.Slice(_numBlocks * _blockSizeBytes, _footer.KeyBytes * _numBlocks)
                .WithStride(_footer.KeyBytes);

            var blockIndex = toc.LowerBoundBy((lhs, rhs) => lhs.SequenceCompareTo(rhs).ToOrdering(), encodingKey.AsSpan());
            if (blockIndex >= toc.Count)
                return default;

            StridedReadOnlySpan<byte> blockData = pageData.Span
                .Slice(blockIndex * _blockSizeBytes, (blockIndex != _numBlocks - 1 ? _entriesPerBlock : _entriesInLastBlock) * _entrySize)
                .WithStride(_entrySize);

            var candidateIndex = blockData.LowerBoundBy((lhs, rhs) => lhs[..rhs.Length].SequenceCompareTo(rhs).ToOrdering(), encodingKey.AsSpan());
            if (candidateIndex >= blockData.Count)
                return default;

            var entry = blockData[candidateIndex];
            if (!entry[.._footer.KeyBytes].SequenceEqual(encodingKey.AsSpan()))
                return default;

            return ParseEntry(entry);
        }

        public unsafe ref struct Enumerator
        {
            private readonly MappedMemory _memoryManager;
            private readonly delegate*<ReadOnlySpan<byte>, int, int, int, int, Entry> _parser;

            private int _pageIndex = 0; // Index of the current page
            private readonly int _pageCount;
            private readonly int _pageSize; // Size of a single page
            private int _entryIndex = 0; // Offset within the current page
            private int _archiveIndex;
            private (int K, int O, int S) _schema;

            internal Enumerator(MappedMemory memoryManager, int pageSize, int pageCount,
                int keyBytes, int offsetBytes, int sizeBytes,
                int archiveIndex,
                delegate*<ReadOnlySpan<byte>, int, int, int, int, Entry> parser)
            {
                _memoryManager = memoryManager;
                _parser = parser;

                _pageSize = pageSize;
                _pageCount = pageCount;

                _schema = (keyBytes, offsetBytes, sizeBytes);
                _archiveIndex = archiveIndex;
            }

            public readonly void Dispose() => _memoryManager.Dispose();

            public readonly Enumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                if (_pageIndex >= _pageCount)
                {
                    Current = default;
                    return false;
                }

                var entrySize = _schema.K + _schema.O + _schema.S;
                var entryOffset = _entryIndex * entrySize;
                if (entryOffset >= _pageSize)
                {
                    ++_pageIndex;
                    _entryIndex = 0;
                }
                else
                {
                    ++_entryIndex;
                }

                var remainingPageData = _memoryManager.Span.Slice(_pageSize * _pageIndex + entryOffset, entrySize);
                Current = _parser(remainingPageData, _schema.K, _schema.O, _schema.S, _archiveIndex);
                return true;
            }

            public Entry Current { get; private set; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected internal unsafe struct Footer
        {
            public byte Revision;
            public fixed byte Flags[2];
            public byte BlockSize;
            public byte OffsetBytes;
            public byte SizeBytes;
            public byte KeyBytes;
            public byte HashBytes;
            public uint NumElements;
            public fixed byte Checksum[8];
        }
    }
}
