using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Shared.IO;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.TACT.Resources
{
    public readonly struct ResourceHandle(string filePath, long offset = 0, int length = 0)
    {
        private readonly FileInfo? _fileInfo = new (filePath);
        public readonly string Path => _fileInfo?.FullName ?? string.Empty;
        public readonly int Length => length == 0 && Exists ? (int) _fileInfo!.Length : length;
        public readonly long Offset = offset;

        public readonly string Name => _fileInfo?.Name ?? string.Empty;

        public readonly bool Exists => (_fileInfo?.Exists ?? false) && _fileInfo?.Length != 0;

        /// <summary>
        /// Reads a structure of type T from the underlying file. If said file is a BLTE archive, the BLTE archive itself
        /// will be read, not its content!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        /// <remarks>Never expose this method to library users.</remarks>
        internal void Read<T>(System.Index offset, out T value) where T : struct
        {
            var relativeOffset = offset.GetOffset(Length);
            var absoluteOffset = relativeOffset + Offset;

            var absoluteLength = Unsafe.SizeOf<T>();
            Debug.Assert(absoluteOffset + absoluteLength <= Length);

            using var mmf = AsMappedFile();
            using var accessor = mmf.CreateViewAccessor(absoluteOffset, absoluteLength);

            accessor.Read(0, out value);
        }

        public void Create(byte[] resourceData)
        {
            if (_fileInfo is not null)
                File.WriteAllBytes(_fileInfo.FullName, resourceData);
        }

        /// <summary>
        /// Maps this resource to memory.
        /// </summary>
        /// <returns>An implementation of <see cref="ISpanSource"/>.</returns>
        /// <remarks>This function should only be used with implementations of <see cref="IResourceParser{T}"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MappedDataSource ToMappedDataSource() => new(Path);

        // IMPLEMENTATION DETAIL.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MemoryMappedFile AsMappedFile()
            => MemoryMappedFile.CreateFromFile(Path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

        public static bool operator ==(ResourceHandle left, ResourceHandle right)
            => left.Length == right.Length && left.Offset == right.Offset && left.Path == right.Path;

        public static bool operator !=(ResourceHandle left, ResourceHandle right)
            => !(left == right);

        public override bool Equals([NotNullWhen(true)] object? obj)
            => obj is ResourceHandle handle && this == handle;

        public override int GetHashCode() => HashCode.Combine(Path, Offset, Length);
    }
}
