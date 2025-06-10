using Blizztrack.Framework.TACT.Resources;

namespace Blizztrack.Framework.TACT.Implementation
{
    public interface IResourceParser<T> where T : class, IResourceParser<T>
    {
        public static abstract T OpenResource(ResourceHandle resourceHandle);
        public static abstract T OpenCompressedResource(ResourceHandle resourceHandle);
    }
}
