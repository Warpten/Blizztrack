namespace Blizztrack.Framework.TACT.Resources
{
    /// <summary>
    /// A resource descriptor is a thin wrapped around a file within the CASC file system.
    /// </summary>
    /// <param name="Type">The type of the resource.</param>
    /// <param name="Product">One of the products that owns this resource.</param>
    /// <param name="ArchiveName">The name of the archive containing this resource.</param>
    /// <param name="Offset">The offset at which this resource is located within its archive.</param>
    /// <param name="Length">The amount of bytes (after compression) this resource occupies within its archive.</param>
    public readonly record struct ResourceDescriptor(ResourceType Type, string Product, EncodingKey ArchiveName, long Offset = 0, long Length = 0)
    {
        public static ResourceDescriptor Create<K>(ResourceType type, string product, K archiveName, long offset = 0, long length = 0)
            where K : IKey<K>, allows ref struct
            => new(type, product, new EncodingKey(archiveName.AsSpan()), offset, length);

        /// <summary>
        /// Returns the relative path of this resource on disk.
        /// </summary>
        public readonly string LocalPath => Type.FormatLocal(ArchiveName);

        /// <summary>
        /// Returns the relative path of this resource on CDNs.
        /// </summary>
        public readonly string RemotePath => Type.FormatRemote(ArchiveName);

        private static string Format(string rootPath, bool isIndex, EncodingKey key)
        {
            var keyString = key.AsHexString();
            return isIndex switch
            {
                true => $"{rootPath}/{keyString[0..2]}/{keyString[2..4]}/{keyString}.index",
                false => $"{rootPath}/{keyString[0..2]}/{keyString[2..4]}/{keyString}"
            };
        }
    }
}
