using Pidgin;

using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.Extensions
{
    internal static class UnsafeAccessors<T>
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_items")]
        public extern static ref T[] AsBackingArray(List<T> list);
    }

    public static class UnsafeAccessors
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_position")]
        private extern static ref long GetLocation<TToken>(ref ParseState<TToken> state);

        public static void Seek<TToken>(this ref ParseState<TToken> state, long position)
            => GetLocation(ref state) = position;
    }
}
