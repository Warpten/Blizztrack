using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;

using Microsoft.Extensions.Options;

using ZiggyCreatures.Caching.Fusion;

namespace Blizztrack.Services.Caching
{
    /// <summary>
    /// A cache of various known resources.
    /// </summary>
    /// <typeparam name="T">The type of resource this cache monitors.</typeparam>
    /// <param name="serviceProvider"></param>
    /// <param name="fileIdentifier"></param>
    /// <param name="durationGetter"></param>
    public abstract class KnownFileCache<T>(IServiceProvider serviceProvider, string fileIdentifier, Func<ExpirySettings, TimeSpan> durationGetter)
        where T : class, IResourceParser<T>
    {
        private IOptionsMonitor<Settings> _settings = serviceProvider.GetRequiredService<IOptionsMonitor<Settings>>();
        private IResourceLocator _resourceLocator = serviceProvider.GetRequiredService<IResourceLocator>();

        private readonly FusionCache _cache = new (new FusionCacheOptions() {
            CacheName = $"cache:{fileIdentifier}",
        });

        public ValueTask<T> Obtain<C, E>(string productCode, C contentKey, E encodingKey, CancellationToken stoppingToken = default)
            where C : IContentKey<C>
            where E : IEncodingKey<E>
            => _cache.GetOrSetAsync($"{fileIdentifier}:{encodingKey.AsHexString()}",
                async token => await OpenHandle(productCode, encodingKey, contentKey, token),
                new FusionCacheEntryOptions() {
                    Duration = durationGetter(_settings.CurrentValue.Cache.Expirations),
                }, stoppingToken);

        public ValueTask<T> Obtain<E>(string productCode, E encodingKey, CancellationToken stoppingToken = default)
            where E : IEncodingKey<E>
            => _cache.GetOrSetAsync($"{fileIdentifier}:{encodingKey.AsHexString()}",
                async token => await OpenHandle(productCode, encodingKey, token),
                new FusionCacheEntryOptions() {
                    Duration = durationGetter(_settings.CurrentValue.Cache.Expirations),
                }, stoppingToken);

        private Task<T> OpenHandle<E, C>(string productCode, E encodingKey, C contentKey, CancellationToken stoppingToken = default)
            where C : IContentKey<C>
            where E : IEncodingKey<E>
            => _resourceLocator.OpenCompressed<E, C, T>(productCode, encodingKey, contentKey, stoppingToken);

        private Task<T> OpenHandle<E>(string productCode, E encodingKey, CancellationToken stoppingToken = default)
            where E : IEncodingKey<E>
            => _resourceLocator.OpenCompressed<E, T>(productCode, encodingKey, stoppingToken);
    }

    public class EncodingCache(IServiceProvider serviceProvider) : KnownFileCache<Encoding>(serviceProvider, "encoding", static e => e.Encoding);
    public class InstallCache(IServiceProvider serviceProvider) : KnownFileCache<Install>(serviceProvider, "install", static e => e.Install);
    public class RootCache(IServiceProvider serviceProvider) : KnownFileCache<Root>(serviceProvider, "root", static e => e.Root);
}
