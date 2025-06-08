using Microsoft.CodeAnalysis;

namespace Blizztrack.Generators.Extensions
{
    public static class AttributeDataExtensions
    {
        public static TypedConstant? FindNamedArgument(this AttributeData data, string attributeName)
        {
            foreach (var namedArgument in data.NamedArguments)
                if (namedArgument.Key == attributeName)
                    return namedArgument.Value;

            return null;
        }
    }
}
