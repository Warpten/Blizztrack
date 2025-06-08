using Microsoft.CodeAnalysis.CSharp;

namespace Blizztrack.Generators.Metamodel
{
    public readonly struct InterceptedLocation(InterceptableLocation location)
    {
        public readonly InterceptableLocation Location = location;
        public string Target => Location.GetDisplayLocation();
    }
}
