using Blizztrack.Shared.Extensions;
using Blizztrack.Framework.TACT.Resources;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

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

        private static void ParseCompressed(ReadOnlySpan<byte> input, Span<byte> output) => Compression.Instance.Execute(input, output);

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
        internal unsafe struct ChunkInfo(Range compressed, Range decompressed, delegate*<ReadOnlySpan<byte>, Span<byte>, void> parser)
        {
            public readonly Range Compressed = compressed;
            public readonly Range Decompressed = decompressed;
            public delegate*<ReadOnlySpan<byte>, Span<byte>, void> Parser = parser;

            public readonly long CompressedSize => Compressed.End.Value - Compressed.Start.Value;
            public readonly long DecompressedSize => Decompressed.End.Value - Decompressed.Start.Value;

            internal string DebuggerDisplay => $"{Compressed} -> {Decompressed}";
        }
    }
}
