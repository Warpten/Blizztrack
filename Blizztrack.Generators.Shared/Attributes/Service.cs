using System;
using System.Collections.Generic;
using System.Text;

namespace Blizztrack.Generators.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    internal class Service(ServiceType serviceKind, string? key = null) : Attribute
    {
        public readonly ServiceType Kind = serviceKind;
        public readonly string? Key = key;
    }

    public enum ServiceType
    {
        Singleton,
        Hosted,
        Scoped
    }
}
