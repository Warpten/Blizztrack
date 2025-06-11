using Blizztrack.Shared.Extensions;
using Blizztrack.Framework.TACT.Resources;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable BT003 // Type or member is obsolete

namespace Blizztrack.Framework.TACT.Implementation
{
    /// <summary>
    /// This object describes a specific BLTE schema.
    /// 
    /// <para>
    /// There are multiple ways to use this object; it can be used to create a schema that can later be used to parse a <see cref="ResourceHandle" />,
    /// or can be used to directly extract the compressed bytes.
    /// </para>
    /// </summary>
    /// <remarks>When using the two-steps implementation, it is primordial to make sure the same <see cref="ResourceHandle" /> is used both at
    /// <see cref="ParseSchema(ResourceHandle, long)" >schema generation</see> time as well as <see cref="Execute(ResourceHandle)">extraction time</see>,
    /// as instances of this object are effectively hardcoded to work for a specific schema. At the very least, make sure the compression schema is
    /// identical if cross-using this object. You can rely on <see cref="Encoding.FindSpecification{T}(T)"/> for this.</remarks>
    /// <example>
    /// <code>
    /// // Example of use of the two-steps implementation.
    /// ResourceHandle handle = ...;
    /// var schema = BLTE.ParseSchema(handle);
    /// var decompressedBytes = schema.Execute(handle);
    /// 
    /// // Example of incorrect use of the two-steps implementation.
    /// ResourceHandle schemaHandle = ...;
    /// ResourceHandle extractionHandle = ...;
    /// Debug.Assert(schemaHandle != extractionHandle);
    /// var schema = BLTE.ParseSchema(handle);
    /// var decompressedBytes = schema.Execute(extractionHandle); // Dragons! Wizards! Time travel! Don't do this.
    /// 
    /// // Example of use of the single-step implementation
    /// ResourceHandle handle = ...;
    /// var decompressedBytes = BLTE.Parse(handle);
    /// </code>
    /// </example>
    public readonly struct BLTE
    {
        public readonly int Flags;
        private readonly ChunkInfo[] _chunks;

        public int DecompressedSize => _chunks[^1].Decompressed.End.Value;

        private BLTE(int flags, ChunkInfo[] chunks)
        {
            Flags = flags;
            _chunks = chunks;
        }

        /// <summary>
        /// Parses the given resource according to its schema and returns an in-memory buffer.
        /// </summary>
        /// <param name="resourceHandle">The resource to parse</param>
        /// <param name="decompressedSize">The expected decompressed size of the file.</param>
        /// <returns>A byte buffer containing the decompressed file.</returns>
        public static byte[] Parse(ResourceHandle resourceHandle, long decompressedSize = 0)
            => ParseSchema(resourceHandle, decompressedSize).Execute(resourceHandle);

        /// <summary>
        /// Executes the current schema over the provided resource, returning a new byte buffer containing the decompressed file.
        /// </summary>
        /// <param name="resourceHandle">A handle to a TACT/CASC file system resource.</param>
        /// <returns>A byte buffer containing the decompressed file.</returns>
        public unsafe byte[] Execute(ResourceHandle resourceHandle)
        {
            using var inputData = resourceHandle.AsMappedMemory();

            var decompressedSize = _chunks[^1].Decompressed.End.Value;
            var dataBuffer = GC.AllocateUninitializedArray<byte>(decompressedSize);

            for (var i = 0; i < _chunks.Length; ++i)
            {
                ref var currentChunk = ref _chunks[i];
                currentChunk.Parser(inputData.Span[currentChunk.Compressed], dataBuffer.AsSpan()[currentChunk.Decompressed]);
            }

            return dataBuffer;
        }

        /// <summary>
        /// Attempts to parse a BLTE schema out of the given span. 
        /// </summary>
        /// <typeparam name="K">The type of encoding key.</typeparam>
        /// <param name="fileData">A contiguous span of memory representing the file's data.</param>
        /// <param name="encodingKey">An encoding key that should match the calculated checksum of the file.</param>
        /// <param name="decompressedSize">The expected decompressed size of the file. If zero, this parameter is ignored</param>
        /// <remarks>
        /// If the checksum calculated does not match <paramref name="encodingKey"/>, <see langword="default" /> is returned.
        /// If the decompressed size does not match <paramref name="decompressedSize"/>, <see langword="default"/> is returned.
        /// </remarks>
        /// <returns>A schema that can then be used to parse a file.</returns>
        public unsafe static BLTE ParseSchema<K>(ReadOnlySpan<byte> fileData, K? encodingKey, long decompressedSize = 0)
            where K : notnull, IEncodingKey<K>, IKey, allows ref struct
        {
            var (flags, chunks, expectedChecksum) = ParseHeader(fileData, 0, 0);
            EnsureSchemaValidity(chunks, decompressedSize);

            var checksumMatches = encodingKey?.SequenceEqual(expectedChecksum) ?? true;
            var sizeMatches = decompressedSize != 0 && chunks[^1].Decompressed.End.Value == decompressedSize;

            if (chunks.Length == 0 || !checksumMatches || !sizeMatches)
                return default;

            return new BLTE(flags, chunks);
        }

        /// <summary>
        /// Attempts to parse a BLTE schema out of the given span. 
        /// </summary>
        /// <typeparam name="K">The type of encoding key.</typeparam>
        /// <param name="fileData">A contiguous span of memory representing the file's data.</param>
        /// <param name="decompressedSize">The expected decompressed size of the file. If zero, this parameter is ignored</param>
        /// <remarks>
        /// If the decompressed size does not match <paramref name="decompressedSize"/>, <see langword="default"/> is returned.
        /// </remarks>
        /// <returns>A schema that can then be used to parse a file.</returns>
        public unsafe static BLTE ParseSchema(ReadOnlySpan<byte> fileData, long decompressedSize = 0)
        {
            var (flags, chunks, _) = ParseHeader(fileData, 0, 0);
            EnsureSchemaValidity(chunks, decompressedSize);

            var sizeMatches = decompressedSize == 0 || chunks[^1].Decompressed.End.Value == decompressedSize;

            if (chunks.Length == 0 || !sizeMatches)
                return default;

            return new (flags, chunks);
        }


        /// <summary>
        /// Attempts to parse a BLTE schema out of the given span. 
        /// </summary>
        /// <typeparam name="K">The type of encoding key.</typeparam>
        /// <param name="resourceHandle">A handle over a resource.</param>
        /// <param name="decompressedSize">The expected decompressed size of the file. If zero, this parameter is ignored</param>
        /// <remarks>
        /// If the checksum calculated does not match <paramref name="encodingKey"/>, <see langword="default" /> is returned.
        /// If the decompressed size does not match <paramref name="decompressedSize"/>, <see langword="default"/> is returned.
        /// </remarks>
        /// <returns>A schema that can then be used to parse a file.</returns>
        public unsafe static BLTE ParseSchema(ResourceHandle resourceHandle, long decompressedSize = 0)
        {
            using var memoryManager = resourceHandle.AsMappedMemory();

            return ParseSchema(memoryManager.Span, decompressedSize);
        }

        /// <summary>
        /// Attempts to parse a BLTE schema out of the given span. 
        /// </summary>
        /// <typeparam name="K">The type of encoding key.</typeparam>
        /// <param name="resourceHandle">A handle over a resource.</param>
        /// <param name="encodingKey">An encoding key that should match the calculated checksum of the file.</param>
        /// <param name="decompressedSize">The expected decompressed size of the file. If zero, this parameter is ignored</param>
        /// <remarks>
        /// If the checksum calculated does not match <paramref name="encodingKey"/>, <see langword="default" /> is returned.
        /// If the decompressed size does not match <paramref name="decompressedSize"/>, <see langword="default"/> is returned.
        /// </remarks>
        /// <returns>A schema that can then be used to parse a file.</returns>
        public static BLTE ParseSchema<K>(ResourceHandle resourceHandle, K? encodingKey, long decompressedSize)
            where K : notnull, IEncodingKey<K>, IKey, allows ref struct
        {
            using var memoryManager = resourceHandle.AsMappedMemory();

            return ParseSchema(memoryManager.Span, encodingKey, decompressedSize);
        }


        [Conditional("DEBUG")]
        private static void EnsureSchemaValidity(ChunkInfo[] chunks, long decompressedSize)
        {
            if (decompressedSize != 0)
                Debug.Assert(chunks.Aggregate(0L, (n, c) => n + c.DecompressedSize) == decompressedSize, "Mismatched size");

            for (var i = 1; i < chunks.Length; ++i)
            {
                ref var previousChunk = ref chunks[i - 1];
                ref var currentChunk = ref chunks[i];

                Debug.Assert(previousChunk.Compressed.End.Value + 1 == currentChunk.Compressed.Start.Value, "Hole found in compressed bytes according to generated schema");
                Debug.Assert(previousChunk.Decompressed.End.Value == currentChunk.Decompressed.Start.Value, "Hole found in decompressed bytes according to generated schema");
            }
        }

        private static void ParseImmediate(ReadOnlySpan<byte> input, Span<byte> output) => input.CopyTo(output);

        private static void ParseCompressed(ReadOnlySpan<byte> input, Span<byte> output) => Compression.Instance.Decompress(input, output);

        private static unsafe (int, ChunkInfo[], EncodingKey) ParseHeader(ReadOnlySpan<byte> fileData, int compressedBase = 0, int decompressedBase = 0)
        {
            var magic = fileData[..4];
            if (!magic.SequenceEqual([(byte)'B', (byte)'L', (byte)'T', (byte)'E']))
                return (0, [], default);

            var headerSize = fileData[4..].ReadInt32BE();
            var flagsChunkCount = fileData[8..].ReadUInt32BE();

            var expectedChecksum = MD5.HashData(fileData[..headerSize]);

            var flags = (int) flagsChunkCount >> 24;
            var chunkCount = (int) (flagsChunkCount & 0x00FFFFFFu);

            var chunks = new List<ChunkInfo>(chunkCount);

            var compressedStart = headerSize + compressedBase;
            var decompressedStart = decompressedBase;

            var chunkData = fileData.Slice(12, chunkCount * (4 + 4 + 16)).WithStride(4 + 4 + 16);
            for (var i = 0; i < chunkCount; ++i)
            {
                var chunkCompressedSize = chunkData[i].ReadInt32BE();
                var chunkDecompressedSize = chunkData[i][4..].ReadInt32BE();
                var checksum = chunkData[i].Slice(4 + 4, 16);
                // TODO: Validate checksum ?

                Range compressedRange = new(compressedStart + 1, compressedStart + chunkCompressedSize);
                Range decompressedRange = new(decompressedStart, decompressedStart + chunkDecompressedSize);

                chunks.Add(new(compressedRange, decompressedRange, null));

                compressedStart = compressedRange.End.Value;
                decompressedStart = decompressedRange.End.Value;
            }

            // This is another loop that abuses cache locality
            // but also has special logic to flatten BLTE nested chunks.
            for (var i = 0; i < chunks.Count; ++i)
            {
                // Because Span's indexer still does bounds checking.
                ref var currentChunk = ref Unsafe.Add(ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(chunks)), i);

                var compressionMode = fileData[currentChunk.Compressed.Start.Value - 1];
                switch (compressionMode)
                {
                    case (byte) 'N':
                        currentChunk.Parser = &ParseImmediate;
                        break;
                    case (byte) 'Z':
                        currentChunk.Parser = &ParseCompressed;
                        break;
                    case (byte) 'F':
                        var (_, nestedChunks, _) = ParseHeader(fileData[currentChunk.Compressed], currentChunk.Compressed.Start.Value, currentChunk.Decompressed.Start.Value);

                        // Copy-insert everything - Not using RemoveAt + InsertRange because...
                        // One regrow, two memmoves. More efficient on codegen (one less memmove incurred by RemoveAt)

                        chunks.Capacity = chunks.Count + nestedChunks.Length - 1; // 1. Reallocate if necessary (one chunk will get written over)
                        var targetSpan = CollectionsMarshal.AsSpan(chunks);
                        targetSpan[(i + 1)..].CopyTo(targetSpan[(i + nestedChunks.Length)..]);  // 2. Move any chunk after this one ahead
                        nestedChunks.AsSpan().CopyTo(targetSpan.Slice(i, nestedChunks.Length)); // 3. And insert the new ones, overwriting the current object in the process.

                        i += nestedChunks.Length - 1; // Skip past the inserted chunks.
                        break;
                    default:
                        throw new IndexOutOfRangeException(nameof(compressionMode));
                }
            }

            return (flags, [.. chunks], ((ReadOnlySpan<byte>) expectedChecksum).AsKey<EncodingKey>());
        }

