namespace Blizztrack.Framework.TACT.Resources
{
    /// <summary>
    /// A resource type.
    /// </summary>
    public readonly struct ResourceType
    {
        /// <summary>
        /// The subdirectory, on Blizzard's CDNs, in which a resource with this type would live.
        /// </summary>
        public readonly string RemotePath;

        /// <summary>
        /// The subdirectory, on disk, in which a resource with this type would live.
        /// </summary>
        public readonly string LocalPath;

        private ResourceType(string remotePath, string localPath)
        {
            RemotePath = remotePath;
            LocalPath = localPath;
        }

        /// <summary>
        /// The resource is a configuration file.
        /// </summary>
        public static readonly ResourceType Config = new("config", "config");

        /// <summary>
        /// The resource is an archive.
        /// </summary>
        public static readonly ResourceType Data = new("data", "data");

        /// <summary>
        /// The resource is an archive index.
        /// </summary>
        public static readonly ResourceType Indice = new("data", "indices");

        /// <summary>
        /// Technically not a true resource type; this type denotes files that have been locally
        /// cached on disk after decompression.
        /// </summary>
        public static readonly ResourceType Compressed = new("data", "decompressed");
    }
}
