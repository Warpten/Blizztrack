using Blizztrack.Framework.Extensions;

using Pidgin;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Xml;

using static Pidgin.Parser<char>;

using static Pidgin.Parser;

namespace Blizztrack.Framework.TACT.Implementation
{
    public static class Spec
    {
        public static IChunkSpec Parse(string specificationString)
        {
            var chunkSpec = _encodingSpecification.Parse(specificationString);
            if (!chunkSpec.Success)
                throw new InvalidOperationException("Malformed specification string");

            return chunkSpec.Value;
        }

        private static readonly Parser<char, char> COLON = Char(':');
        private static readonly Parser<char, char> COMMA = Char(',');
        private static readonly Parser<char, char> EQUALS = Char('=');
        private static readonly Parser<char, char> STAR = Char('*');

        private static readonly Parser<char, char> LBRACE = Char('{');
        private static readonly Parser<char, char> RBRACE = Char('}');
        private static readonly Parser<char, int> NUMBER = UnsignedInt(10);


        private static readonly Parser<char, char> HexDigit = Token(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        private static readonly Parser<char, string> EncryptionKey = HexDigit.RepeatString(16); // 8 bytes
        private static readonly Parser<char, string> EncryptionIV = HexDigit.RepeatString(8);   // 4 bytes

        private static readonly Parser<char, IChunkSpec> _encodingSpecification;

        static Spec()
        {
            // block-size  =  number [ 'K' | 'M' ]
            var _blockSize = Map((size, unit) => size * unit,
                NUMBER,
                OneOf(
                    Char('M').Select(_ => 1024 * 1024),
                    Char('K').Select(_ => 1024),
                    Return(1)
                )
            ).Labelled("block-size");

            // block-size-spec =  block-size [ '*' block-count ]
            var _blockSizeSpec = Map((blockSize, blockCount) => (BlockSize: blockSize, BlockCount: blockCount),
                _blockSize,
                // If block-size is omitted, 1 is included.
                Try(STAR.Then(OneOf(Try(NUMBER), Return(1))))
            ).Labelled("block-size-spec");

            // block-subchunk  =  block-size-spec '=' e-spec ;
            var _blockSubchunk = Map(
                (size, spec) => new SizeAwareChunkSpec(size.BlockSize, size.BlockCount, spec),
                _blockSizeSpec.Before(EQUALS),
                Rec(() => _encodingSpecification!)
            ).Labelled("block-subchunl");

            // final-size-spec =  block-size-spec | block-size '*' | '*'
            //                 =  block-size '*' block-count  | block-size '*' | '*'
            //                 =  block-size '*' [ block-count ] | '*'
            var _finalSizeSpec = OneOf(
                STAR.Select(_ => (BlockSize: GREEDY, BlockCount: 0)),
                Map(
                    (size, count) => (BlockSize: size, BlockCount: count),
                    _blockSize.Before(STAR),
                    Try(NUMBER).Or(Return(REPEATING_UNTIL_END))
                )
            ).Labelled("final-size-spec");

            // var _finalSizeSpec = OneOf(
            //     _blockSizeSpec,
            //     _blockSize.Before(STAR).Select(size => (BlockSize: size, BlockCount: REPEATING_UNTIL_END)),
            //     STAR.Select(_ => (BlockSize: GREEDY, BlockCount: 0))
            // ).Labelled("final-size-spec");

            // final-subchunk  =  final-size-spec '=' e-spec ;
            var _finalSubchunk = Map(
                (size, spec) => new SizeAwareChunkSpec(size.BlockSize, size.BlockCount, spec),
                _finalSizeSpec.Before(EQUALS),
                Rec(() => _encodingSpecification!)
            ).Labelled("final-subchunk");

            // ( 'b' ':' ( final-subchunk | '{' ( [{block-subchunk ','}] final-subchunk ) '}' ) )
            var _blockSpec = String("b:").Then(OneOf(
                Try(_finalSubchunk.Select(chunk => new AggregatedChunkSpec(chunk))),
                // This has to be done a bit weirdly
                // You'd expect to be able to do
                Brackets(_blockSubchunk.Before(COMMA)
                    .ManyThen(_finalSubchunk)
                    .Select(tpl => new AggregatedChunkSpec([..tpl.Item1, tpl.Item2])))
                // However that does not appear to work, probably because it's left associative instead of right-associative.
                // Instead, we choose to treat a block-spec as a sequence of final-subchunks; if they're malformed, then parsing
                // will fail later.
                // TODO: Fix this
                //Brackets(_blockSubchunk.Separated(COMMA).Select(repeating => new AggregatedChunkSpec([..repeating])))
            )).Labelled("block-spec");
            // var _blockSpec = String("b:").Then(OneOf(
            //     Try(_finalSubchunk.Select(chunk => new AggregatedChunkSpec([chunk]))),
            //     Brackets(new BlockSpecParser<char>(_blockSubchunk.Cast<IChunkSpec>(), _finalSubchunk.Cast<IChunkSpec>(), COMMA))
            // )).Labelled("block-spec");

            // var _blockSpec = String("b:").Then(Brackets(_finalSubchunk)).Select(chunk => new AggregatedChunkSpec(chunk)).Labelled("block-spec");
            // var _blockSpec = String("b:").Then(Brackets(new BlockSpecParser<char>(_blockSubchunk.Cast<IChunkSpec>(), _finalSubchunk.Cast<IChunkSpec>(), COMMA)));

            // ( 'z' [ ':' ( zip-level | '{' zip-level ',' zip-bits '}' ) ] )
            var _compressedSpec = Char('z').Then(
                OneOf(
                    Try(COLON.Then(OneOf(
                        NUMBER.Select(level => new CompressionSpec(level, 15)),
                        Brackets(Map(
                            (level, bits) => new CompressionSpec(level, bits),
                            NUMBER.Before(COMMA),
                            OneOf(
                                NUMBER,
                                String("mpq").Select(_ => COMPRESSION_BITS_MPQ),
                                String("zlib").Select(_ => COMPRESSION_BITS_ZLIB),
                                String("lz4hc").Select(x => COMPRESSION_BITS_LZ4HC)
                            )
                        ))
                    ))),
                    Return(new CompressionSpec(9, 15))
                )
            ).Labelled("z-spec");

            // ( 'e' ':' '{' encryption-key ',' encryption-iv ',' e-spec '}' )
            var _encryptedSpec = String("e:").Then(
                Brackets(Map(
                    (key, iv, spec) => new EncryptedChunkSpec(key, iv, spec),
                    EncryptionKey.Before(COMMA),
                    EncryptionIV.Before(COMMA),
                    Rec(() => _encodingSpecification!)
                ))
            ).Labelled("e-spec");

            // Overall rule.
            _encodingSpecification = OneOf(
                Char('n').Select(_ => (IChunkSpec) default(FlatChunkSpec)),
                Try(_compressedSpec.Cast<IChunkSpec>()),
                Try(_encryptedSpec.Cast<IChunkSpec>()),
                Try(_blockSpec.Cast<IChunkSpec>())
                // ( 'c' ':' '{' bcpack-bcn '}' )
                // ( 'g' ':' '{' gdeflate-compression-level '}' )´
            ).Labelled("root-spec");
        }

        /// <summary>
        /// Produces a parser than is encapsulated by curly brackets.
        /// </summary>
        /// <typeparam name="T">The type of value returned by the parser</typeparam>
        /// <param name="parser">The parser to wrap.</param>
        /// <returns>A new parser.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Parser<char, T> Brackets<T>(Parser<char, T> parser) => Map((l, m, r) => m, LBRACE, parser, RBRACE);

        private class BlockSpecParser<TToken>(Parser<TToken, IChunkSpec> head, Parser<TToken, IChunkSpec> tail, Parser<TToken, TToken> delimiter)
            : Parser<TToken, AggregatedChunkSpec>
        {
            private readonly Parser<TToken, TToken> _delimiter = delimiter;
            private readonly Parser<TToken, IChunkSpec> _head = head;
            private readonly Parser<TToken, IChunkSpec> _tail = tail;

            public override bool TryParse(ref ParseState<TToken> state, ref PooledList<Expected<TToken>> expecteds, [MaybeNullWhen(false)] out AggregatedChunkSpec result)
            {
                result = default;

                List<(IChunkSpec Chunk, long Position)> chunks = [];

                while (state.HasCurrent)
                {
                    var location = state.Bookmark();
                    PooledList<Expected<TToken>> headExpecteds = new (state.Configuration.ArrayPoolProvider.GetArrayPool<Expected<TToken>>());

                    var elementOutcome = _head.TryParse(ref state, ref headExpecteds, out var elementResult);
                    if (!elementOutcome)
                    {
                        state.Rewind(location);
                        break;
                    }

                    chunks.Add((Chunk: elementResult!, Position: location));

                    var delimiterOutcome = _delimiter.TryParse(ref state, ref expecteds, out _);

                    headExpecteds.Dispose();
                    if (!delimiterOutcome)
                    {
                        state.Rewind(location);
                        break;
                    }   
                }

                var finalPosition = state.Location;

                state.Seek(chunks[0].Position);
                if (chunks.Count == 0)
                    return false;

                // Validate that the last chunk is actually a finalSubblock
                state.Seek(chunks[^1].Position);
                if (!_tail.TryParse(ref state, ref expecteds, out var finalChunk))
                    state.Seek(chunks[0].Position);

                state.Seek(finalPosition);
                result = new AggregatedChunkSpec(chunks.Select(c => c.Chunk).ToArray());

                foreach (var item in chunks.Select(c => c.Position))
                    state.DiscardBookmark(item);

                return true;
            }
        }

        private const int REPEATING_UNTIL_END = -2;
        private const int GREEDY = -1;
        private const int COMPRESSION_BITS_MPQ = -1;
        private const int COMPRESSION_BITS_ZLIB = -2;
        private const int COMPRESSION_BITS_LZ4HC = -3;

        public interface IChunkSpec
        {
            public void Accept<T>(T visitor, int compressedSize) where T : IVisitor, allows ref struct
                => Accept(visitor, new TraversalContext() {
                    Size = compressedSize
                });

            void Accept<T>(T visitor, TraversalContext context) where T : IVisitor, allows ref struct;
        }

        //< Describes a chunk of unknown size that is just the decompressed data.
        private record struct FlatChunkSpec : IChunkSpec
        {
            readonly void IChunkSpec.Accept<T>(T visitor, TraversalContext context) where T : default
                => visitor.OnRawChunk(context.Size);
        }

        //< Describes a chunk of unknown size that is compressed using ZLib with the provided parameters.
        private record struct CompressionSpec(int Level = 9, int Window = 15) : IChunkSpec
        {
            readonly void IChunkSpec.Accept<T>(T visitor, TraversalContext context) where T : default
                => visitor.OnCompressedChunk(Level, Window, context.Size);
        }

        //< Describes a chunk of unknown size that is encrypted with the given parameters. The decrypted data is further compressed
        //  by the given specification.
        private record struct EncryptedChunkSpec(string EncryptionKey, string EncryptionIV, IChunkSpec Spec) : IChunkSpec
        {
            readonly void IChunkSpec.Accept<T>(T visitor, TraversalContext context) where T : default
            {
                // Push the encryption state on the stack
                visitor.BeginEncryption(EncryptionKey, EncryptionIV);
                Spec.Accept(visitor, context);
                visitor.EndEncryption();
            }
        }

        //< Applies the given size to a specific chunk
        private record struct SizeAwareChunkSpec(int Size, int Count, IChunkSpec Spec) : IChunkSpec
        {
            readonly void IChunkSpec.Accept<T>(T visitor, TraversalContext context) where T : default
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(context.Size, 0, nameof(context.Size));

                if (Count == REPEATING_UNTIL_END)
                {
                    var chunkCount = context.Size / Size;
                    var finalChunkSize = context.Size % Size;

                    // Update context with the size of every chunk.
                    context.Size = Size;
                    for (var i = 0; i < chunkCount; ++i)
                        Spec.Accept(visitor, context);

                    // And finally the size of the final chunk.
                    context.Size = finalChunkSize;
                    if (finalChunkSize != 0)
                        Spec.Accept(visitor, context);

                    // Done consuming.
                    context.Size = 0;
                }
                else if (Size == GREEDY)
                {
                    // Consume all.
                    Spec.Accept(visitor, context);
                    context.Size = 0;
                }
                else
                {
                    // A repeating chunk of this.Size bytes, repeating Count times.
                    for (var i = 0; i < Count; ++i)
                        Spec.Accept(visitor, context);
                }
            }
        }

        private readonly struct AggregatedChunkSpec(params ReadOnlySpan<IChunkSpec> specs) : IChunkSpec {
            private readonly IChunkSpec[] _specs = [..specs];

            readonly void IChunkSpec.Accept<T>(T visitor, TraversalContext context) where T : default
            {
                for (var i = 0; i < _specs.Length; ++i)
                    _specs[i].Accept(visitor, context);
            }

        }

        public class TraversalContext
        {
            public int Size { get; internal set; }
        }

        public interface IVisitor
        {
            public void OnCompressedChunk(int level, int windowBits, int chunkSize);
            public void OnRawChunk(int chunkSize);

            public void BeginEncryption(string key, string iv);
            public void EndEncryption();
        }
    }
}
