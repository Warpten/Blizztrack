namespace Blizztrack.Framework.TACT.Services
{
    public class CacheSettings
    {
        /// <summary>
        /// The path of the cache directory on disk.
        /// </summary>
        public required string Path { get; init; }

        public required string[] CDNs { get; init; }
    }
}
