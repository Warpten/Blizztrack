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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stream ToStream()
        {
            if (_fileInfo is not null)
                return new FileStream(_fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 0, true);

            return Stream.Null;
        }

        public static bool operator ==(ResourceHandle left, ResourceHandle right)
            => left.Length == right.Length && left.Offset == right.Offset && left.Path == right.Path;

        public static bool operator !=(ResourceHandle left, ResourceHandle right)
            => !(left == right);

        public override bool Equals([NotNullWhen(true)] object? obj)
            => obj is ResourceHandle handle && this == handle;

        public override int GetHashCode() => HashCode.Combine(Path, Offset, Length);
    }
}
