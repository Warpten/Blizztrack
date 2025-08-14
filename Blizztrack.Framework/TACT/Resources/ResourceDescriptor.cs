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
        private readonly ResourceTypeImpl _type;

        /// <summary>
        /// One of the products that owns this resource.
        /// </summary>
        public readonly string Product;

        /// <summary>
        /// The name of the archive containing this resource.
        /// </summary>
        public readonly EncodingKey Archive;

        /// <summary>
        /// The encoding key of the resource this descriptor represents. Note that this information may not be available.
        /// In that case, this property will be <see cref="EncodingKey.Zero"/>.
        /// </summary>
        public readonly EncodingKey EncodingKey { get; init; }

        /// <summary>
        /// The content key of the resource this descriptor represents. Note that this information may not be available.
        /// In that case, this property will be <see cref="ContentKey.Zero"/>.
        /// </summary>
        public readonly ContentKey ContentKey { get; init; }

        /// <summary>
        /// The offset at which this resource is located within its archive.
        /// </summary>
        public readonly long Offset;

        /// <summary>
        /// The amount of bytes (after compression) this resource occupies within its archive.
        /// </summary>
        public readonly long Length;

        internal ResourceDescriptor(ResourceTypeImpl type, string product, EncodingKey archive, long offset = 0, long length = 0)
        {
            _type = type;
            Product = product;

            Archive = archive;
            
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// Returns the relative path of this resource on disk.
        /// </summary>
        public readonly string LocalPath => _type.FormatLocal(Archive.AsRef());

        /// <summary>
        /// Returns the relative path of this resource on CDNs.
        /// </summary>
        public readonly string RemotePath => _type.FormatRemote(Archive);
    }
}
