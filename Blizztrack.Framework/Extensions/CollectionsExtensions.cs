using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.Extensions
{
    internal static class CollectionsExtensions
    {
        /// <summary>
        /// Obtains a reference to the array backing up this <see cref="List{T}" />'s storage.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="container"></param>
        /// <returns>A reference to the array backing up this list.</returns>
        /// <remarks>Consider if <see cref="CollectionsMarshal.AsSpan{T}(List{T}?)" /> would not be better suited to your needs before using this.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T[] AsBackingArray<T>(this List<T> container)
            => ref UnsafeAccessors<T>.AsBackingArray(container);
    }
}
