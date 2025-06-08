using System;
using System.Collections.Generic;
using System.Text;

namespace Blizztrack.Generators.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class InjectionTargetAttribute : Attribute
    {
    }
}
