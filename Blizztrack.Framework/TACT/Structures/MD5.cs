using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Framework.TACT.Structures
{
    [InlineArray(16)]
    public struct MD5
    {
        public const int Length = 16;

        private byte _element;

        internal MD5(ReadOnlySpan<byte> sourceData) => sourceData.CopyTo(MemoryMarshal.CreateSpan(ref _element, Length));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan() => MemoryMarshal.CreateSpan(ref _element, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref _element, Length);
    }
}
