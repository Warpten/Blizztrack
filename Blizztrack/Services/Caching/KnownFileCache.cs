using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Framework.TACT.Views;
using Blizztrack.Options;

using Microsoft.Extensions.Options;

using ZiggyCreatures.Caching.Fusion;

namespace Blizztrack.Services.Caching
{
    using ContentKey = Framework.TACT.Views.ContentKey;
    using EncodingKey = Framework.TACT.Views.EncodingKey;

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

        public ValueTask<T> Obtain(string productCode, ContentKey contentKey, EncodingKey encodingKey, CancellationToken stoppingToken = default)
        {
            var ckey = contentKey.Upgrade();
            var ekey = encodingKey.Upgrade();

            return _cache.GetOrSetAsync($"{fileIdentifier}:{encodingKey.AsHexString()}",
                        token => OpenHandle(productCode, ekey, ckey, token),
                        new FusionCacheEntryOptions()
                        {
                            Duration = durationGetter(_settings.CurrentValue.Cache.Expirations),
                        }, stoppingToken);
        }

        public ValueTask<T> Obtain(string productCode, in EncodingKey encodingKey, CancellationToken stoppingToken = default)
        {
            var ekey = encodingKey.Upgrade();

            return _cache.GetOrSetAsync($"{fileIdentifier}:{encodingKey.AsHexString()}",
                        async token => await OpenHandle(productCode, ekey, token),
                        new FusionCacheEntryOptions()
                        {
                            Duration = durationGetter(_settings.CurrentValue.Cache.Expirations),
                        }, stoppingToken);
        }

        private Task<T> OpenHandle(string productCode, in EncodingKey encodingKey, in ContentKey contentKey, CancellationToken stoppingToken = default)
            => _resourceLocator.OpenCompressed<T>(productCode, encodingKey, contentKey, stoppingToken);

        private Task<T> OpenHandle(string productCode, in EncodingKey encodingKey, CancellationToken stoppingToken = default)
            => _resourceLocator.OpenCompressed<T>(productCode, encodingKey, stoppingToken);
    }

    public class EncodingCache(IServiceProvider serviceProvider) : KnownFileCache<Encoding>(serviceProvider, "encoding", static e => e.Encoding);
    public class InstallCache(IServiceProvider serviceProvider) : KnownFileCache<Install>(serviceProvider, "install", static e => e.Install);
    public class RootCache(IServiceProvider serviceProvider) : KnownFileCache<Root>(serviceProvider, "root", static e => e.Root);
}
