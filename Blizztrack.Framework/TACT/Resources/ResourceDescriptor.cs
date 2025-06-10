namespace Blizztrack.Framework.TACT.Resources
{
    /// <summary>
    /// A resource descriptor is a thin wrapped around a file within the CASC file system.
    /// </summary>
    /// <param name="type">The type of the resource.</param>
    /// <param name="archiveName">The name of the archive containing this resource.</param>
    /// <param name="offset">The offset at which this resource is located within its archive.</param>
    /// <param name="length">The amount of bytes (after compression) this resource occupies within its archive.</param>
    public readonly record struct ResourceDescriptor(ResourceType Type, string Product, string ArchiveName, long Offset = 0, long Length = 0)
    {
        public ResourceDescriptor(ResourceType resourceType, string productCode, ReadOnlySpan<byte> archiveHash, long offset = 0, long length = 0)
            : this(resourceType, productCode, Convert.ToHexStringLower(archiveHash), offset, length)
        { }

        /// <summary>
        /// Returns the relative path of this resource on disk.
        /// </summary>
        public readonly string LocalPath => $"{Type.LocalPath}/{ArchiveName.AsSpan()[0..2]}/{ArchiveName.AsSpan()[2..4]}/{ArchiveName}";

        /// <summary>
        /// Returns the relative path of this resource on CDNs.
        /// </summary>
        public readonly string RemotePath => $"{Type.RemotePath}/{ArchiveName.AsSpan()[0..2]}/{ArchiveName.AsSpan()[2..4]}/{ArchiveName}";
    }
}