        [DebuggerDisplay("{DebuggerDisplay,nq}")]
        internal unsafe struct ChunkInfo(Range compressed, Range decompressed,
            delegate*<ReadOnlySpan<byte>, Span<byte>, void> parser)
        {
            public readonly Range Compressed = compressed;
            public readonly Range Decompressed = decompressed;

            public delegate*<ReadOnlySpan<byte>, Span<byte>, void> Parser = parser;

            public readonly long CompressedSize => Compressed.End.Value - Compressed.Start.Value;
            public readonly long DecompressedSize => Decompressed.End.Value - Decompressed.Start.Value;

            internal readonly string DebuggerDisplay => $"{Compressed} -> {Decompressed}";
        }
    }


    public static partial class SpecificationExtensions
    {
        public static ISpecification ToSchema(this string specification)
        {
            (_, var schema) = ParseEncodingSpec(specification.AsSpan(), 0);
            return schema;
        }

        private static (int, ISpecification) ParseEncodingSpec(ReadOnlySpan<char> source, int startOffset)
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

        private static (int, ISpecification) ParseBlocks(ReadOnlySpan<char> source, int startOffset)
        {
            if (source[startOffset] != ':')
                throw new InvalidOperationException("Malformed espec");

            if (source[startOffset + 1] == '{')
            {
                var chunks = new List<ISpecification>();
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

        private static (int, ISpecification) ParseCompressedBlock(ReadOnlySpan<char> source, int startOffset)
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

        private static (int, ISpecification) ParseBlockSubchunk(ReadOnlySpan<char> source, int startOffset)
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
    }

    unsafe ref struct SpecificationVisitor(int fileSize) : ISpecificationVisitor
    {
        private readonly int _fileSize = fileSize;
        private int _cursor = 0;

        // Internal scrap variable used to keep track of the source chunk size.
        // Initialize to the whole file so that top-level chunks are nicely dealt with.
        private Range _currentRange = new (0, fileSize);

        private List<(Range, nint Fn, nint Opaque)> _parsers = [];

        private void ProcessChunkSpecification(Range dataRange, ISpecification chunkSpecification)
        {
            _currentRange = dataRange;
            chunkSpecification.Accept(this);
        }

        public void VisitBlockSizedChunk(int length, int repetitions, ISpecification chunk)
        {
            for (var i = repetitions; i != 0 && _cursor < _fileSize; _cursor += length, --repetitions)
                ProcessChunkSpecification(new Range(_cursor, _cursor + length), chunk);

            // TODO: There may be a bug in disguise here if the trailing chunk in a greedy chunk is actually
            //       propagating the bit window size of the regular-sized chunks.
            if (repetitions == -1)
                ProcessChunkSpecification(new Range(_cursor, _fileSize), chunk);
        }

        public void VisitCompressedChunk(int level, int bits)
        {
            if (bits != -1)
            {
                // Materializes the pointer
                int pfn = (int) (delegate*<ReadOnlySpan<byte>, Span<byte>, nint, void>) &ExecuteCompress;

                // Bits go from 9 to 15, which means that they can be encoded by mapping from 0 to 6.
                // (9 -> 0)..(15 -> 6). Thus, to encode a value up to six, we need 3 bits.
                // On top of that, we have compression levels that go from 0 to 9 - and therefore fit
                // 5 bits.
                // Coincidentally, this means we can code the level and the bits on a single byte -
                // But we choose to use an opaque storage that is as wide as one register.
                _parsers.Add((_currentRange, pfn , (level << 3) | (bits - 9)));

                return;
            }

            VisitCompressedChunk(level, (_currentRange.End.Value - _currentRange.Start.Value) switch {
                <= 0x200 => 9,
                <= 0x400 => 10,
                <= 0x800 => 11,
                <= 0x1000 => 12,
                <= 0x2000 => 13,
                <= 0x4000 => 14,
                _ => 15
            });
        }

        public void VisitFlatChunk()
        {
            // Materializes the pointer
            int pfn = (int)(delegate*<ReadOnlySpan<byte>, Span<byte>, nint, void>) &ExecuteFlat;
            _parsers.Add((_currentRange, pfn, 0));
        }

        private static void ExecuteFlat(ReadOnlySpan<byte> source, Span<byte> dest, nint _)
            => source.CopyTo(dest);

        private static void ExecuteCompress(ReadOnlySpan<byte> source, Span<byte> dest, nint opaque)
        {
            var windowBits = (int)(opaque & 0b111);
            var compressionLevel = (int)((opaque >> 3) & 0b11111);

            Compression.Instance.Compress(source, dest, compressionLevel, windowBits);
        }
    }

    public interface ISpecification
    {
        public void Accept<T>(T visitor) where T : ISpecificationVisitor, allows ref struct;
    }

    public interface ISpecificationVisitor
    {
        public void VisitCompressedChunk(int level, int bits);
        public void VisitFlatChunk();
        public void VisitBlockSizedChunk(int length, int repetitions, ISpecification chunk);
    }

    record class CompressedChunk(int Level, int Bits) : ISpecification
    {
        public void Accept<T>(T visitor) where T : ISpecificationVisitor, allows ref struct => visitor.VisitCompressedChunk(Level, Bits);
    }
    record class FlatChunk() : ISpecification
    {
        public void Accept<T>(T visitor) where T : ISpecificationVisitor, allows ref struct => visitor.VisitFlatChunk();
    }
    record class Chunks(ISpecification[] Items) : ISpecification
    {
        public void Accept<T>(T visitor) where T : ISpecificationVisitor, allows ref struct
        {
            foreach (var chunk in Items)
                chunk.Accept(visitor);
        }
    }
    record class BlockSizedChunk(ISpecification Spec, int Size, char Unit, int Repetitions) : ISpecification
    {
        public void Accept<T>(T visitor) where T : ISpecificationVisitor, allows ref struct
            => visitor.VisitBlockSizedChunk(Unit switch
            {
                'K' => Size * 1024,
                'M' => Size * 1024 * 1024,
                'b' => Size,
                _ => throw new ArgumentOutOfRangeException(nameof(Unit))
            }, Repetitions, Spec);
    }
}

#pragma warning restore BT003 // Type or member is obsolete
