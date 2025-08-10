using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.TACT.Implementation
{
    internal class Specification
    {
        private static readonly Parser<char, char> HexDigit = Token(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        // Basic parsers
        private static readonly Parser<char, char> COLON = Char(':');
        private static readonly Parser<char, char> COMMA = Char(',');
        private static readonly Parser<char, char> EQUALS = Char('=');
        private static readonly Parser<char, char> STAR = Char('*');

        private static readonly Parser<char, char> LBRACE = Char('{');
        private static readonly Parser<char, char> RBRACE = Char('}');
        private static readonly Parser<char, int> NUMBER = UnsignedInt(10);

        private static readonly Parser<char, string> EncryptionKey = HexDigit.RepeatString(16); // 8 bytes
        private static readonly Parser<char, string> EncryptionIV = HexDigit.RepeatString(8);   // 4 bytes

        /// <summary>
        /// Produces a parser than is encapsulated by curly brackets.
        /// </summary>
        /// <typeparam name="T">The type of value returned by the parser</typeparam>
        /// <param name="parser">The parser to wrap.</param>
        /// <returns>A new parser.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Parser<char, T> Brackets<T>(Parser<char, T> parser) => LBRACE.Then(parser).Before(RBRACE);

        static Specification() {
            _blockSize = Map(
                (size, unit) => unit.GetValueOrDefault() switch {
                    'K' => size * 1024,
                    'M' => size * 1024 * 1024,
                    _ => size
                },
                NUMBER,
                OneOf(Char('K'), Char('M')).Optional()
            );

            _blockSizeSpec = Map(
                (size, count) => (Size: size, Count: count),
                _blockSize,
                NUMBER.Or(Return(1))
            );

            _blockSubchunk = Map(
                (size, spec) => Enumerable.Repeat((size.Size, Spec: spec), size.Count),
                _blockSizeSpec.Before(EQUALS),
                Rec(() => _encodingSpecification!)
            );

            _finalSizeSpec = OneOf(
                _blockSizeSpec,
                _blockSize.Before(STAR).Select(size => (Size: size, Count: -1)),
                Return((Size: -1, Count: 0))
            );

            _finalSubchunk = Map(
                (size, spec) => Enumerable.Repeat((size.Size, Spec: spec), Math.Max(1, size.Count)),
                _finalSizeSpec.Before(EQUALS),
                Rec(() => _encodingSpecification!)
            );

            static IEnumerable<(int Size, T Spec)> merge<T>(IEnumerable<IEnumerable<(int, T)>> bodyChunks, IEnumerable<(int, T)> tailChunks)
            {
                foreach (var bodyChunk in bodyChunks)
                    foreach (var chunk in bodyChunk)
                        yield return chunk;

                foreach (var chunk in tailChunks)
                    yield return chunk;
            }

            _blockSpec = String("b:").Then(OneOf(
                _finalSubchunk,
                Brackets(Map(merge, _blockSubchunk.SeparatedAtLeastOnce(COMMA), _finalSubchunk))
            ));

            _compressedSpec = Char('z').Then(COLON.Then(
                OneOf(
                    // 'z:$level'
                    NUMBER.Select(level => new CompressedChunkSupplier(level, 15)),
                    // 'z:{$level, $bits}'
                    Brackets(Map(
                        (level, bits) => new CompressedChunkSupplier(level, bits),
                        NUMBER.Before(COMMA),
                        OneOf(
                            NUMBER,
                            String("mpq").Select(_ => -1),
                            String("zlib").Select(_ => -2),
                            String("lz4hc").Select(x => -3)
                        )
                    ))
                )).Or(Return(CompressedChunkSupplier.Default)) // z
            );

            _encryptedSpec = String("e:").Then(
                Brackets(Map(
                    (key, iv, spec) => new EncryptedChunkSupplier(key, iv, spec),
                    EncryptionKey.Before(COMMA),
                    EncryptionIV.Before(COMMA),
                    Rec(() => _encodingSpecification!)
                ))
            );

            _encodingSpecification = OneOf(
                _flatSpec.Cast<IChunkSupplier>(),
                _compressedSpec.Cast<IChunkSupplier>(),
                _encryptedSpec.Cast<IChunkSupplier>(),
                _blockSpec.Cast<IChunkSupplier>()
            );
        }

        private static readonly Parser<char, int> _blockSize;
        private static readonly Parser<char, (int Size, int Count)> _blockSizeSpec;
        private static readonly Parser<char, IEnumerable<(int Size, IChunkSupplier Spec)>> _blockSubchunk;

        private static readonly Parser<char, (int Size, int Count)> _finalSizeSpec;
        private static readonly Parser<char, IEnumerable<(int Size, IChunkSupplier Spec)>> _finalSubchunk;

        private static readonly Parser<char, IEnumerable<(int Size, IChunkSupplier Spec)>> _blockSpec;

        private static readonly Parser<char, CompressedChunkSupplier> _compressedSpec;
        private static readonly Parser<char, FlatChunkSupplier> _flatSpec = Char('n').Select(_ => default(FlatChunkSupplier));
        private static readonly Parser<char, EncryptedChunkSupplier> _encryptedSpec;

        private static readonly Parser<char, IChunkSupplier> _encodingSpecification;

        public static BLTE Parse(string specificationString)
        {
            _encodingSpecification.Parse(specificationString);
        }

        // TODO: This interface needs to return an enumerable.
        public interface IChunkSupplier;

        public record struct EncryptedChunkSupplier(string EncryptionKey, string EncryptionIV, IChunkSupplier Supplier) : IChunkSupplier;
        public record struct CompressedChunkSupplier(int Level, int Bits) : IChunkSupplier {
            public static CompressedChunkSupplier Default = new(9, 15);
        }
        public record struct FlatChunkSupplier : IChunkSupplier;
    }
}
