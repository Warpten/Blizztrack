using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Shared.Extensions;
using Blizztrack.Shared.IO;

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using static Blizztrack.Framework.TACT.Implementation.Install;

namespace Blizztrack.Framework.TACT.Implementation
{
    // TODO: Clean up this file

    /// <summary>
    /// This object describes a specific BLTE schema.
    /// 
    /// <para>
    /// There are multiple ways to use this object; it can be used to create a schema
    /// that can later be used to parse a <see cref="ResourceHandle" />,
    /// or can be used to directly extract the compressed bytes.
    /// </para>
    /// </summary>
    /// <remarks>When using the two-steps implementation, it is primordial to make sure
    /// the same <see cref="ResourceHandle" /> is used both at 
    /// <see cref="ParseSchema(ResourceHandle, long)" >schema generation</see> time as
    /// well as <see cref="Execute(ResourceHandle)">extraction time</see>,
    /// as instances of this object are effectively hardcoded to work for a specific
    /// schema. At the very least, make sure the compression schema is identical if
    /// cross-using this object. You can rely on
    /// <see cref="Encoding.FindSpecification{T}(T)"/> for this.</remarks>
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
    /// // Note that this is called on a different resource handle than the one used to parse.
    /// // This is usually not recommended but will be fine if you can guarantee that both
    /// // resources are using the same schema.
    /// var decompressedBytes = schema.Execute(extractionHandle);
    /// 
    /// // Example of use of the single-step implementation
    /// ResourceHandle handle = ...;
    /// var decompressedBytes = BLTE.Parse(handle);
    /// 
    /// // You are also able to extract part of a file. In that situation, you must use the two-phase extraction logic:
    /// ResourceHandle handle = ...;
    /// var schema = BLTE.ParseSchema(handle);
    /// // The ranges provided below are relative to the decompressed file.
    /// var decompressedFragment = schema.Execute(handle, 0..1024);
    /// var decompressedFragment = schema.Execute(handle, 100..200);
    /// </code>
    /// </example>
    public readonly struct BLTE
    {
        private readonly ChunkInfo[] _chunks;
        private readonly int _decompressedSize;

        public readonly int Flags;

        private BLTE(int flags, int decompressedSize, ChunkInfo[] chunks)
        {
            _chunks = chunks;
            _decompressedSize = decompressedSize;

            Flags = flags;
        }

        public static async Task<BLTE> Parse(Stream sourceStream, string specification, CancellationToken stoppingToken = default)
        {
            // 1. Read the top of the file.
            var dataBuffer = new byte[8];
            await sourceStream.ReadExactlyAsync(dataBuffer, stoppingToken);

            if (BinaryPrimitives.ReadUInt32LittleEndian(dataBuffer) != 0x45544C42u)
                throw new InvalidOperationException("The provided stream does not encapsulate a BLTE resource.");

            var headerSize = BinaryPrimitives.ReadInt32BigEndian(dataBuffer.AsSpan(4));

            // 2. Construct a complete header
            var fileHeader = GC.AllocateUninitializedArray<byte>(headerSize);
            Buffer.BlockCopy(dataBuffer, 0, fileHeader, 0, 8);
            await sourceStream.ReadExactlyAsync(dataBuffer.AsMemory(8), stoppingToken);

            // 3. Parse the header.
            var dataSource = dataBuffer.ToDataSource();
            var (flags, chunks, encodingKey) = ParseHeader(dataSource, 0, 0);

            // 4. Read the spec string.
            var chunkSpec = Spec.Parse(specification);

            // 5. Update chunks with the compression modes.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Parses the given resource according to its schema and returns an in-memory buffer.
        /// </summary>
        /// <param name="resourceHandle">The resource to parse</param>
        /// <param name="decompressedSize">The expected decompressed size of the file.</param>
        /// <returns>A byte buffer containing the decompressed file.</returns>
        public static byte[] Parse(ResourceHandle resourceHandle, long decompressedSize = 0)
            => ParseSchema(resourceHandle, decompressedSize).Execute(resourceHandle);

        [Obsolete("Use BLTE.Parse(Stream, string, CancellationToken) instead.")]
        public static async Task<MemoryStream> Parse(Stream sourceStream, long decompressedSize = 0, CancellationToken stoppingToken = default)
        {
            var schema = await ParseSchema(sourceStream, decompressedSize, stoppingToken);
            return await schema.Execute(sourceStream, stoppingToken);
        }

        /// <summary>
        /// Executes the current schema over the provided resource, returning a new byte buffer containing the decompressed file.
        /// </summary>
        /// <param name="resourceHandle">A handle to a TACT/CASC file system resource.</param>
        /// <returns>A byte buffer containing the decompressed file.</returns>
        public unsafe byte[] Execute(ResourceHandle resourceHandle)
        {
            using var inputData = resourceHandle.ToMappedDataSource();

            var dataBuffer = GC.AllocateUninitializedArray<byte>(_decompressedSize);

            for (var i = 0; i < _chunks.Length; ++i)
            {
                ref var currentChunk = ref _chunks[i];
                currentChunk.Parser(inputData[currentChunk.Compressed], dataBuffer.AsSpan(currentChunk.Decompressed), 0);
            }

            return dataBuffer;
        }

        public async Task<MemoryStream> Execute(Stream sourceStream, CancellationToken stoppingToken = default)
        {
            var outputBuffer = GC.AllocateUninitializedArray<byte>(_decompressedSize);

            for (var i = 0; i < _chunks.Length; ++i)
            {
                var compressedChunkSize = _chunks[i].CompressedSize;
                var decompressedChunkEnd = _chunks[i].Decompressed.End.Value;
                var outputRange = _chunks[i].Compressed;

                var inputBuffer = getInputBuffer(compressedChunkSize, decompressedChunkEnd, outputBuffer, _decompressedSize);
                await sourceStream.ReadExactlyAsync(inputBuffer, stoppingToken);

                // And now decompress
                unsafe
                {
                    _chunks[i].Parser(inputBuffer.Span, outputBuffer[outputRange], 0);
                }
            }

            return new MemoryStream(outputBuffer);

            static Memory<byte> getInputBuffer(int compressedChunkSize, int decompressedChunkEnd, byte[] outputBuffer, long decompressedSize)
            {
                // If there is enough space right after this chunk's decompressed data for the compressed data,
                // use that space as a scrap buffer to save an allocation
                if (compressedChunkSize + decompressedChunkEnd <= decompressedSize)
                    return new ArraySegment<byte>(outputBuffer, decompressedChunkEnd, compressedChunkSize);
                
                // Otherwise, allocate a temporary buffer
                return GC.AllocateUninitializedArray<byte>(compressedChunkSize);
            }
        }

        /// <summary>
        /// Extracts the <paramref name="dataRange"/> bytes out of the <paramref name="resourceHandle"/>.
        /// </summary>
        /// <param name="resourceHandle"></param>
        /// <param name="dataRange">A range.</param>
        /// <returns>An </returns>
        public unsafe byte[] Execute(ResourceHandle resourceHandle, Range dataRange)
        {
            using var inputData = resourceHandle.ToMappedDataSource();

            var (_, decompressedLength) = dataRange.GetOffsetAndLength(_decompressedSize);
            var dataBuffer = GC.AllocateUninitializedArray<byte>(decompressedLength);

            for (var i = 0; i < _chunks.Length && decompressedLength != 0; ++i)
            {
                ref var currentChunk = ref _chunks[i];

                // Compute how many bytes we need to read in this chunk.
                var chunkRange = dataRange.Intersection(_decompressedSize, currentChunk.Decompressed);
                if (chunkRange.Equals(default)) // No overlap ?
                    continue;

                // Get the offset and length of data in this chunk.
                var (offset, length) = chunkRange.GetOffsetAndLength(currentChunk.DecompressedSize);

                // We read the whole input chunk because compressed chunks will need to discard.
                // Flat chunks will just copy-paste the data around.
                var input = inputData[currentChunk.Compressed];
                var output = dataBuffer.AsSpan().Slice(offset, length);

                currentChunk.Parser(input, output, offset);
                // Update the remainder. It's faster to do this than re-calculating an intersection.
                decompressedLength -= length;
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
            where K : notnull, IEncodingKey<K>, allows ref struct
        {
            var (flags, chunks, expectedChecksum) = ParseHeader(fileData);
            EnsureSchemaValidity(chunks, decompressedSize);

            var checksumMatches = encodingKey?.SequenceEqual(expectedChecksum) ?? true;
            var sizeMatches = decompressedSize != 0 && chunks[^1].Decompressed.End.Value == decompressedSize;

            if (chunks.Length == 0 || !checksumMatches || !sizeMatches)
                return default;

            return new BLTE(flags, chunks[^1].Decompressed.End.Value, chunks);
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
            var (flags, chunks, _) = ParseHeader(fileData);
            EnsureSchemaValidity(chunks, decompressedSize);

            var sizeMatches = decompressedSize == 0 || chunks[^1].Decompressed.End.Value == decompressedSize;

            if (chunks.Length == 0 || !sizeMatches)
                return default;

            return new (flags, chunks[^1].Decompressed.End.Value, chunks);
        }

        public static async ValueTask<BLTE> ParseSchema(Stream sourceStream, long decompressedSize = 0, CancellationToken stoppingToken = default)
        {
            // Manually reconstruct a complete header.
            var fileStart = new byte[8];
            await sourceStream.ReadExactlyAsync(fileStart, stoppingToken);

            if (BinaryPrimitives.ReadUInt32LittleEndian(fileStart) != 0x45544C42u)
                throw new InvalidOperationException("The provided stream does not encapsulate a BLTE resource.");

            var headerSize = BinaryPrimitives.ReadInt32BigEndian(fileStart.AsSpan(4));

            var fileHeader = GC.AllocateUninitializedArray<byte>(headerSize);
            Buffer.BlockCopy(fileStart, 0, fileHeader, 0, 8);
            await sourceStream.ReadExactlyAsync(fileHeader.AsMemory(8), stoppingToken);

            return ParseSchema(fileHeader, decompressedSize);
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
            using var memoryManager = resourceHandle.ToMappedDataSource();

            return ParseSchema(memoryManager..], decompressedSize);
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
            where K : notnull, IEncodingKey<K>, allows ref struct
        {
            using var memoryManager = resourceHandle.ToMappedDataSource();

            return ParseSchema(memoryManager[..], encodingKey, decompressedSize);
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

        private static void ParseImmediate(ReadOnlySpan<byte> input, Span<byte> output, int discardCount)
            => input.Slice(discardCount, output.Length).CopyTo(output);

        private static void ParseCompressed(ReadOnlySpan<byte> input, Span<byte> output, int discardCount)
            => Compression.Instance.Decompress(input, output, discardCount, windowBits: 15);

        private static unsafe void CompleteChunks(List<ChunkInfo> chunks, ReadOnlySpan<byte> fileData)
        {
            // This is another loop that abuses cache locality
            // but also has special logic to flatten BLTE nested chunks.
            for (var i = 0; i < chunks.Count; ++i)
            {
                // Because Span's indexer still does bounds checking.
                ref var currentChunk = ref Unsafe.Add(ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(chunks)), i);

                var compressionMode = fileData[currentChunk.Compressed.Start.Value - 1];
                switch (compressionMode)
                {
                    case (byte)'N':
                        currentChunk.Parser = &ParseImmediate;
                        break;
                    case (byte)'Z':
                        currentChunk.Parser = &ParseCompressed;
                        break;
                    case (byte)'F':
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
        }

        /// <summary>
        /// Reads the BLTE header from the given data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileData"></param>
        /// <param name="compressedBase"></param>
        /// <param name="decompressedBase"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe (int, List<ChunkInfo>, EncodingKey) ParseHeader<T>(T fileData, int compressedBase, int decompressedBase)
            where T : IDataSource, allows ref struct
        {
            var magic = fileData[..4];
            if (!magic.SequenceEqual([(byte)'B', (byte)'L', (byte)'T', (byte)'E']))
                return (0, [], default);

            var headerSize = fileData[4..].ReadInt32BE();
            var flagsChunkCount = fileData[8..].ReadUInt32BE();

            var expectedChecksum = MD5.HashData(fileData[..headerSize]);

            var flags = (int)flagsChunkCount >> 24;
            var chunkCount = (int)(flagsChunkCount & 0x00FFFFFFu);

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

            return (flags, chunks, new EncodingKey(expectedChecksum));
        }

        private static unsafe (int, ChunkInfo[], EncodingKey) ParseHeader(ReadOnlySpan<byte> fileData, int compressedBase = 0, int decompressedBase = 0)
        {
            var (flags, chunks, encodingKey) = ParseHeader(fileData.ToDataSource(), compressedBase, decompressedBase);
            if (flags == 0)
                return (flags, [], default);

            CompleteChunks(chunks, fileData);
            return (flags, [.. chunks], encodingKey);
        }

        [DebuggerDisplay("{DebuggerDisplay,nq}")]
        internal unsafe struct ChunkInfo(Range compressed, Range decompressed,
            delegate*<ReadOnlySpan<byte>, Span<byte>, int, void> parser)
        {
            public readonly Range Compressed = compressed;
            public readonly Range Decompressed = decompressed;

            public delegate*<ReadOnlySpan<byte>, Span<byte>, int /* discardOutput */, void> Parser = parser;

            public readonly int CompressedSize => Compressed.End.Value - Compressed.Start.Value;
            public readonly int DecompressedSize => Decompressed.End.Value - Decompressed.Start.Value;

            internal readonly string DebuggerDisplay => $"{Compressed} -> {Decompressed}";
        }
    }
}

#pragma warning restore BT003 // Type or member is obsolete
