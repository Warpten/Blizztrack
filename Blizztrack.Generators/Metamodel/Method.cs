using Microsoft.CodeAnalysis;

namespace Blizztrack.Generators.Metamodel
{
    public readonly struct Method(IMethodSymbol method)
    {
        public readonly Type ReturnType = new(method.ReturnType);
        public readonly string Name = method.Name;
        public readonly Parameter[] Parameters = [.. method.Parameters.Select(p => new Parameter(p))];

        public readonly Type? ReceiverType = method.ReceiverType == null ? null : new(method.ReceiverType);

        public readonly bool IsAsync = method.IsAsync;
        public readonly Accessibility Accessibility = method.DeclaredAccessibility;
    }
}
