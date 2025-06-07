using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Buffers
{
    public static class MemoryMappedFileExtensions
    {
        /// <summary>
        /// Maps a <pre>[<paramref name="offset"/>, <paramref name="offset"/> + <paramref name="count"/>)</pre>
        /// segment of file data to memory.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="file"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static MappedMemoryManager<T> MapSegment<T>(this MemoryMappedFile file, long offset, int count) where T : unmanaged
            => new(file, offset, count);
    }

    public class MappedMemoryManager<T> : MemoryManager<T> where T : unmanaged
    {
        private readonly MemoryMappedViewAccessor _fileView;
        private readonly int _count;
        private unsafe byte* _ptr = null;

        public unsafe MappedMemoryManager(MemoryMappedFile file, long offset, int count)
        {
            _fileView = file.CreateViewAccessor(offset, count);
            _count = count;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _ptr = (byte*)_fileView.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();
            else
                _fileView.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
        }

        public unsafe override Span<T> GetSpan()
        {
            if (_ptr == null)
                return [];

            return new Span<T>((T*)_ptr, _count);
        }

        public unsafe override MemoryHandle Pin(int elementIndex = 0)
            => _ptr == null || elementIndex >= _count || elementIndex < 0
                ? default
                : new((T*)(_ptr + elementIndex * Unsafe.SizeOf<T>()));

        public override void Unpin() { }

        protected unsafe override void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (_ptr != null && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _fileView.SafeMemoryMappedViewHandle.ReleasePointer();

            _ptr = null;
            _fileView.Dispose();
        }
    }
}
