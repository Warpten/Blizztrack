using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;

using Microsoft.Extensions.Options;

using ZiggyCreatures.Caching.Fusion;

namespace Blizztrack.Services
{

    public abstract class FileRepository<T>(IServiceProvider serviceProvider, string fileIdentifier, Func<ExpirySettings, TimeSpan> durationGetter)
        where T : class, IResourceParser<T>
    {
        private IOptionsMonitor<Settings> _settings = serviceProvider.GetRequiredService<IOptionsMonitor<Settings>>();
        private IResourceLocator _resourceLocator = serviceProvider.GetRequiredService<IResourceLocator>();

        private readonly FusionCache _cache = new (new FusionCacheOptions() {
            CacheName = $"cache:{fileIdentifier}",
        });

        public ValueTask<T> Obtain<C, E>(string productCode, C contentKey, E encodingKey, CancellationToken stoppingToken)
            where C : IContentKey<C>, IKey<C>
            where E : IEncodingKey<E>, IKey<E>
            => _cache.GetOrSetAsync($"{fileIdentifier}:{encodingKey.AsHexString()}",
                async token => await OpenHandle(productCode, encodingKey, contentKey, token),
                new FusionCacheEntryOptions() {
                    Duration = durationGetter(_settings.CurrentValue.Cache.Expirations),
                }, stoppingToken);

        public ValueTask<T> Obtain<E>(string productCode, E encodingKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey<E>
            => _cache.GetOrSetAsync($"{fileIdentifier}:{encodingKey.AsHexString()}",
                async token => await OpenHandle(productCode, encodingKey, token),
                new FusionCacheEntryOptions()
                {
                    Duration = durationGetter(_settings.CurrentValue.Cache.Expirations),
                }, stoppingToken);

        private Task<T> OpenHandle<E, C>(string productCode, E encodingKey, C contentKey, CancellationToken stoppingToken)
            where C : IContentKey<C>, IKey<C>
            where E : IEncodingKey<E>, IKey<E>
        {
            return _resourceLocator.OpenCompressed<E, C, T>(productCode, encodingKey, contentKey, stoppingToken);
        }
        private Task<T> OpenHandle<E>(string productCode, E encodingKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey<E>
        {
            return _resourceLocator.OpenCompressed<T, E>(productCode, encodingKey, stoppingToken);
        }

        protected abstract T Open(ResourceHandle resourceHandle);
    }
}
