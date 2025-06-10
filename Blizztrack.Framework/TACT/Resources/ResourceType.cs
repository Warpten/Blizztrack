namespace Blizztrack.Framework.TACT.Resources
{
    /// <summary>
    /// A resource type.
    /// </summary>
    public readonly struct ResourceType
    {
        private readonly int _index;

        /// <summary>
        /// The subdirectory, on Blizzard's CDNs, in which a resource with this type would live.
        /// </summary>
        public readonly string RemotePath;

        /// <summary>
        /// The subdirectory, on disk, in which a resource with this type would live.
        /// </summary>
        public readonly string LocalPath;

        private ResourceType(int index, string remotePath, string localPath)
        {
            _index = index;
            RemotePath = remotePath;
            LocalPath = localPath;
        }

        /// <summary>
        /// The resource is a configuration file.
        /// </summary>
        public static readonly ResourceType Config = new(0, "config", "config");

        /// <summary>
        /// The resource is an archive.
        /// </summary>
        public static readonly ResourceType Data = new(1, "data", "data");

        /// <summary>
        /// The resource is an archive index.
        /// </summary>
        public static readonly ResourceType Indice = new(2, "data", "indices");

        /// <summary>
        /// Technically not a true resource type; this type denotes files that have been locally
        /// cached on disk after decompression.
        /// </summary>
        public static readonly ResourceType Decompressed = new(3, "data", "decompressed");

        public static bool operator ==(ResourceType lhs, ResourceType rhs) => lhs._index == rhs._index;
        public static bool operator !=(ResourceType lhs, ResourceType rhs) => lhs._index != rhs._index;
    }
}
