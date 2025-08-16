using Pidgin;
using static Pidgin.Parser;

using static Blizztrack.Framework.TACT.Implementation.Spec;

namespace Blizztrack.Framework.Tests
{
#if BLIZZTRACK_CAN_SEE_INTERNALS
    [Trait("Category", "BLTE specification string parsers")]
    public class SpecificationParserTests
    {
        [Fact(DisplayName = "Real samples")]
        public void RealSamples()
        {
            EnsureRoundTrip(_encodingSpecification, "b:{1020=z,256K*5=z,172394=z,72=e:{9E45CED8F6D15907,0337f73e,z},192=e:{FD074938B998C71F,0337f73e,z},72=e:{8FEA9D1AA7184722,0337f73e,z},112=e:{C0EDF412145D322E,0337f73e,z},112=e:{086528C2B6C89F40,0337f73e,z},52=e:{502B4885DFFA5541,0337f73e,z},92=e:{2DCD90B391A7D096,0337f73e,z},52=e:{C0C7850C82C019A1,0337f73e,z},92=e:{365D56FF1E5DAABA,0337f73e,z},32=e:{891F16BCC41DF6BD,0337f73e,z},152=e:{F1E0AE9AF97882DB,0337f73e,z},72=e:{514BD1B24E68B9F4,0337f73e,z}}");
            EnsureRoundTrip(_encodingSpecification, "b:{1588=z,16K*235=z,16104=z,256K*3=z,196844=z,864=e:{05519C232A3B8903,8eb7bb21,z},218=e:{05519C232A3B8903,8eb7bb21,z},58=e:{9E45CED8F6D15907,8eb7bb21,z},36=e:{9E45CED8F6D15907,8eb7bb21,z},352=e:{FD074938B998C71F,8eb7bb21,z},28=e:{FD074938B998C71F,8eb7bb21,z},192=e:{8FEA9D1AA7184722,8eb7bb21,z},36=e:{8FEA9D1AA7184722,8eb7bb21,z},416=e:{C3BE36A01C154827,8eb7bb21,z},28=e:{C3BE36A01C154827,8eb7bb21,z},200=e:{6313BF3D1AE99927,8eb7bb21,z},28=e:{6313BF3D1AE99927,8eb7bb21,z},52=e:{8E0FA9E388892E2D,8eb7bb21,z},14=e:{8E0FA9E388892E2D,8eb7bb21,z},106=e:{C0EDF412145D322E,8eb7bb21,z},28=e:{C0EDF412145D322E,8eb7bb21,z},204=e:{37B45EA9F224943D,8eb7bb21,z},14=e:{37B45EA9F224943D,8eb7bb21,z},362=e:{086528C2B6C89F40,8eb7bb21,z},364=e:{086528C2B6C89F40,8eb7bb21,z},44=e:{502B4885DFFA5541,8eb7bb21,z},14=e:{502B4885DFFA5541,8eb7bb21,z},58=e:{4BF2C63738D1F95B,8eb7bb21,z},14=e:{4BF2C63738D1F95B,8eb7bb21,z},402=e:{2DCD90B391A7D096,8eb7bb21,z},28=e:{2DCD90B391A7D096,8eb7bb21,z},188=e:{C0C7850C82C019A1,8eb7bb21,z},28=e:{C0C7850C82C019A1,8eb7bb21,z},208=e:{3F2C09089DA72FBA,8eb7bb21,z},14=e:{3F2C09089DA72FBA,8eb7bb21,z},206=e:{365D56FF1E5DAABA,8eb7bb21,z},28=e:{365D56FF1E5DAABA,8eb7bb21,z},44=e:{891F16BCC41DF6BD,8eb7bb21,z},14=e:{891F16BCC41DF6BD,8eb7bb21,z},462=e:{39989F9838E440D4,8eb7bb21,z},192=e:{39989F9838E440D4,8eb7bb21,z},212=e:{F1E0AE9AF97882DB,8eb7bb21,z},28=e:{F1E0AE9AF97882DB,8eb7bb21,z}}");
            EnsureRoundTrip(_encodingSpecification, "b:{2852=z,188534=z,42=e:{365D56FF1E5DAABA,0b18ef8e,z},42=e:{39989F9838E440D4,0b18ef8e,z}}");
        }

        [Fact(DisplayName ="block-spec")]
        public void BlockSpec()
        {
            EnsureRoundTrip(_blockSpec, "b:{256K=n}");
            EnsureRoundTrip(_blockSpec, "b:{256K*=n}");
            EnsureRoundTrip(_blockSpec, "b:{164=z,16*=z}");
            EnsureFails(_blockSpec, "b:{164=z,16*2=z}");
            EnsureRoundTrip(_blockSpec, "b:{123=z,45K*5=z,7=z,89=z}");
        }

