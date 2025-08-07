using Blizztrack.Generators.Metamodel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Collections.Generic;

namespace Blizztrack.Generators
{
    internal class InterceptableCall<M>(InterceptableLocation interceptableLocation, M methodSymbol, IEnumerable<ISymbol> callParameters)
    {
        /// <summary>
        /// The entire [InterceptsLocation(...)] attribute.
        /// </summary>
        public readonly InterceptableLocation Location = interceptableLocation;
        /// <summary>
        /// The method being called.
        /// </summary>
        public readonly M Target = methodSymbol;

        /// <summary>
        /// Arguments at the call site. The caller constructing this object is in charge of providing them, allowing consumers to save
        /// execution time in the case these are not needed.
        /// </summary>
        public readonly ISymbol[] Arguments = [.. callParameters];

        public InterceptableCall<T> To<T>(Func<M, T> converter) => new(Location, converter(Target), Arguments);
    }

    /// <summary>
    /// A interceptable call expression that also stores a regular <see cref="Location"/> to use to
    /// report diagnostics to the compiler.
    /// </summary>
    /// <typeparam name="M">The type of object describing the prototype of the method being called.</typeparam>
    /// <param name="call">A complete interceptable call site.</param>
    /// <param name="location">The location as requested by Roslyn analyzers.</param>
    internal class InterceptableLocatedCall<M>(InterceptableCall<M> call, Location location)
    {
        public readonly InterceptableCall<M> CallSite = call;
        public readonly Location Location = location;

        public InterceptableLocatedCall(InterceptableLocation interceptableLocation, Location location, M methodSymbol, IEnumerable<ISymbol> callParameters)
            : this(new(interceptableLocation, methodSymbol, callParameters), location)
        {

        }

        public InterceptableLocatedCall<T> To<T>(Func<M, T> converter) => new(CallSite.To(converter), Location);

        public Diagnostic ToDiagnostic(DiagnosticDescriptor rule, Func<M, string> targetTransform, params object[] args)
            => Diagnostic.Create(rule, Location, targetTransform(CallSite.Target), args);
    }

    internal static class InterceptedCall
    {
        public static InterceptableCall<Method> Of(InterceptableLocation location, IMethodSymbol symbol, IEnumerable<ISymbol> callParameters)
            => new(location, new(symbol), callParameters);
        public static InterceptableCall<IMethodSymbol> OfSymbol(InterceptableLocation location, IMethodSymbol symbol, IEnumerable<ISymbol> callParameters)
            => new(location, symbol, callParameters);

        /// <summary>
        /// Adds location information (to use for diagnostics) to an intercepted call.
        /// </summary>
        /// <typeparam name="M">The type of the object used to model the method being called.</typeparam>
        /// <param name="call">The interceptable call site.</param>
        /// <param name="location">The location of the call.</param>
        /// <returns></returns>
        public static InterceptableLocatedCall<M> WithLocation<M>(this InterceptableCall<M> call, Location location)
            => new(call.Location, location, call.Target, call.Arguments);
    }
}
