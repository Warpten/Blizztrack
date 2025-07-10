using Blizztrack.Framework.TACT.Resources;

namespace Blizztrack.Framework.TACT.Implementation
{
    public interface IResourceParser<T> where T : class, IResourceParser<T>
    {
        /// <summary>
        /// Parses a resource, given a valid handle to it. The resource should be guaranteed to not be <see cref="BLTE"/>-encoded.
        /// </summary>
        /// <param name="resourceHandle">A handle to the resource.</param>
        /// <returns></returns>
        public static abstract T OpenResource(ResourceHandle resourceHandle);

        /// <summary>
        /// Parses a resource, given its handle. The resource should be guaranteed to be <see cref="BLTE"/>-encoded.
        /// </summary>
        /// <param name="resourceHandle">A handle to the resource.</param>
        /// <returns></returns>
        public static abstract T OpenCompressedResource(ResourceHandle resourceHandle);
    }
}
