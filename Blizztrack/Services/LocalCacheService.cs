using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;

using Microsoft.Extensions.Options;

namespace Blizztrack.Services
{
    /// <summary>
    /// This service provides access to files on disk, given a <see cref="ResourceDescriptor" />.
    /// </summary>
    /// <param name="settings"></param>
    public class LocalCacheService(IOptionsMonitor<Settings> settings): ICacheService
    {
        public string CreatePath(string relativePath)
        {
            DirectoryInfo rootDirectory = new(settings.CurrentValue.Cache.Path);

            var filePath = Path.Combine(rootDirectory.FullName, relativePath);
            Directory.GetParent(filePath)?.Create();
            return filePath;
        }

        public ResourceHandle OpenHandle(ResourceDescriptor descriptor)
        {
            DirectoryInfo rootDirectory = new(settings.CurrentValue.Cache.Path);
            if (rootDirectory.Exists)
            {
                var localPath = new FileInfo(Path.Combine(rootDirectory.FullName, descriptor.LocalPath));
                if (localPath.Exists)
                    return new ResourceHandle(localPath);
            }

            return default;
        }
    }

    public interface ICacheService
    {
        public ResourceHandle OpenHandle(ResourceDescriptor descriptor);
    }
}
