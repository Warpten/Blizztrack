using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Shared.Extensions
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Returns an element in an array, bypassing bounds check automatically inserted by the JITter.
        /// 
        /// <para>
        /// This is semantically equivalent to <c>arr[index]</c> but prevents the JIT from emitting bounds checks.
        /// </para>
        /// 
        /// <para>
        /// Note that in return no guarantees are made and you should always make sure the <paramref name="index"/> is within bounds
        /// yourself.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr">The array to index.</param>
        /// <param name="index">The index of the element to return.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this T[] arr, int index)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), index);

        /// <summary>
        /// Unsafely promotes the boolean to an integer, bypassing normally emitted branching code.
        /// 
        /// <para>
        /// The result of this method can't be specified when truthy booleans are different than 1.
        /// </para>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int UnsafePromote(this bool value) {
            // Masking to a single bit is necessary everywhere to account for non-normalized inputs
            // (Unfortunately wastes cycles)

#pragma warning disable CS0162 // Unreachable code detected
            if (sizeof(bool) == sizeof(byte))
            {
                return Unsafe.As<bool, byte>(ref value) & 0b1;
            }
            else if (sizeof(bool) == sizeof(short))
            {
                return Unsafe.As<bool, short>(ref value) & 0b1;
            }
            else if (sizeof(bool) == sizeof(int))
            {
                return Unsafe.As<bool, int>(ref value) & 0b1;;
            }
            else
            {
                return value ? 1 : 0;
            }
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
}
