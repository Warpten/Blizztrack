using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Wasm;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Blizztrack.Shared.Extensions;

namespace Blizztrack.Framework.TACT
{
    /// <summary>
    /// Base interface of all key-like types.
    /// </summary>
    public interface IKey
    {
        public ReadOnlySpan<byte> AsSpan();
        public string AsHexString();

        public bool SequenceEqual<U>(U other) where U : notnull, IKey, allows ref struct;

        public byte this[int index] { get; }
        public int Length { get; }
    }

    public record struct SizeAware<T>(T Key, long Size)
        where T : IKey<T>
    {
        public static implicit operator T(SizeAware<T> self) => self.Key;
    }

    public record struct SizedKeyPair<T, U>(SizeAware<T> Content, SizeAware<U> Encoding)
        where T : IContentKey<T>
        where U : IEncodingKey<U>;

    public record struct KeyPair<T, U>(T Content, U Encoding)
        where T : IContentKey<T>
        where U : IEncodingKey<U>;

    /// <summary>
    /// Typed equivalent of <see cref="IKey"/> that also requires the implementation to be <see cref="IEquatable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The concrete implementation type.</typeparam>
    public interface IKey<T> : IKey, IEquatable<T> where T : notnull, IKey<T>, allows ref struct
    {
        /// <summary>
        /// TODO: Should this be public?
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static abstract T From(ReadOnlySpan<byte> data);

        public static abstract bool operator ==(T left, T right);
        public static abstract bool operator !=(T left, T right);
    }

    /// <summary>
    /// A key that is actually a view over a sequence of bytes. Storage for this key is not owned.
    /// <para>
    /// Implementations should be ref-like types.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The non-owning concrete implementation.</typeparam>
    /// <typeparam name="U">An owning equivalent to <typeparamref name="T"/>.</typeparam>
    public interface IKeyView<T, U> : IKey<T> where T : notnull, IKeyView<T, U>, allows ref struct
    {
        /// <summary>
        /// Takes ownership of the data this key references and returns an owning instance of a key type.
        /// </summary>
        /// <returns></returns>
        public U AsOwned();
    }

    /// <summary>
    /// A key whose storage for bytes is an array of owned memory.
    /// </summary>
    /// <typeparam name="T">The concrete type of the key.</typeparam>
    public interface IOwnedKey<T> : IKey<T> where T : notnull, IOwnedKey<T>
    {
        /// <summary>
        /// An empty value for this type.
        /// </summary>
        public static abstract T Zero { get; }

        /// <summary>
        /// Constructs an array of keys from an ASCII hex string, split by the given <paramref name="delimiter"/>.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="delimiter">The delimiting character.</param>
        /// <returns></returns>
        internal static virtual T[] FromString(ReadOnlySpan<byte> str, byte delimiter)
        {
            var sections = str.Split(delimiter, true);
            if (sections.Length == 0)
                return [];

            var dest = GC.AllocateUninitializedArray<T>(sections.Length);
            var i = 0;
            foreach (var section in sections)
                dest[i] = T.FromString(str[section]);
            return dest;
        }

        /// <summary>
        /// Constructs a key from an ASCII hex string.
        /// </summary>
        /// <param name="sourceChars"></param>
        /// <returns></returns>
        internal virtual static T FromString(ReadOnlySpan<char> sourceChars)
        {
            if (sourceChars.IsEmpty)
                return T.Zero;

            try
            {
                return T.From(Convert.FromHexString(sourceChars));
            }
            catch (FormatException)
            {
                return T.Zero;
            }
        }

