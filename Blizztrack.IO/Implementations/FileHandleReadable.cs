using Blizztrack.IO.Extensions;

using Microsoft.Win32.SafeHandles;

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.IO.Implementations
{
    public readonly struct FileHandleReadable(SafeFileHandle fileHandle)
        : IReadable, ISliceable<FileHandleReadable, FileHandleReadable.Temporary>
    {
        private readonly SafeFileHandle _fileHandle = fileHandle;

        public void Dispose() => _fileHandle.Dispose();

        #region ISliceable
        Temporary ISliceable<FileHandleReadable, Temporary>.this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SliceCore(range);
        }
        Temporary ISliceable<FileHandleReadable, Temporary>.Slice(int offset, int length) => SliceCore(new Range(offset, offset + length));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Temporary SliceCore(Range range)
        {
            var (offset, length) = range.GetOffsetAndLength((int)RandomAccess.GetLength(_fileHandle));
            return new(_fileHandle, offset, length);
        }
        #endregion

        #region IReadable
        readonly U IReadable.ReadCore<U>(nint byteOffset, bool reverseEndianness)
            => Shared.ReadImpl<U>(_fileHandle, byteOffset, reverseEndianness);

        readonly ReadOnlySpan<U> IReadable.ReadCore<U>(nint byteOffset, int count, bool reverseEndianness)
            => Shared.ReadImpl<U>(_fileHandle, byteOffset, count, reverseEndianness);
        #endregion

        public readonly struct Temporary(SafeFileHandle fileHandle, int offset, int length) : IReadable, ISliceable<Temporary>
        {
            private readonly SafeFileHandle _fileHandle = fileHandle;
            private readonly int _offset = offset;
            private readonly int _length = length;

            Temporary ISliceable<Temporary, Temporary>.this[Range range] => throw new NotImplementedException();

            #region IReadable
            readonly U IReadable.ReadCore<U>(nint byteOffset, bool reverseEndianness)
                => Shared.ReadImpl<U>(_fileHandle, byteOffset + _offset, reverseEndianness);

            readonly ReadOnlySpan<U> IReadable.ReadCore<U>(nint byteOffset, int count, bool reverseEndianness)
                => Shared.ReadImpl<U>(_fileHandle, byteOffset + _offset, count, reverseEndianness);
            #endregion

            readonly Temporary ISliceable<Temporary, Temporary>.Slice(int offset, int length)
                => new(_fileHandle, _offset + offset, length);
        }
    }

    file class Shared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static U ReadImpl<U>(SafeFileHandle fileHandle, nint byteOffset, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>
        {
            var value = default(U);
            var valueSpan = new Span<U>(ref value);
            var rawDataSpan = MemoryMarshal.Cast<U, byte>(valueSpan);

            var readCount = RandomAccess.Read(fileHandle, rawDataSpan, byteOffset);
            Debug.Assert(readCount == Unsafe.SizeOf<U>());

            if (reverseEndianness)
                valueSpan.ReverseEndianness();

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<U> ReadImpl<U>(SafeFileHandle fileHandle, nint byteOffset, int count, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>
        {
            var value = GC.AllocateUninitializedArray<U>(count);
            var rawDataSpan = MemoryMarshal.Cast<U, byte>(value);

            var readCount = RandomAccess.Read(fileHandle, rawDataSpan, byteOffset);
            Debug.Assert(readCount == Unsafe.SizeOf<U>());

            if (reverseEndianness)
                value.AsSpan().ReverseEndianness();

            return value;
        }
    }
}
