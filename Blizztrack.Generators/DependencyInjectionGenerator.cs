using Blizztrack.Generators.Shared.Attributes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Threading;

namespace Blizztrack.Generators
{
    internal class DependencyInjectionGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var injectionPoint = context.SyntaxProvider.ForAttributeWithMetadataName(
                nameof(InjectionTargetAttribute),
                (node, token) => node is InvocationExpressionSyntax,
                TransformInjectionPoint
            );

            var services = context.SyntaxProvider.ForAttributeWithMetadataName(
                nameof())
        }

        private static int TransformInjectionPoint(GeneratorAttributeSyntaxContext context, CancellationToken stoppingToken)
        {
            return 0;
        }
    }
}
