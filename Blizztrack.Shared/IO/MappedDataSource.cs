using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Blizztrack.Shared.IO
{
    /// <summary>
    /// Eagerly maps to memory a specific segment of a file to be used as a <see cref="IDataSource"/>.
    /// </summary>
    public readonly struct MappedDataSource : IDataSource
    {
        private readonly MemoryMappedFile _memoryMappedFile;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly unsafe byte* _dataBuffer;

        /// <summary>
        /// Maps the given segment of the file.
        /// </summary>
        /// <param name="filePath">A complete path to the file on disk.</param>
        /// <param name="offset">The offset at which to begin mapping data. A value of zero denotes the start of the file.</param>
        /// <param name="length">The amount of bytes to map. A value of zero will cause an amount of bytes ranging from <paramref name="offset"/> to roughly the end of the file to be mapped.</param>
        public MappedDataSource(string filePath, long offset = 0, int length = 0)
        {
            _memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, length, MemoryMappedFileAccess.Read);
            _accessor = _memoryMappedFile.CreateViewAccessor(offset, length);

            unsafe
            {
                _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _dataBuffer);
            }
        }

        public readonly void Dispose()
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _memoryMappedFile.Dispose();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ReadOnlySpan<byte> Slice(int offset, int length)
            => new (_dataBuffer + (nint)(uint) offset, length);

        public unsafe readonly ReadOnlySpan<byte> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var (offset, length) = range.GetOffsetAndLength((int) _accessor.Capacity);
                return Slice(offset, length);
            }
        }
        public unsafe byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataBuffer[index];
        }

        public unsafe byte this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataBuffer[index.GetOffset((int) _accessor.Capacity)];
        }
    }
}
