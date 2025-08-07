using Blizztrack.Generators.Metamodel;
using Blizztrack.Generators.Shared.Attributes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Blizztrack.Generators
{
    internal class DependencyInjectionGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var injectionTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
               typeof(UnsafeIndexersAttribute).FullName,
               (node, _) => node is ClassDeclarationSyntax
                   or MethodDeclarationSyntax
                   or AccessorDeclarationSyntax
                   or ConstructorDeclarationSyntax,
               TransformIndexers);
        }

        private static IEnumerable<InterceptableLocation> TransformIndexers(GeneratorAttributeSyntaxContext context, CancellationToken stoppingToken)
        {
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
            return context.TargetSymbol switch
            {
                INamedTypeSymbol typeSymbol => TransformIndexers(typeSymbol, context.SemanticModel, stoppingToken),
                IMethodSymbol methodSymbol => TransformIndexers(methodSymbol, context.SemanticModel, stoppingToken),
            };
#pragma warning restore CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
        }

        private static IEnumerable<InterceptableLocation> TransformIndexers(INamedTypeSymbol classSymbol, SemanticModel model, CancellationToken stoppingToken)
        {
            foreach (var methodSymbol in classSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                foreach (var location in TransformIndexers(methodSymbol, model, stoppingToken))
                    yield return location;
            }
        }

        private static IEnumerable<InterceptableLocation> TransformIndexers(IMethodSymbol symbol, SemanticModel model, CancellationToken stoppingToken)
        {
            var elementAccessType = typeof(ElementAccessExpressionSyntax);
            var positionProperty = elementAccessType.GetProperty("Position", BindingFlags.NonPublic | BindingFlags.Instance);
            var locationProperty = elementAccessType.GetProperty("Location", BindingFlags.NonPublic | BindingFlags.Instance);

            var interceptableAttrType = Assembly.GetExecutingAssembly().GetType("Microsoft.CodeAnalysis.CSharp.InterceptableLocation1");
            var interceptableAttrConstructor = interceptableAttrType.GetConstructor(BindingFlags.NonPublic, null, 
                [typeof(ImmutableArray<byte>), typeof(string), positionProperty.PropertyType, typeof(int), typeof(int)],
                []);

            foreach (var syntaxNodeReference in symbol.DeclaringSyntaxReferences)
            {
                var syntaxNode = syntaxNodeReference.GetSyntax(stoppingToken) as MethodDeclarationSyntax;
                if (syntaxNode?.Body is null)
                    continue;

                foreach (var elementAccess in syntaxNode.Body.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
                {
                    // Imaginary syntax, this is unfortunately not possible (ElementAccessExpression is not an InvocationExpression)
                    // var interceptLocation = model.GetInterceptableLocation(elementAccess, stoppingToken);

                    // Dragons -- Dragons -- Fuckery -- But mostly dragons.
                    // https://source.dot.net/#Microsoft.CodeAnalysis.CSharp/Compilation/CSharpSemanticModel.cs,5225

                    var tree = elementAccess.SyntaxTree;
                    var text = tree.GetText(stoppingToken);
                    var path = tree.FilePath;
                    var checksum = text.GetContentHash();

                    var lineSpan = (locationProperty.GetValue(elementAccess) as Location).GetLineSpan().Span.Start;
                    var lineNumberOneIndexed = lineSpan.Line + 1;
                    var characterNumberOneIndexed = lineSpan.Character + 1;

                    var resolver = model.Compilation.Options.SourceReferenceResolver;

                    var interceptableLocation = interceptableAttrConstructor.Invoke([
                        checksum,
                        path,
                        resolver,
                        positionProperty.GetValue(elementAccess),
                        lineNumberOneIndexed,
                        characterNumberOneIndexed
                    ]) as InterceptableLocation;
                }
            }

            return 0;
        }
    }
}