        /// <summary>
        /// Constructs a new instance of the key type from the given hex string.
        /// </summary>
        /// <param name="sourceChars"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        internal virtual static T FromString(ReadOnlySpan<byte> sourceChars)
        {
            if (sourceChars.IsEmpty)
                return T.Zero;

            Span<byte> workBuffer = stackalloc byte[sourceChars.Length / 2];

            ref byte srcRef = ref MemoryMarshal.GetReference(sourceChars);
            ref byte dstRef = ref MemoryMarshal.GetReference(workBuffer);

            nuint offset = 0;
            if (BitConverter.IsLittleEndian
                && (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported || PackedSimd.IsSupported)
                && sourceChars.Length >= Vector128<byte>.Count)
            {
                // Author: Geoff Langdale, http://branchfree.org
                // https://github.com/WojciechMula/toys/blob/master/simd-parse-hex/geoff_algorithm.cpp#L15
                // https://twitter.com/geofflangdale/status/1484460241240539137
                // https://twitter.com/geofflangdale/status/1484460243778097159
                // https://twitter.com/geofflangdale/status/1484460245560684550
                // https://twitter.com/geofflangdale/status/1484460247368355842
                // I wish I'd never comment twitter links in code... but here we are.

                do
                {
                    var v = Vector128.LoadUnsafe(ref srcRef, offset);

                    var t1 = v + Vector128.Create((byte)(0xFF - '9')); // Move digits '0'..'9' into range 0xF6..0xFF.
                    var t2 = subtractSaturate(t1, Vector128.Create((byte)6));
                    var t3 = Vector128.Subtract(t2, Vector128.Create((byte)0xF0));
                    var t4 = v & Vector128.Create((byte)0xDF);
                    var t5 = t4 - Vector128.Create((byte)'A');
                    var t6 = addSaturate(t5, Vector128.Create((byte)10));

                    var t7 = Vector128.Min(t3, t6);
                    var t8 = addSaturate(t7, Vector128.Create((byte)(127 - 15)));

                    if (t8.ExtractMostSignificantBits() != 0)
                        return T.Zero;

                    Vector128<byte> t0;
                    if (Sse3.IsSupported)
                    {
                        t0 = Ssse3.MultiplyAddAdjacent(t7,
                            Vector128.Create((short)0x0110).AsSByte()).AsByte();
                    }
                    else if (AdvSimd.Arm64.IsSupported)
                    {
                        // Workaround for missing MultiplyAddAdjacent on ARM -- Stolen from corelib
                        // Note this is specific to the 0x0110 case - See Convert.FromHexString.
                        var even = AdvSimd.Arm64.TransposeEven(t7, Vector128<byte>.Zero).AsInt16();
                        var odd = AdvSimd.Arm64.TransposeOdd(t7, Vector128<byte>.Zero).AsInt16();
                        even = AdvSimd.ShiftLeftLogical(even, 4).AsInt16();
                        t0 = AdvSimd.AddSaturate(even, odd).AsByte();
                    }
                    else if (PackedSimd.IsSupported)
                    {
                        Vector128<byte> shiftedNibbles = PackedSimd.ShiftLeft(t7, 4);
                        Vector128<byte> zipped = PackedSimd.BitwiseSelect(t7, shiftedNibbles, Vector128.Create((ushort)0xFF00).AsByte());
                        t0 = PackedSimd.AddPairwiseWidening(zipped).AsByte();
                    }
                    else
                    {
                        // Consider sse2neon ?
                        throw new UnreachableException();
                    }

                    var output = Vector128.Shuffle(t0, Vector128.Create((byte)0, 2, 4, 6, 8, 10, 12, 14, 0, 0, 0, 0, 0, 0, 0, 0));

                    Unsafe.WriteUnaligned(
                        ref Unsafe.Add(
                            ref MemoryMarshal.GetReference(workBuffer),
                            offset / 2
                        ),
                        output.AsUInt64().ToScalar()
                    );

                    offset += (nuint)Vector128<byte>.Count;
                }
                while (offset < (nuint)sourceChars.Length);
            }

            for (; offset < (nuint)sourceChars.Length; offset += 2)
            {
                var highNibble = Unsafe.Add(ref srcRef, offset);
                highNibble = (byte)(highNibble - (highNibble < 58 ? 48 : 87));

                var lowNibble = Unsafe.Add(ref srcRef, offset + 1);
                lowNibble = (byte)(lowNibble - (lowNibble < 58 ? 48 : 87));

                Unsafe.Add(ref dstRef, offset / 2) = (byte)(highNibble << 4 | lowNibble);
            }

            return T.From(workBuffer);

            // Should be Vector128.SubtractSaturate but that appears to not be public on .NET 9??
            Vector128<byte> subtractSaturate(Vector128<byte> left, Vector128<byte> right)
            {
                if (Sse2.IsSupported)
                    return Sse2.SubtractSaturate(left, right);

                if (!AdvSimd.Arm64.IsSupported)
                    throw new NotSupportedException();

                return AdvSimd.SubtractSaturate(left, right);
            }

            // Should be Vector128.AddSaturate but that appears to not be public on .NET 9??
            Vector128<byte> addSaturate(Vector128<byte> left, Vector128<byte> right)
            {
                if (Sse2.IsSupported)
                    return Sse2.AddSaturate(left, right);

                if (!AdvSimd.Arm64.IsSupported)
                    throw new NotSupportedException();

                return AdvSimd.AddSaturate(left, right);
            }
        }
    }

    public static class KeyExtensions
    {
        /// <summary>
        /// Converts the given hex ASCII string to an implementation of <see cref="IOwnedKey{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of key to produce</typeparam>
        /// <param name="str">An ASCII hex string to parse.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public static T? AsKeyString<T>(this ReadOnlySpan<byte> str) where T : notnull, IOwnedKey<T>
            => T.FromString(str);
        /// <summary>
        /// Converts the given hex ASCII string to an implementation of <see cref="IOwnedKey{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of key to produce</typeparam>
        /// <param name="str">An ASCII hex string to parse.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public static T[] AsKeyString<T>(this ReadOnlySpan<byte> str, byte delimiter) where T : notnull, IOwnedKey<T>
            => T.FromString(str, delimiter);

        /// <summary>
        /// Converts the given bytes into an implementation of <see cref="IKey{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of key to produce</typeparam>
        /// <param name="bytes">The bytes to treat as a <typeparamref name="T"/></param>.
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public static T AsKey<T>(this ReadOnlySpan<byte> bytes) where T : notnull, IKey<T>, allows ref struct
            => T.From(bytes);

        /// <summary>
        /// Converts the given bytes as a pair of keys.
        /// </summary>
        /// <typeparam name="T">The type of the left key.</typeparam>
        /// <typeparam name="U">The type of the right key.</typeparam>
        /// <param name="bytes"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public static (T, U) AsKeyStringPair<T, U>(this ReadOnlySpan<byte> bytes)
            where T : IOwnedKey<T>
            where U : IOwnedKey<U>
        {
            var tokens = bytes.Split((byte) ' ', true);
            Debug.Assert(tokens.Length == 2);

            return (T.FromString(bytes[tokens[0]]), U.FromString(bytes[tokens[1]]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public static T AsKey<T>(this string @string) where T : IOwnedKey<T>
            => T.FromString(@string);
    }
}
