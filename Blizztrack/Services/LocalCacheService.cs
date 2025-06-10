using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;

using Microsoft.Extensions.Options;

namespace Blizztrack.Services
{
    /// <summary>
    /// This service provides access to files on disk, given a <see cref="ResourceDescriptor" />.
    /// </summary>
    /// <param name="settings"></param>
    public class LocalCacheService(IOptionsMonitor<Settings> settings)
    {
        public string CreatePath(string relativePath)
        {
            var filePath = Path.Combine(settings.CurrentValue.Cache.Path, relativePath);
            Directory.GetParent(filePath)?.Create();
            return filePath;
        }

        public void Write(string filePath, byte[] fileData) => File.WriteAllBytes(CreatePath(filePath), fileData);

        public ResourceHandle OpenHandle(ResourceDescriptor descriptor) => new (CreatePath(descriptor.LocalPath));
    }
}