        [Fact(DisplayName = "encrypted-spec")]
        public void EncryptedSpec()
        {
            // Check the encryption key length and character types
            EnsureRoundTrip(_encryptedSpec, "e:{0123456789ABCDEF,06FC152E,z}");
            EnsureFails(_encryptedSpec,     "e:{GGGGGGGGGGGGGGGG,06FC152E,z}");
            EnsureFails(_encryptedSpec,     "e:{0123456789ABCDEFG,06FC152E,z}");
            EnsureFails(_encryptedSpec,     "e:{0123456789ABCDE,06FC152E,z}");

            EnsureFails(_encryptedSpec, "e:{237DA26C65073F42,06FC152E}");
            EnsureFails(_encryptedSpec, "e:{06FC152E,z}");
            EnsureRoundTrip(_encryptedSpec, "e:{0123456789ABCDEF,01234567,e:{0123456789ABCDEF,01234567,z}}");
        }

        [Fact(DisplayName = "compression-spec")]
        public void CompressionSpec()
        {
            EnsureRoundTrip(_compressedSpec, "z");
            EnsureRoundTrip(_compressedSpec, "z:{9,5}");
            EnsureRoundTrip(_compressedSpec, "z:5");
        }

        [Fact(DisplayName = "block-subchunk")]
        public void BlockSubChunk()
        {
            { // Preamble: test block-size-spec
                var (size, count) = Parse(_blockSizeSpec, "164");
                Assert.Equal(164, size);
                Assert.Equal(1, count);

                EnsureFails(_blockSizeSpec, "164K*");
                EnsureParses(_blockSizeSpec, "164K*5");
            }

            EnsureRoundTrip(_blockSubchunk, "164=z");
            EnsureParses(_blockSubchunk.Before(Char(',')), "164=z,");
            EnsureRoundTrip(_blockSubchunk, "164K=n");
            EnsureRoundTrip(_blockSubchunk, "164M=n");
            EnsureRoundTrip(_blockSubchunk, "164*5=z");
            EnsureFails(_blockSubchunk, "164*=z");

            { // Check that this correctly parses
                var chunkSpec = Parse(_blockSubchunk, "64=z");

                Assert.Equal(1, chunkSpec.Count);
                Assert.Equal(64, chunkSpec.Size);
                var compressionSpec = Assert.IsType<CompressionSpec>(chunkSpec.Spec);
                Assert.Equal(9, compressionSpec.Level);
                Assert.Equal(15, compressionSpec.Window);
            }
        }

        [Fact(DisplayName = "final-subchunk")]
        public void FinalSubChunk()
        {
            { // Preamble: test final-size-spec
                var (size, count) = Parse(_finalSizeSpec, "164");
                Assert.Equal(164, size);
                Assert.Equal(1, count);

                (size, count) = Parse(_finalSizeSpec, "164K*");
                Assert.Equal(164 * 1024, size);
                Assert.Equal(REPEATING_UNTIL_END, count);

                EnsureParses(_finalSizeSpec, "164K*");
            }
            
            { // Now the actual chunks
                var chunkSpec = EnsureRoundTrip(_finalSubchunk, "164K*=z");
                Assert.Equal(164 * 1024, chunkSpec.Size);
                Assert.Equal(REPEATING_UNTIL_END, chunkSpec.Count);
                Assert.IsType<CompressionSpec>(chunkSpec.Spec);
            }
        }

        internal static T Parse<T>(Parser<char, T> parser, string specificationString)
        {
            var chunkSpec = parser.Parse(specificationString);
            if (!chunkSpec.Success)
                throw new InvalidOperationException($"Malformed specification string: {chunkSpec.Error}");

            return chunkSpec.Value;
        }

        internal static T EnsureRoundTrip<T>(Parser<char, T> parser, string specificationString)
        {
            var chunkSpec = parser.Parse(specificationString);
            if (!chunkSpec.Success)
                throw new InvalidOperationException($"Malformed specification string: {chunkSpec.Error}");

            Assert.Equal(specificationString, chunkSpec.Value!.ToString());
            return chunkSpec.Value;
        }

        internal static void EnsureFails<T>(Parser<char, T> parser, string specificationString)
        {
            var chunkSpec = parser.Parse(specificationString);
            Assert.False(chunkSpec.Success);
        }

        internal static void EnsureParses<T>(Parser<char, T> parser, string specificationString)
        {
            var chunkSpec = parser.Parse(specificationString);
            Assert.True(chunkSpec.Success);
        }
    }
#endif
}
