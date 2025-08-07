using System;
using System.Collections.Generic;
using System.Text;

namespace Blizztrack.Generators.Shared.Attributes
{
    /// <summary>
    /// Methods (and methods in classes) decorated with this attribute get any and all array subscript
    /// calls replaced by calls that unconditionally avoid bounds check at compile time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class UnsafeIndexersAttribute : Attribute { }
}
