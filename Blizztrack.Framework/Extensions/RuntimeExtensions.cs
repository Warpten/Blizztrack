using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.Extensions
{
    /// <summary>
    /// Evil methods that definitely should not be extensions. These are opt-in, because you need to know:
    /// <list type="number">
    /// <item><description>The layout of the object you're calling this on.</description></item>
    /// <item><description>How the CLR internally represents objects.</description></item>
    /// <item><description>... What the fuck are you doing?  Stop reading this file.</description></item>
    /// </list>
    /// No, but seriously, avoid using these at all costs. All bets are off and invalid use will blow up at runtime. Or even worse, it will
    /// appear to be working but mutate another object entirely! Or the GC will be moving the original object while you're fucking around
    /// with one of its fields!
    /// Consider using <see cref="UnsafeAccessorAttribute"/> instead!
    /// </summary>
    internal static class RuntimeExtensions
    {
        /// <summary>
        /// Obtains a managed pointer to a member of this value type as identified by its offset.
        /// </summary>
        /// <typeparam name="U">The type of the member to acquire.</typeparam>
        /// <typeparam name="T">The reference type that is holding a member.</typeparam>
        /// <param name="object">The object to read from.</param>
        /// <param name="memberOffset">The offset, in bytes, at which the member is located.</param>
        /// <returns>A managed pointer to the member of type <typeparamref name="U" /> living at <paramref name="memberOffset" /> within <typeparamref name="T"/>.</returns>
        [Experimental(diagnosticId: "BT666"), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref U UnsafeAcquireField<U, T>(scoped ref T @object, int memberOffset) where T : struct
        {
            var rawPointer = *(nint*)Unsafe.AsPointer(ref @object);
            return ref Unsafe.AsRef<U>((byte*)rawPointer + memberOffset);
        }

        /// <summary>
        /// Obtains a managed pointer to a member of this reference type as identified by its offset.
        /// </summary>
        /// <typeparam name="U">The type of the member to acquire.</typeparam>
        /// <typeparam name="T">The reference type that is holding a member.</typeparam>
        /// <param name="object">The object to read from.</param>
        /// <param name="memberOffset">The offset, in bytes, at which the member is located.</param>
        /// <returns>A managed pointer to the member of type <typeparamref name="U" /> living at <paramref name="memberOffset" /> within <typeparamref name="T"/>.</returns>
        [Experimental(diagnosticId: "BT666"), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref U UnsafeAcquireField<U, T>(T @object, int memberOffset) where T : class
        {
            var rawPointer = *(nint*)Unsafe.AsPointer(ref @object);
            return ref Unsafe.AsRef<U>((byte*)rawPointer + sizeof(nint) + memberOffset);
        }
    }
}
