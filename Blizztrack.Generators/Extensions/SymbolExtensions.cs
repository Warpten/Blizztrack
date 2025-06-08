using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Blizztrack.Generators.Extensions
{
    internal static class SymbolExtensions
    {
        private static readonly SymbolDisplayFormat NamespaceFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.None,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public static string GetFullyQualifiedContainingName(this INamespaceOrTypeSymbol symbol)
            => symbol.ContainingSymbol is INamespaceSymbol
                ? symbol.ContainingSymbol.ToDisplayString(NamespaceFormat)
                : symbol.ContainingSymbol.ToDisplayString(FullyQualifiedTypeFormat);

        public static string GetFullyQualifiedName(this INamespaceOrTypeSymbol symbol)
            => symbol is INamespaceSymbol
                ? symbol.ToDisplayString(NamespaceFormat)
                : symbol.ToDisplayString(FullyQualifiedTypeFormat);

        public static string GetName(this ITypeSymbol type)
            => type.ToDisplayString(TypeFormat);

        public static AttributeData FindAttribute(this ISymbol symbol, Func<AttributeData, bool> predicate)
        {
            foreach (var attr in symbol.GetAttributes())
                if (predicate(attr))
                    return attr;

            return null;
        }

        public static AttributeData FindAttribute<T>(this ISymbol symbol) where T : Attribute
            => FindAttribute(symbol, attributeData => attributeData.AttributeClass?.GetFullyQualifiedName() == typeof(T).FullName);

        public static IEnumerable<AttributeData> SelectAttributes(this INamedTypeSymbol symbol, Func<AttributeData, bool> predicate)
        {
            var itr = symbol;
            while (itr != null)
            {
                foreach (var attr in itr.GetAttributes())
                    if (predicate(attr))
                        yield return attr;

                foreach (var implementedInterface in itr.AllInterfaces)
                    foreach (var attr in implementedInterface.GetAttributes())
                        if (predicate(attr))
                            yield return attr;

                itr = itr.BaseType;
            }
        }

        public static IEnumerable<AttributeData> SelectAttributes<T>(this INamedTypeSymbol symbol) where T : Attribute
            => SelectAttributes(symbol, attributeData => attributeData.AttributeClass?.GetFullyQualifiedName() == typeof(T).FullName);

        public static bool IsAssignableFrom(this ITypeSymbol symbol, ITypeSymbol other)
        {
            // Check through all base types
            var baseType = other;
            while (baseType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(symbol, other))
                    return true;

                baseType = baseType.BaseType;
            }

            // Base type didn't match, try on interfaces
            foreach (var implementedInterface in other.AllInterfaces)
                if (symbol.IsAssignableFrom(implementedInterface))
                    return true;

            return false;
        }

        public static IFieldSymbol GetBackingField(this IPropertySymbol property)
            => property.ContainingType.GetMembers().Where(x => x.Kind == SymbolKind.Field)
                .Cast<IFieldSymbol>()
                .Where(x => SymbolEqualityComparer.Default.Equals(x.AssociatedSymbol, property))
                .FirstOrDefault();
    }
}
