using Blizztrack.Framework.TACT.Resources;

using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.IO
{
    public readonly struct MemoryMappedDataSupplier : IBinaryDataSupplier
    {
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly unsafe byte* _rawData;
        private readonly nint _offset;
        private readonly int _length;

        public readonly int Length => _length;

        public unsafe MemoryMappedDataSupplier(ResourceHandle resourceHandle)
        {
            var memoryMappedFile = resourceHandle.AsMappedFile();
            _accessor = memoryMappedFile.CreateViewAccessor(resourceHandle.Offset, resourceHandle.Length);
            _offset = (nint) resourceHandle.Offset;
            _length = resourceHandle.Length;

            _rawData = OperatingSystem.IsWindows()
                ? (byte*)_accessor.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer()
                : null;
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _rawData);

            if (_rawData == null)
                throw new InvalidOperationException("Failed to retrieve a pointer to file data");
        }

        public readonly unsafe void Dispose()
        {
            if (_rawData != null && !OperatingSystem.IsWindows())
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();

            _accessor.Dispose();
        }

        public readonly unsafe ReadOnlySpan<byte> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var (offset, length) = range.GetOffsetAndLength(_length);
                return new(_rawData + _offset + offset, length);
            }
        }

        public readonly unsafe byte this[int offset]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.AddByteOffset(ref Unsafe.AsRef<byte>(_rawData), _offset + offset);
        }

        public readonly byte this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[index.GetOffset(_length)];
        }
    }
}

