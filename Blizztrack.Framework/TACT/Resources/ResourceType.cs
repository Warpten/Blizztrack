using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Shared.IO;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Framework.TACT.Resources
{
    /// <summary>
    /// A resource type.
    /// </summary>
    public readonly struct ResourceType : IEquatable<ResourceType>
    {
        private readonly int _index;
        private readonly string _remotePath;
        private readonly string _localPath;

        private ResourceType(int index, string remotePath, string localPath)
        {
            _index = index;
            _remotePath = remotePath;
            _localPath = localPath;
        }

        private ResourceType(int index, string path) : this(index, path, path) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string FormatLocal<K>(K key) where K : IKey<K>, allows ref struct => Format(_localPath, key.AsHexString());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string FormatRemote<K>(K key) where K : IKey<K>, allows ref struct => Format(_remotePath, key.AsHexString());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string Format(string formatString, string archiveName)
            => string.Format(formatString, archiveName[0..2], archiveName[2..4], archiveName);

        public ResourceDescriptor ToDescriptor<K>(string productCode, K key, long offset = 0, long length = 0)
            where K : IKey<K>, allows ref struct
            => new (this, productCode, key.AsSpan(), offset, length);

        /// <summary>
        /// The resource is a configuration file.
        /// </summary>
        public static readonly ResourceType Config = new(0, "config/{1}/{2}/{3}");

        /// <summary>
        /// The resource is an archive.
        /// </summary>
        public static readonly ResourceType Data = new(1, "data/{1}/{2}/{3}");

        /// <summary>
        /// The resource is an archive index.
        /// </summary>
        public static readonly ResourceType Indice = new(2, "data/{1}/{2}/{3}.index", "indices/{1}/{2}/{3}");

        /// <summary>
        /// Technically not a true resource type; this type denotes files that have been locally
        /// cached on disk after decompression.
        /// </summary>
        public static readonly ResourceType Decompressed = new(3, "data/{1}/{2}/{3}", "decompressed/{1}/{2}/{3}");

        public static bool operator ==(ResourceType lhs, ResourceType rhs) => lhs._index == rhs._index;
        public static bool operator !=(ResourceType lhs, ResourceType rhs) => lhs._index != rhs._index;

        public override bool Equals([NotNullWhen(true)] object? obj) => obj is ResourceType other && Equals(other);
        public bool Equals(ResourceType other) => other._index == _index;
        public override int GetHashCode() => _index;
    }
}
