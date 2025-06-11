using Blizztrack.Framework.TACT.Implementation;

namespace Blizztrack.Framework.Tests
{
    public class SpecificationParserTests
    {
        [Fact]
        public void TestExpression()
        {
            ensureRoundTrip("b:{256K=n}");
            ensureRoundTrip("b:{256K*=n}");
            ensureRoundTrip("b:{256K*50=n}");
            ensureRoundTrip("b:{16K*4=z:{9,15}}");
            ensureTransforms("b:{16K*=z}", "b:{16K*=z:{9,15}}");
            ensureTransforms("b:{16K*=z:{6,mpq}}", "b:{16K*=z:{6,-1}}");

            static void ensureRoundTrip(string spec) => Assert.Equal(spec, spec.ToSchema().ToString());
            static void ensureTransforms(string spec, string expected) => Assert.Equal(expected, spec.ToSchema().ToString());
        }
    }
}
