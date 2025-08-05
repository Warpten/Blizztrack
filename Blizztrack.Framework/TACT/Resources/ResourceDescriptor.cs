namespace Blizztrack.Framework.TACT.Resources
{
    /// <summary>
    /// A resource descriptor is a thin wrapped around a file within the CASC file system.
    /// </summary>
    public readonly struct ResourceDescriptor
    {
        /// <summary>
        /// The type of the resource.
        /// </summary>
        public readonly ResourceType Type;

        /// <summary>
        /// One of the products that owns this resource.
        /// </summary>
        public readonly string Product;

        /// <summary>
        /// The name of the archive containing this resource.
        /// </summary>
        public readonly EncodingKey Archive;

        /// <summary>
        /// The offset at which this resource is located within its archive.
        /// </summary>
        public readonly long Offset;

        /// <summary>
        /// The amount of bytes (after compression) this resource occupies within its archive.
        /// </summary>
        public readonly long Length;

        internal ResourceDescriptor(ResourceType type, string product, ReadOnlySpan<byte> archive, long offset = 0, long length = 0)
        {
            Type = type;
            Product = product;
            Archive = new (archive);
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// Returns the relative path of this resource on disk.
        /// </summary>
        public readonly string LocalPath => Type.FormatLocal(Archive.AsRef());

        /// <summary>
        /// Returns the relative path of this resource on CDNs.
        /// </summary>
        public readonly string RemotePath => Type.FormatRemote(Archive);
    }
}
