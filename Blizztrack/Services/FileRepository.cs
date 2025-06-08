using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;

using Microsoft.Extensions.Options;

using ZiggyCreatures.Caching.Fusion;

namespace Blizztrack.Services
{

    public abstract class FileRepository<T>(IServiceProvider serviceProvider, string fileIdentifier, Func<ExpirySettings, TimeSpan> durationGetter)
    {
        private IOptionsMonitor<Settings> _settings = serviceProvider.GetRequiredService<IOptionsMonitor<Settings>>();
        private IResourceLocator _resourceLocator = serviceProvider.GetRequiredService<IResourceLocator>();

        private readonly FusionCache _cache = new (new FusionCacheOptions() {
            CacheName = $"cache:{fileIdentifier}",
        });

        public ValueTask<T> Obtain<K>(K encodingKey, CancellationToken stoppingToken) where K : IEncodingKey<K>, IKey<K>, allows ref struct
            => Obtain(encodingKey.AsHexString(), stoppingToken);

        public ValueTask<T> Obtain(string encodingKey, CancellationToken stoppingToken)
            => _cache.GetOrSetAsync($"{fileIdentifier}:{encodingKey}", async token => await OpenHandle(encodingKey, token), new FusionCacheEntryOptions() {
                Duration = durationGetter(_settings.CurrentValue.Cache.Expirations),
            }, stoppingToken);

        private async Task<T> OpenHandle(string key, CancellationToken stoppingToken)
        {
            var descriptor = new ResourceDescriptor(ResourceType.Data, key);
            var resourceHandle = await _resourceLocator.OpenHandleAsync(descriptor, stoppingToken);

            return Open(resourceHandle);
        }

        protected abstract T Open(ResourceHandle resourceHandle);
    }
}
