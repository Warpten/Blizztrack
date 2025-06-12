using Blizztrack.IO.Extensions;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.IO
{
    public interface IReadable
    {
        public U ReadBE<U>(nint byteOffset) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, BitConverter.IsLittleEndian);
        public U ReadLE<U>(nint byteOffset) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, !BitConverter.IsLittleEndian);

        public U ReadNative<U>(nint byteOffset) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, false);

        public ReadOnlySpan<U> ReadBE<U>(nint byteOffset, int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, count, BitConverter.IsLittleEndian);
        public ReadOnlySpan<U> ReadLE<U>(nint byteOffset, int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, count, BitConverter.IsLittleEndian);
        public ReadOnlySpan<U> ReadNativE<U>(nint byteOffset, int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, count, BitConverter.IsLittleEndian);

        protected U ReadCore<U>(nint byteOffset, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>;
        protected ReadOnlySpan<U> ReadCore<U>(nint byteOffset, int count, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>;
    }

    public readonly struct FileHandleReadable(SafeFileHandle fileHandle) : IReadable, ISliceable<FileHandleReadable, FileHandleReadable.Temporary>
    {
        private readonly SafeFileHandle _fileHandle = fileHandle;

        Temporary ISliceable<FileHandleReadable, Temporary>.this[Range range]
        {
            get
            {
                var (offset, length) = range.GetOffsetAndLength((int) RandomAccess.GetLength(_fileHandle));
                return new(_fileHandle, offset, length);
            }
        }
        Temporary ISliceable<FileHandleReadable, Temporary>.Slice(int offset, int length) => this[new Range(offset, offset + length)];

        public void Dispose() => _fileHandle.Dispose();

        readonly U IReadable.ReadCore<U>(nint byteOffset, bool reverseEndianness)
            => Shared.ReadImpl<U>(_fileHandle, byteOffset, reverseEndianness);

        readonly ReadOnlySpan<U> IReadable.ReadCore<U>(nint byteOffset, int count, bool reverseEndianness)
            => Shared.ReadImpl<U>(_fileHandle, byteOffset, count, reverseEndianness);


        public struct Temporary(SafeFileHandle fileHandle, int offset, int length) : IReadable, ISliceable<Temporary>
        {
            private readonly SafeFileHandle _fileHandle = fileHandle;
            private readonly int _offset = offset;
            private readonly int _length = length;

            U IReadable.ReadCore<U>(nint byteOffset, bool reverseEndianness)
                => Shared.ReadImpl<U>(_fileHandle, byteOffset + _offset, reverseEndianness);

            ReadOnlySpan<U> IReadable.ReadCore<U>(nint byteOffset, int count, bool reverseEndianness)
                => Shared.ReadImpl<U>(_fileHandle, byteOffset + _offset, count, reverseEndianness);
        }
    }

    file class Shared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe U ReadImpl<U>(SafeFileHandle fileHandle, nint byteOffset, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>
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
        public static unsafe ReadOnlySpan<U> ReadImpl<U>(SafeFileHandle fileHandle, nint byteOffset, int count, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>
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
