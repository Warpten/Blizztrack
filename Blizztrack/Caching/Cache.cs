using Microsoft.Extensions.Caching.Memory;

namespace Blizztrack.Caching
{
    public abstract class BackgroundWorker<T>(string cacheIdentifier, IServiceProvider serviceProvider, TimeSpan updateInterval) : BackgroundService()
    {
        private readonly CacheSignal<T> _cacheSignal = serviceProvider.GetRequiredService<CacheSignal<T>>();
        private readonly IMemoryCache _cache = serviceProvider.GetRequiredService<IMemoryCache>();
        private bool _initialized = false;

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await _cacheSignal.WaitAsync();
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var entries = await Update(stoppingToken);
                    if (entries.Length > 0)
                        _cache.Set(cacheIdentifier, entries);
                }
                finally
                {
                    if (!_initialized)
                        _cacheSignal.Release();

                    _initialized = true;
                }

                await Task.Delay(updateInterval, stoppingToken);
            }
        }

        protected abstract ValueTask<T[]> Update(CancellationToken stoppingToken);
    }

    public class BackgroundService<T>(string cacheIdentifier, IServiceProvider serviceProvider)
    {
        private readonly CacheSignal<T> _cacheSignal = serviceProvider.GetRequiredService<CacheSignal<T>>();
        private readonly IMemoryCache _cache = serviceProvider.GetRequiredService<IMemoryCache>();

        public async IAsyncEnumerable<T> Enumerate(Func<T, bool>? predicate = null)
        {
            try
            {
                await _cacheSignal.WaitAsync();

                T[] entries = (await _cache.GetOrCreateAsync(cacheIdentifier, _ => Task.FromResult(Array.Empty<T>())))!;
                predicate ??= _ => true;

                foreach (var entry in entries)
                    if (predicate(entry))
                        yield return entry;
            }
            finally
            {
                _cacheSignal.Release();
            }
        }
    }

    public static class CacheExtensions
    {
        public delegate void InitializeHttp(IHttpClientBuilder builder);

        public static IServiceCollection RegisterCache<T, TWorker, TService>(this IServiceCollection services, InitializeHttp? builder = default)
            where TWorker : BackgroundWorker<T>
            where TService : BackgroundService<T>
        {
            if (builder != default)
                builder(services.AddHttpClient<TWorker>());

            services.AddHostedService<TWorker>();
            services.AddScoped<TService>();
            services.AddSingleton<CacheSignal<T>>();

            return services;
        }

        public static IServiceCollection RegisterCache<T, TWorker, TService>(this IServiceCollection services, Func<IServiceProvider, TService> serviceFactory,
            InitializeHttp? builder = default)
            where TWorker : BackgroundWorker<T>
            where TService : BackgroundService<T>
        {
            if (builder != default)
                builder(services.AddHttpClient<TWorker>());

            services.AddHostedService<TWorker>();
            services.AddScoped(serviceFactory);
            services.AddSingleton<CacheSignal<T>>();

            return services;
        }

        public static IServiceCollection RegisterCache<T, TWorker, TService>(this IServiceCollection services, Func<IServiceProvider, IList<TService>> serviceFactory,
            InitializeHttp? builder = default)
            where TWorker : BackgroundWorker<T>
            where TService : BackgroundService<T>
        {
            if (builder != default)
                builder(services.AddHttpClient<TWorker>());

            services.AddHostedService<TWorker>();
            services.AddScoped(serviceFactory);
            services.AddSingleton<CacheSignal<T>>();

            return services;
        }
    }
}
