using System.Security.AccessControl;

namespace Blizztrack.Framework
{
    public interface IResource
    {
        public ResourceType Type { get; }
        public long Offset { get; }
        public long Length { get; }
    }
}
