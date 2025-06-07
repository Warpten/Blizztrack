using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.Extensions
{
    internal static class UnsafeAccessors<T>
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_items")]
        public extern static ref T[] AsBackingArray(List<T> list);
    }
}
