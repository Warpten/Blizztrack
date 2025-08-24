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
                throw new InvalidOperationException($"Malformed specification string: {chunkSpec.Error}");

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

        // block-size  =  number [ 'K' | 'M' ]
        internal static readonly Parser<char, int> _blockSize =
            Map((size, unit) => size * unit,
                NUMBER,
                OneOf(
                    Char('M').Select(_ => 1024 * 1024),
                    Char('K').Select(_ => 1024),
                    Return(1)
                )
            ).Labelled("block-size");


        // block-size-spec =  block-size [ '*' block-count ]
        internal static readonly Parser<char, (int Size, int Count)> _blockSizeSpec =
            Map((blockSize, blockCount) => (blockSize, blockCount),
                _blockSize,
                // If block-size is omitted, 1 is included.
                STAR.Then(NUMBER).Select(Maybe.Just).Or(Return(Maybe.Just(1))).Select(x => x.Value!)
            ).Labelled("block-size-spec");

        // block-subchunk  =  block-size-spec '=' e-spec ;
        internal static readonly Parser<char, SizeAwareChunkSpec> _blockSubchunk = Map(
            (size, spec) => new SizeAwareChunkSpec(size.Size, size.Count, spec),
            _blockSizeSpec.Before(EQUALS),
            Rec(() => _encodingSpecification!)
        ).Labelled("block-subchunk");

        internal static readonly Parser<char, (int Size, int Count)> _finalSizeSpec = OneOf(
            STAR.Select(_ => (Size: GREEDY, Count: 0)),
            Try(_blockSize.Before(STAR)).Select(size => (Size: size, Count: REPEATING_UNTIL_END)),
            _blockSizeSpec
        ).Labelled("final-size-spec");

        // final-size-spec =  block-size-spec | block-size '*' | '*'
        //                 =  block-size [ '*' block-count ] | block-size '*' | '*'
        //                 =  block-size ( [ '*' block-count ] | '*' ) | '*'
        // internal static readonly Parser<char, (int Size, int Count)> _finalSizeSpec = OneOf(
        //     STAR.Select(_ => (BlockSize: GREEDY, BlockCount: 0)),
        //     Map(
        //         (size, count) => (size, count),
        //         _blockSize,
        //         STAR.Then(NUMBER.Or(Return(REPEATING_UNTIL_END)).Labelled("[ '*' block-count ] | '*'"))
        //     )
        //     /*Map(
        //         (size, count) => (BlockSize: size, BlockCount: count),
        // 
        //         _blockSize.Before(STAR),
        //         Try(NUMBER).Or(Return(REPEATING_UNTIL_END))
        //     )*/
        // ).Labelled("final-size-spec");

        // final-subchunk  =  final-size-spec '=' e-spec ;
        internal static readonly Parser<char, SizeAwareChunkSpec> _finalSubchunk = Map(
            (size, spec) => new SizeAwareChunkSpec(size.Size, size.Count, spec),
            _finalSizeSpec.Before(EQUALS),
            Rec(() => _encodingSpecification!)
        ).Labelled("final-subchunk");


        // ( 'b' ':' ( final-subchunk | '{' ( [{block-subchunk ','}] final-subchunk ) '}' ) )
        internal static readonly Parser<char, AggregatedChunkSpec> _blockSpec =
            String("b:").Then(OneOf(
                Try(_finalSubchunk).Select(c => new AggregatedChunkSpec(c)),
                Brackets(
                    new RepeatUntil<char, SizeAwareChunkSpec>(_blockSubchunk, _finalSubchunk, COMMA)
                        .Select(t => new AggregatedChunkSpec([.. t]))
                )
            )).Labelled("block-spec");

        // ( 'z' [ ':' ( zip-level | '{' zip-level ',' zip-bits '}' ) ] )
        internal static readonly Parser<char, CompressionSpec> _compressedSpec = Char('z').Then(
            OneOf(
                COLON.Then(OneOf(
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
                )),
                Return(new CompressionSpec(9, 15))
            )
        ).Labelled("z-spec");


        // ( 'e' ':' '{' encryption-key ',' encryption-iv ',' e-spec '}' )
        internal static readonly Parser<char, EncryptedChunkSpec> _encryptedSpec = String("e:").Then(
            Brackets(Map(
                (key, iv, spec) => new EncryptedChunkSpec(key, iv, spec),
                EncryptionKey.Before(COMMA),
                EncryptionIV.Before(COMMA),
                Rec(() => _encodingSpecification!)
            ))
        ).Labelled("e-spec");

        // Overall rule.
        internal static readonly Parser<char, IChunkSpec> _encodingSpecification = OneOf(
            Char('n').Select(_ => (IChunkSpec)default(FlatChunkSpec)),
            Try(_compressedSpec.Cast<IChunkSpec>()),
            Try(_encryptedSpec.Cast<IChunkSpec>()),
            Try(_blockSpec.Cast<IChunkSpec>())
        // ( 'c' ':' '{' bcpack-bcn '}' )
        // ( 'g' ':' '{' gdeflate-compression-level '}' )´
        ).Labelled("root-spec");

        /// <summary>
        /// Produces a parser than is encapsulated by curly brackets.
        /// </summary>
        /// <typeparam name="T">The type of value returned by the parser</typeparam>
        /// <param name="parser">The parser to wrap.</param>
        /// <returns>A new parser.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Parser<char, T> Brackets<T>(Parser<char, T> parser) => Map((l, m, r) => m, LBRACE, parser, RBRACE);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RepeatUntil<TToken, T> RepeatUntil<TToken, T>(Parser<TToken, T> element, Parser<TToken, T> tail, Parser<TToken, TToken> delimiter)
            => new(element, tail, delimiter);

        public const int REPEATING_UNTIL_END = -2;
        public const int GREEDY = -1;
        public const int COMPRESSION_BITS_MPQ = -1;
        public const int COMPRESSION_BITS_ZLIB = -2;
        public const int COMPRESSION_BITS_LZ4HC = -3;

        public interface IChunkSpec
        {
            public void Accept<T>(ref T visitor, int compressedSize) where T : IVisitor, allows ref struct
                => Accept(ref visitor, new TraversalContext()
                {
                    Size = compressedSize
                });

            void Accept<T>(ref T visitor, TraversalContext context) where T : IVisitor, allows ref struct;
        }

        //< Describes a chunk of unknown size that is just the decompressed data.
        internal readonly record struct FlatChunkSpec : IChunkSpec
        {
            readonly void IChunkSpec.Accept<T>(ref T visitor, TraversalContext context) where T : default
                => visitor.OnRawChunk(context.Size);

            public override readonly string ToString() => "n";
        }

        //< Describes a chunk of unknown size that is compressed using ZLib with the provided parameters.
        internal readonly record struct CompressionSpec(int Level = 9, int Window = 15) : IChunkSpec
        {
            readonly void IChunkSpec.Accept<T>(ref T visitor, TraversalContext context) where T : default
                => visitor.OnCompressedChunk(Level, Window, context.Size);

            public override readonly string ToString()
            {
                if (Level == 9 && Window == 15)
                    return "z";

                return Window switch
                {
                    COMPRESSION_BITS_MPQ => $"z:{{{Level},mpq}}",
                    COMPRESSION_BITS_LZ4HC => $"z:{{{Level},lz4hc}}",
                    COMPRESSION_BITS_ZLIB => $"z:{{{Level},zlib}}",
                    15 => $"z:{Level}",
                    _ => $"z:{{{Level},{Window}}}",
                };
            }
        }

        //< Describes a chunk of unknown size that is encrypted with the given parameters. The decrypted data is further compressed
        //  by the given specification.
        internal readonly record struct EncryptedChunkSpec(string EncryptionKey, string EncryptionIV, IChunkSpec Spec) : IChunkSpec
        {
            readonly void IChunkSpec.Accept<T>(ref T visitor, TraversalContext context) where T : default
            {
                // Push the encryption state on the stack
                visitor.BeginEncryption(EncryptionKey, EncryptionIV);
                Spec.Accept(ref visitor, context);
                visitor.EndEncryption();
            }

            public override readonly string ToString()
                => $"e:{{{EncryptionKey},{EncryptionIV},{Spec}}}";
        }

        //< Applies the given size to a specific chunk
        internal readonly record struct SizeAwareChunkSpec(int Size, int Count, IChunkSpec Spec) : IChunkSpec
        {
            readonly void IChunkSpec.Accept<T>(ref T visitor, TraversalContext context) where T : default
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(context.Size, 0, nameof(context.Size));

                if (Count == REPEATING_UNTIL_END)
                {
                    var chunkCount = context.Size / Size;
                    var finalChunkSize = context.Size % Size;

                    // Update context with the size of every chunk.
                    context.Size = Size;
                    for (var i = 0; i < chunkCount; ++i)
                        Spec.Accept(ref visitor, context);

                    // And finally the size of the final chunk.
                    context.Size = finalChunkSize;
                    if (finalChunkSize != 0)
                        Spec.Accept(ref visitor, context);

                    // Done consuming.
                    context.Size = 0;
                }
                else if (Size == GREEDY)
                {
                    // Consume all.
                    Spec.Accept(ref visitor, context);
                    context.Size = 0;
                }
                else
                {
                    // A repeating chunk of this.Size bytes, repeating Count times.
                    for (var i = 0; i < Count; ++i)
                        Spec.Accept(ref visitor, context);
                }
            }

            public override readonly string ToString()
            {
                if (Size == GREEDY)
                    return $"*={Spec}";

                var sizeString = Size switch
                {
                    > 1024 => format(Size),
                    _ => $"{Size}"
                };

                if (Count == REPEATING_UNTIL_END)
                    return $"{sizeString}*={Spec}";

                if (Count == 1)
                    return $"{sizeString}={Spec}";

                return $"{sizeString}*{Count}={Spec}";

                static string format(int size)
                {
                    if ((size % (1024 * 1024)) == 0)
                        return $"{size / (1024 * 1024)}M";

                    if ((size % 1024) == 0)
                        return $"{size / 1024}K";

                    return $"{size}";
                }
            }
        }

        internal readonly struct AggregatedChunkSpec(params ReadOnlySpan<IChunkSpec> specs) : IChunkSpec
        {
            private readonly IChunkSpec[] _specs = [.. specs];

            readonly void IChunkSpec.Accept<T>(ref T visitor, TraversalContext context) where T : default
            {
                for (var i = 0; i < _specs.Length; ++i)
                    _specs[i].Accept(ref visitor, context);
            }

            public override string ToString()
                => $"b:{{{string.Join(',', _specs.Select(s => s.ToString()))}}}";
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

    internal class RepeatUntil<TToken, T>(Parser<TToken, T> element, Parser<TToken, T> tail, Parser<TToken, TToken> delimiter)
        : Parser<TToken, IEnumerable<T>>
    {
        public override bool TryParse(ref ParseState<TToken> state, ref PooledList<Expected<TToken>> expecteds, [MaybeNullWhen(false)] out IEnumerable<T> result)
        {
            List<T> elements = [];
            while (state.HasCurrent)
            {
                var bookmark = state.Bookmark();

                var hasElement = element.TryParse(ref state, ref expecteds, out var elemResult);
                var hasDelimiter = hasElement
                    ? delimiter.TryParse(ref state, ref expecteds, out _)
                    : false;

                if (!hasDelimiter)
                {
                    state.Rewind(bookmark);
                    expecteds.Clear();

                    var hasTail = tail.TryParse(ref state, ref expecteds, out var tailElement);
                    if (hasTail)
                    {
                        elements.Add(tailElement!);
                        break;
                    }
                    else
                    {
                        result = [];
                        return false;
                    }
                }
                else
                {
                    elements.Add(elemResult!);
                }
            }

            result = elements;
            return true;
        }
    }
}
