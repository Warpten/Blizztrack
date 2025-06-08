using Microsoft.CodeAnalysis;

namespace Blizztrack.Generators.Metamodel
{
    public readonly struct Property(Type type, string name)
    {
        public Property(IPropertySymbol property) : this(new(property.Type), property.Name) { }

        public readonly string Name = name;
        public readonly Type Type = type;
    }
}
