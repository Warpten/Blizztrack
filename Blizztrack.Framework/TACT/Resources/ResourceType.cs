using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.TACT.Resources
{
    file static class Shared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(string formatString, string archiveName)
            => string.Format(formatString, archiveName[0..2], archiveName[2..4], archiveName);
    }

    internal readonly struct ResourceTypeImpl
    {
        internal static readonly ResourceTypeImpl _config;
        internal static readonly ResourceTypeImpl _indice;
        internal static readonly ResourceTypeImpl _data;
        internal static readonly ResourceTypeImpl _decompressed;

        private readonly int _identity;
        private readonly string _localPath;
        private readonly string _remotePath;

        internal ResourceTypeImpl(int identity, string remotePath, string localPath)
        {
            _identity = identity;
            _localPath = localPath;
            _remotePath = remotePath;
        }

        internal ResourceTypeImpl(int identity, string path) : this(identity, path, path) { }

        public string FormatLocal<K>(K key) where K : struct, IKey<K>, allows ref struct => Shared.Format(_localPath, key.AsHexString());
        public string FormatRemote<K>(K key) where K : struct, IKey<K>, allows ref struct => Shared.Format(_remotePath, key.AsHexString());

        static ResourceTypeImpl()
        {
            _config = new(0, "config/{0}/{1}/{2}");
            _indice = new(1, "data/{0}/{1}/{2}.index", "indices/{0}/{1}/{2}");
            _data = new(2, "data/{0}/{1}/{2}");
            _decompressed = new(3, "data/{0}/{1}/{2}", "decompressed/{0}/{1}/{2}");
        }
    }

    public readonly struct ResourceType
    {
        static ResourceType()
        {
            Config = new (ResourceTypeImpl._config);
            Data = new(ResourceTypeImpl._data);
            Indice = new(ResourceTypeImpl._indice);
            Decompressed = new(ResourceTypeImpl._decompressed);
        }
        
        /// <summary>
        /// The resource is a configuration file.
        /// </summary>
        public static readonly EncodingResourceType Config;

        /// <summary>
        /// The resource is an archive.
        /// </summary>
        public static readonly EncodingResourceType Data;

        /// <summary>
        /// The resource is an archive index.
        /// </summary>
        public static readonly EncodingResourceType Indice;

        /// <summary>
        /// Technically not a true resource type; this type denotes files that have been locally
        /// cached on disk after decompression.
        /// </summary>
        public static readonly ContentResourceType Decompressed;
    }

    /// <summary>
    /// A resource type.
    /// </summary>
    public readonly struct EncodingResourceType
    {
        internal readonly ResourceTypeImpl _identity;

        internal EncodingResourceType(in ResourceTypeImpl identity) => _identity = identity;

        public ResourceDescriptor ToDescriptor(string productCode, in Views.EncodingKey archiveKey, in Views.EncodingKey encodingKey, in Views.ContentKey contentKey, long offset, long length)
            => new(_identity, productCode, new(archiveKey.AsSpan()), offset, length)
            {
                EncodingKey = new(encodingKey.AsSpan()),
                ContentKey = new(contentKey.AsSpan())
            };

        public ResourceDescriptor ToDescriptor(string productCode, in Views.EncodingKey archiveKey, long offset = 0, long length = 0)
            => ToDescriptor(productCode, archiveKey, archiveKey, ContentKey.Zero, offset, length);

        public ResourceDescriptor ToDescriptor(string productCode, in Views.EncodingKey archiveKey, in Views.ContentKey contentKey, long offset = 0, long length = 0)
            => ToDescriptor(productCode, archiveKey, archiveKey, contentKey, offset, length);
    }

    /// <summary>
    /// This resource type expects content keys.
    /// </summary>
    public readonly struct ContentResourceType
    {
        internal readonly ResourceTypeImpl _identity;

        internal ContentResourceType(in ResourceTypeImpl identity) => _identity = identity;
        
        public readonly ResourceDescriptor ToDescriptor(string productCode, in Views.EncodingKey archiveKey, in Views.ContentKey contentKey, long offset = 0, long length = 0)
            => new(_identity, productCode, new(contentKey.AsSpan()), offset, length)
            {
                EncodingKey = new(archiveKey.AsSpan()),
                ContentKey = new(contentKey.AsSpan())
            };  
    }
}
