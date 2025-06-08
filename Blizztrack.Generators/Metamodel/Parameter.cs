using Microsoft.CodeAnalysis;

namespace Blizztrack.Generators.Metamodel
{
    public readonly struct Parameter(Type type, string name)
    {
        public Parameter(IParameterSymbol parameter) : this(new(parameter.Type), parameter.Name) { }

        public readonly Type Type = type;
        public readonly string Name = name;
    }
}
