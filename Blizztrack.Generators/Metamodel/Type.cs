using Blizztrack.Generators.Extensions;

using Microsoft.CodeAnalysis;

namespace Blizztrack.Generators.Metamodel
{
    public readonly struct Type(string @namespace, string type)
    {
        public Type(ITypeSymbol type) : this(type.GetFullyQualifiedContainingName(), type.GetName()) { }

        public readonly string Namespace = @namespace;
        public readonly string Name = type;

        public readonly string FullyQualifiedName => $"{Namespace}.{Name}";
    }
}
