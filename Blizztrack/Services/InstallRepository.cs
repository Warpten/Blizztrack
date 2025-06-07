using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;

using Microsoft.Extensions.Caching.Memory;

using ZiggyCreatures.Caching.Fusion;

namespace Blizztrack.Services
{
    public class InstallRepository(ResourceLocatorService resourceLocator)
    {
        private readonly FusionCache _cache = new FusionCache(new FusionCacheOptions()
        {
            CacheName = nameof(InstallRepository),
        });

        public ValueTask<Install> Obtain<K>(K encodingKey, CancellationToken stoppingToken) where K : IEncodingKey<K>, IKey<K>, allows ref struct
            => Obtain(encodingKey.AsHexString(), stoppingToken);

        public ValueTask<Install> Obtain(string encodingKey, CancellationToken stoppingToken)
            => _cache.GetOrSetAsync($"install:{encodingKey}", async token => await ObtainInstall(encodingKey, token), new FusionCacheEntryOptions() {
                Duration = TimeSpan.FromMinutes(5),
            }, stoppingToken);

        private async Task<Install> ObtainInstall(string key, CancellationToken stoppingToken)
        {
            var descriptor = new ResourceDescriptor(ResourceType.Data, key);
            var resourceHandle = await resourceLocator.OpenHandleAsync(descriptor, stoppingToken);

            var decompressedArchive = BLTE.Parse(resourceHandle);
            return Install.Open(new InMemoryDataSupplier(decompressedArchive));
        }
    }
}
