using Blizztrack.IO.Extensions;

using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.IO.Implementations
{
    public struct MemoryMappedReadable : IReadable, ISliceable<MemoryMappedReadable, MemoryMappedReadable.Temporary>
    {
        private readonly MemoryMappedViewAccessor _accessor;
        // Inlined Span<byte>.
        private unsafe byte* _rawData;
        private readonly long _length;

        public MemoryMappedReadable(MemoryMappedFile mmf, long offset, long length) : this(mmf.CreateViewAccessor(offset, length), length) { }

        public unsafe MemoryMappedReadable(MemoryMappedViewAccessor accessor, long length)
        {
            _accessor = accessor;
            _length = length;

            if (OperatingSystem.IsWindows())
                _rawData = (byte*) _accessor.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();
            else
                _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _rawData);

            if (_rawData == null)
                throw new InvalidOperationException("Failed to retrieve a pointer to file data");
        }

        public unsafe void Dispose()
        {
            if (!OperatingSystem.IsWindows())
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();

            _accessor.Dispose();
            _rawData = null;
        }

        #region IReadable
        readonly unsafe U IReadable.ReadCore<U>(nint byteOffset, bool reverseEndianness)
            => Shared.ReadImpl<U>(_rawData, _length, byteOffset, reverseEndianness);

        readonly unsafe ReadOnlySpan<U> IReadable.ReadCore<U>(nint byteOffset, int count, bool reverseEndianness)
            => Shared.ReadImpl<U>(_rawData, _length, byteOffset, count, reverseEndianness);
        #endregion

        #region ISliceable
        public readonly unsafe Temporary this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var (offset, length) = range.GetOffsetAndLength((int) _length);
                return new(_rawData, offset, length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly unsafe Temporary Slice(int offset, int length) => new(_rawData, offset, length);
        #endregion

        public readonly unsafe struct Temporary(byte* rawData, int offset, int length) : ISliceable<Temporary>, IReadable
        {
            private readonly unsafe byte* _rawData = rawData + offset;
            private readonly int _length = length;

            public Temporary this[Range range]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    var (offset, length) = range.GetOffsetAndLength(_length);
                    return new(_rawData, offset, length);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Temporary Slice(int offset, int length)
                => new(_rawData, offset, length);

            U IReadable.ReadCore<U>(nint byteOffset, bool reverseEndianness)
                => Shared.ReadImpl<U>(_rawData, _length, byteOffset, reverseEndianness);

            ReadOnlySpan<U> IReadable.ReadCore<U>(nint byteOffset, int count, bool reverseEndianness)
                => Shared.ReadImpl<U>(_rawData, _length, byteOffset, count, reverseEndianness);
        }
    }
    file static class Shared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe U ReadImpl<U>(byte* rawData, long length, nint byteOffset, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>
        {
            Debug.Assert(byteOffset + Unsafe.SizeOf<U>() <= length);

            var value = Unsafe.ReadUnaligned<U>(ref Unsafe.AddByteOffset(ref Unsafe.AsRef<byte>(rawData), byteOffset));
            if (reverseEndianness)
                value.ReverseEndianness();

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ReadOnlySpan<U> ReadImpl<U>(byte* rawData, long length, nint byteOffset, int count, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>
        {
            Debug.Assert(byteOffset + Unsafe.SizeOf<U>() * count <= length);

            ref var dataSource = ref Unsafe.AddByteOffset(ref Unsafe.AsRef<byte>(rawData), byteOffset);
            var dataSpan = MemoryMarshal.CreateReadOnlySpan(ref dataSource, count * Unsafe.SizeOf<U>());
            var typedSpan = MemoryMarshal.Cast<byte, U>(dataSpan);
            if (reverseEndianness)
            {
                var dataBuffer = new U[count];
                typedSpan.CopyTo(dataBuffer);
                dataBuffer.ReverseEndianness();
                return dataBuffer;
            }

            return typedSpan;
        }
    }
}
