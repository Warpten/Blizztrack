using Pidgin;

using System.Runtime.CompilerServices;

using static Pidgin.Parser;
using static Pidgin.Parser<char>;

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
                    Char('K').Select(_ => 1024),
                    Char('M').Select(_ => 1024 * 1024),
                    Return(1)
                )
            );

            // block-size-spec =  block-size [ '*' block-count ]
            var _blockSizeSpec = Map((blockSize, blockCount) => (BlockSize: blockSize, BlockCount: blockCount),
                _blockSize,
                // If block-size is omitted, 1 is included.
                STAR.Then(NUMBER).Or(Return(1))
            );

            // block-subchunk  =  block-size-spec '=' e-spec ;
            var _blockSubchunk = Map(
                (size, spec) => new SizeAwareChunkSpec(size.BlockSize, size.BlockCount, spec),
                _blockSizeSpec.Before(EQUALS),
                Rec(() => _encodingSpecification!)
            );

            // final-size-spec =  block-size-spec | block-size '*' | '*'
            var _finalSizeSpec = OneOf(
                _blockSizeSpec,
                _blockSize.Before(STAR).Select(size => (BlockSize: size, BlockCount: REPEATING_UNTIL_END)),
                STAR.Select(_ => (BlockSize: GREEDY, BlockCount: 0))
            );

            // final-subchunk  =  final-size-spec '=' e-spec ;
            var _finalSubchunk = Map(
                (size, spec) => new SizeAwareChunkSpec(size.BlockSize, size.BlockCount, spec),
                _finalSizeSpec.Before(EQUALS),
                Rec(() => _encodingSpecification!)
            );

            // ( 'b' ':' ( final-subchunk | '{' ( [{block-subchunk ','}] final-subchunk ) '}' ) )
            var _blockSpec = String("b:").Then(OneOf(
                _finalSubchunk.Select(chunk => new AggregatedChunkSpec(chunk)),
                Brackets(Map(
                    (repeating, last) => new AggregatedChunkSpec([.. repeating, last]),
                    _blockSubchunk.Separated(COMMA),
                    _finalSubchunk
                ))
            ));

            // ( 'z' [ ':' ( zip-level | '{' zip-level ',' zip-bits '}' ) ] )
            var _compressedSpec = Char('z').Then(COLON.Then(
                OneOf(
                    // 'z:$level'
                    NUMBER.Select(level => new CompressionSpec(level, 15)),
                    // 'z:{$level, $bits}'
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
                )).Or(Return(default(CompressionSpec))) // z
            );

            // ( 'e' ':' '{' encryption-key ',' encryption-iv ',' e-spec '}' )
            var _encryptedSpec = String("e:").Then(
                Brackets(Map(
                    (key, iv, spec) => new EncryptedChunkSpec(key, iv, spec),
                    EncryptionKey.Before(COMMA),
                    EncryptionIV.Before(COMMA),
                    Rec(() => _encodingSpecification!)
                ))
            );

            // Overall rule.
            _encodingSpecification = OneOf(
                 Char('n').Select(_ => (IChunkSpec) default(FlatChunkSpec)),
                _compressedSpec.Cast<IChunkSpec>(),
                _encryptedSpec.Cast<IChunkSpec>(),
                _blockSpec.Cast<IChunkSpec>()
                // ( 'c' ':' '{' bcpack-bcn '}' )
                // ( 'g' ':' '{' gdeflate-compression-level '}' )´
            );
        }

        /// <summary>
        /// Produces a parser than is encapsulated by curly brackets.
        /// </summary>
        /// <typeparam name="T">The type of value returned by the parser</typeparam>
        /// <param name="parser">The parser to wrap.</param>
        /// <returns>A new parser.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Parser<char, T> Brackets<T>(Parser<char, T> parser) => LBRACE.Then(parser).Before(RBRACE);

        private const int REPEATING_UNTIL_END = 0;
        private const int GREEDY = -1;
        private const int COMPRESSION_BITS_MPQ = -1;
        private const int COMPRESSION_BITS_ZLIB = -2;
        private const int COMPRESSION_BITS_LZ4HC = -3;

        public interface IChunkSpec;
        //< Describes a chunk of unknown size that is just the decompressed data.
        private record struct FlatChunkSpec : IChunkSpec;
        //< Describes a chunk of unknown size that is compressed using ZLib with the provided parameters.
        private record struct CompressionSpec(int Level = 9, int Window = 15) : IChunkSpec;
        //< Describes a chunk of unknown size that is encrypted with the given parameters. The decrypted data is further compressed
        //  by the given specification.
        private record struct EncryptedChunkSpec(string EncryptionKey, string EncryptionIV, IChunkSpec Spec) : IChunkSpec;

        //< Applies the given size to a specific chunk
        private record struct SizeAwareChunkSpec(int Size, int Count, IChunkSpec Spec) : IChunkSpec;
        private readonly struct AggregatedChunkSpec(params ReadOnlySpan<IChunkSpec> specs) : IChunkSpec {
            private readonly IChunkSpec[] _specs = [..specs];
        }
    }
}
