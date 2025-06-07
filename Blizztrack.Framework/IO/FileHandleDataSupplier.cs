using Blizztrack.Framework.TACT.Resources;

using Microsoft.Win32.SafeHandles;

using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.IO
{
    public struct FileHandleDataSupplier(ResourceHandle resourceHandle, FileOptions fileOptions = FileOptions.SequentialScan) : IBinaryDataSupplier
    {
        private readonly SafeFileHandle _fileHandle = resourceHandle.AsFileHandle(fileOptions);
        private int _length = resourceHandle.Length;
        private long _offset = resourceHandle.Offset;

        public readonly void Dispose()
        {
            _fileHandle.Close();
            _fileHandle.Dispose();
        }

        public readonly ReadOnlySpan<byte> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var (offset, length) = range.GetOffsetAndLength(_length);
                var dataBuffer = GC.AllocateUninitializedArray<byte>(length);
                var readCount = RandomAccess.Read(_fileHandle, dataBuffer, _offset + offset);
                return dataBuffer.AsSpan()[..readCount];
            }
        }

        public readonly byte this[int offset]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var value = (byte) 0;
                _ = RandomAccess.Read(_fileHandle, new Span<byte>(ref value), _offset + offset);
                return value;
            }
        }

        public readonly byte this[Index index] => this[index.GetOffset(_length)];

        public readonly int Length => _length;
    }
}

