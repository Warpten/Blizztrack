using Microsoft.Win32.SafeHandles;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.TACT.Resources
{
    public readonly struct ResourceHandle(string filePath, long offset, int length)
    {
        private readonly string _filePath = Path.GetFullPath(filePath);
        
        public ResourceHandle(FileInfo fileInfo) : this(fileInfo.FullName, 0, (int) fileInfo.Length) { }

        public readonly int Length = length;
        public readonly long Offset = offset;

        /// <summary>
        /// Reads a structure of type T from the underlying file. If said file is a BLTE archive, the BLTE archive itself
        /// will be read, not its content!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        /// <remarks>Never expose this method to library users.</remarks>
        internal void Read<T>(Index offset, out T value) where T : struct
        {
            var relativeOffset = offset.GetOffset(Length);
            var absoluteOffset = relativeOffset + Offset;

            var absoluteLength = Unsafe.SizeOf<T>();
            Debug.Assert(absoluteOffset + absoluteLength <= Length);

            using var mmf = AsMappedFile();
            using var accessor = mmf.CreateViewAccessor(absoluteOffset, absoluteLength);

            accessor.Read(0, out value);
        }

        /// <summary>
        /// Opens a stream over the file. 
        /// </summary>
        /// <returns>A stream optimized for asynchronous I/O over the file.</returns>
        /// <remarks>The stream returned by this method is optimized for asynchronous I/O operations.</remarks>
        internal FileStream OpenStream()
            => new(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 0, true);

        public MappedMemory AsMappedMemory() => new(AsMappedFile(), Offset, Length);

        internal SafeFileHandle AsFileHandle(FileOptions options) => File.OpenHandle(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, options);

        internal MemoryMappedFile AsMappedFile()
            => MemoryMappedFile.CreateFromFile(_filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

        public static bool operator ==(ResourceHandle left, ResourceHandle right)
            => left.Length == right.Length && left.Offset == right.Offset && left._filePath == right._filePath;

        public static bool operator !=(ResourceHandle left, ResourceHandle right)
            => !(left == right);

        public override bool Equals([NotNullWhen(true)] object? obj)
            => obj is ResourceHandle handle && this == handle;

        public override int GetHashCode() => HashCode.Combine(_filePath, Offset, Length);
    }
}
