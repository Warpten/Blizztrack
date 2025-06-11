using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;
using Blizztrack.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;
using Polly.Telemetry;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

using static Blizztrack.Program;

namespace Blizztrack.Services
{
    file class TransferContext
    {
        public required IEnumerator<PatchEndpoint> Endpoints { get; init; }
        public required HttpClient Client { get; init; }

        public required RangeHeaderValue? Range { get; init; }
    }

    public readonly record struct ContentQueryResult(HttpStatusCode StatusCode, Stream Body);

    /// <summary>
    /// Provides utility methods to access resources, optionally downloading them from Blizzard's CDNs.
    /// </summary>
    /// <param name="clientFactory"></param>
    /// <param name="localCache"></param>
    /// <param name="serviceProvider"></param>
    public class ResourceLocatorService : IResourceLocator
    {
        private readonly LocalCacheService _localCache;
        private readonly DatabaseContext _databaseContext;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IOptionsMonitor<Settings> _settings;

        public ResourceLocatorService(IHttpClientFactory clientFactory, IServiceProvider serviceProvider)
        {
            _clientFactory = clientFactory;
            _localCache = serviceProvider.GetRequiredService<LocalCacheService>();
            _settings = serviceProvider.GetRequiredService<IOptionsMonitor<Settings>>();

            var scope = serviceProvider.CreateScope();
            _databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        }

        // VALIDATED API
        public async Task<ResourceHandle> OpenHandle(ResourceDescriptor resourceDescriptor, CancellationToken stoppingToken)
        {
            var localHandle = _localCache.OpenHandle(resourceDescriptor);
            if (localHandle.Exists)
                return localHandle;

            var endpoints = GetEndpoints(resourceDescriptor.Product);
            var backendQuery = await ExecuteQuery(endpoints, resourceDescriptor, stoppingToken);
            if (backendQuery.StatusCode != HttpStatusCode.OK)
                return default;

            { // Create a stream from the local handle.
                using var fileStream = new FileStream(localHandle.Path, FileMode.Create, FileAccess.Write, FileShare.None, 0, true);
                await backendQuery.Body.CopyToAsync(fileStream, stoppingToken);
                fileStream.Dispose();
            }

            return _localCache.OpenHandle(resourceDescriptor);
        }

        // VALIDATED API
        public Task<T> OpenCompressed<E, C, T>(string productCode, E encodingKey, C contentKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey, allows ref struct
            where C : IContentKey<C>, IKey, allows ref struct
            where T : class, IResourceParser<T>
        {
            using var activity = ActivitySupplier.StartActivity("blizztrack.resources.open_compressed");
            if (activity is not null && activity.IsAllDataRequested) {
                activity.SetTag("blizztrack.blte.product", productCode);
                activity.SetTag("blizztrack.blte.ckey", contentKey.AsHexString());
                activity.SetTag("blizztrack.blte.ekey", encodingKey.AsHexString());
            }

            var compressedDescriptor = new ResourceDescriptor(ResourceType.Data, productCode, encodingKey.AsHexString());
            var decompressedDescriptor = new ResourceDescriptor(ResourceType.Decompressed, productCode, contentKey.AsHexString());
            return OpenCompressedImpl<T>(compressedDescriptor, decompressedDescriptor, stoppingToken);
        }

        public Task<ResourceHandle> OpenCompressedHandle<E, C>(string productCode, E encodingKey, C contentKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey, allows ref struct
            where C : IContentKey<C>, IKey, allows ref struct
        {
            using var activity = ActivitySupplier.StartActivity("blizztrack.resources.open_compressed");
            if (activity is not null && activity.IsAllDataRequested)
            {
                activity.SetTag("blizztrack.blte.product", productCode);
                activity.SetTag("blizztrack.blte.ckey", contentKey.AsHexString());
                activity.SetTag("blizztrack.blte.ekey", encodingKey.AsHexString());
            }

            var compressedDescriptor = new ResourceDescriptor(ResourceType.Data, productCode, encodingKey.AsHexString());
            var decompressedDescriptor = new ResourceDescriptor(ResourceType.Decompressed, productCode, contentKey.AsHexString());
            return OpenCompressedHandleImpl(compressedDescriptor, decompressedDescriptor, stoppingToken);
        }

        // VALIDATED API
        public Task<T> OpenCompressed<T, E>(string productCode, E encodingKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey, allows ref struct
            where T : class, IResourceParser<T>
            => OpenCompressedImpl<T>(new ResourceDescriptor(ResourceType.Data, productCode, encodingKey.AsHexString()), stoppingToken);

        // VALIDATED API
        public Task<ResourceHandle> OpenCompressedHandle<E>(string productCode, E encodingKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey, allows ref struct
            => OpenCompressedHandle(new ResourceDescriptor(ResourceType.Data, productCode, encodingKey.AsHexString()), stoppingToken);

        public async Task<ResourceHandle> OpenCompressedHandle(ResourceDescriptor compressedDescriptor, CancellationToken stoppingToken)
        {
            // Look for this resource in the well known table.
            // If it's well known, creeate a file on disk if it doesn't exist, decompressed the resource
            // in it, and call the decompressed loader. Otherwise, call the compressed loader.
            var knownResource = _databaseContext.KnownResources
                .SingleOrDefault(e => e.EncodingKey.AsHexString() == compressedDescriptor.ArchiveName);

            if (knownResource is not null)
            {
                var decompressedDescriptor = new ResourceDescriptor(ResourceType.Decompressed, compressedDescriptor.Product, knownResource.ContentKey.AsHexString());

                return await OpenCompressedHandleImpl(compressedDescriptor, decompressedDescriptor, stoppingToken);
            }
            else
            {
                // Not a known resource... Just use the compressed handler.
                return await OpenHandle(compressedDescriptor, stoppingToken);
            }
        }

        // VALIDATED IMPLEMENTATION DETAIL
        private async Task<ResourceHandle> OpenCompressedHandleImpl(ResourceDescriptor compressedDescriptor, ResourceDescriptor decompressedDescriptor, CancellationToken stoppingToken)
        {
            var decompressedHandle = _localCache.OpenHandle(decompressedDescriptor);
            if (decompressedHandle != default)
                return decompressedHandle;

            // Create the decompressed resource now.
            var compressedHandle = await OpenHandle(compressedDescriptor, stoppingToken);
            var decompressedData = BLTE.Parse(compressedHandle);

            decompressedHandle.Create(decompressedData);
            return decompressedHandle;
        }

        // VALIDATED IMPLEMENTATION DETAIL
        private async Task<T> OpenCompressedImpl<T>(ResourceDescriptor compressedDescriptor, CancellationToken stoppingToken)
            where T : class, IResourceParser<T>
        {
            // Look for this resource in the well known table.
            // If it's well known, creeate a file on disk if it doesn't exist, decompressed the resource
            // in it, and call the decompressed loader. Otherwise, call the compressed loader.
            var knownResource = _databaseContext.KnownResources
                .SingleOrDefault(e => e.EncodingKey.AsHexString() == compressedDescriptor.ArchiveName);

            if (knownResource is not null)
            {
                var decompressedDescriptor = new ResourceDescriptor(ResourceType.Decompressed, compressedDescriptor.Product, knownResource.ContentKey.AsHexString());

                return await OpenCompressedImpl<T>(compressedDescriptor, decompressedDescriptor, stoppingToken);
            }
            else
            {
                // Not a known resource... Just use the compressed handler.
                var compressedHandle = await OpenHandle(compressedDescriptor, stoppingToken);
                return T.OpenCompressedResource(compressedHandle);
            }
        }

        // VALIDATED IMPLEMENTATION DETAIL
        private async Task<T> OpenCompressedImpl<T>(ResourceDescriptor compressedDescriptor, ResourceDescriptor decompressedDescriptor, CancellationToken stoppingToken)
            where T : class, IResourceParser<T>
        {
            var decompressedHandle = _localCache.OpenHandle(decompressedDescriptor);
            if (decompressedHandle != default)
                return T.OpenResource(decompressedHandle);

            // Create the decompressed resource now.
            var compressedHandle = await OpenHandle(compressedDescriptor, stoppingToken);
            var decompressedData = BLTE.Parse(compressedHandle);

            _localCache.Write(decompressedDescriptor.LocalPath, decompressedData);
            return T.OpenResource(_localCache.OpenHandle(decompressedDescriptor));
        }

        private readonly ResiliencePipeline<ContentQueryResult> _acquisitionPipeline = new ResiliencePipelineBuilder<ContentQueryResult>()
            .AddConcurrencyLimiter(permitLimit: 20, queueLimit: 10)
            .AddRetry(new RetryStrategyOptions<ContentQueryResult>()
            {
                BackoffType = DelayBackoffType.Constant,
                MaxDelay = TimeSpan.Zero,
                MaxRetryAttempts = int.MaxValue,
                ShouldHandle = static args => args.Outcome switch
                {
                    { Exception: not null } => PredicateResult.True(),
                    { Result.StatusCode: HttpStatusCode.NotFound } => PredicateResult.False(),
                    _ => PredicateResult.False()
                }
            })
            .ConfigureTelemetry(new TelemetryOptions()
            {
                LoggerFactory = LoggerFactory.Create(options => options.AddOpenTelemetry())
            })
            .Build();

        /// <summary>
        /// Gets all endpoints that match the product (and optionally the region) provided.
        /// </summary>
        /// <param name="productCode"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        private IEnumerable<PatchEndpoint> GetEndpoints(string productCode, string region = "xx")
        {
            var endpointsQuery = _databaseContext.Endpoints.Include(e => e.Products).Where(e => e.Products.Any(p => p.Code == productCode));
            if (region != "xx")
                endpointsQuery = endpointsQuery.Where(e => e.Regions.Contains(region));

            var endpoints = endpointsQuery.Select(e => new PatchEndpoint(e.Host, e.DataPath, e.ConfigurationPath));

            var configurationEndpoints = _settings.CurrentValue.Cache.CDNs
                .Where(c => c.Products.Contains(productCode))
                .SelectMany(c => c.Hosts.Select(h => new PatchEndpoint(h, c.Data, c.Configuration)));

            return [.. endpoints, .. configurationEndpoints];
        }

        /// <summary>
        /// Queries the given descriptor from the first endpoint that responds successfully.
        /// </summary>
        /// <param name="hosts"></param>
        /// <param name="descriptor"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private async ValueTask<ContentQueryResult> ExecuteQuery(IEnumerable<PatchEndpoint> hosts, ResourceDescriptor descriptor, CancellationToken stoppingToken)
        {
            Debug.Assert(descriptor.Type != ResourceType.Decompressed, "Decompressed descriptors can't be acquired from CDNs.");

            var transferContext = new TransferContext()
            {
                Client = _clientFactory.CreateClient(),
                Range = descriptor.Offset != 0
                    ? new RangeHeaderValue(descriptor.Offset, descriptor.Offset + descriptor.Length)
                    : default,
                Endpoints = hosts.GetEnumerator(),
            };

            var resilienceContext = ResilienceContextPool.Shared.Get(stoppingToken);
            var result = await _acquisitionPipeline.ExecuteOutcomeAsync(async (context, state) =>
            {
                if (!state.Endpoints.MoveNext())
                    return Outcome.FromResult(new ContentQueryResult(HttpStatusCode.NotFound, Stream.Null));

                var server = state.Endpoints.Current;
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"http://{server.Host}/{server.DataStem}/{descriptor.RemotePath}")
                {
                    Headers = { Range = state.Range },
                };

                var response = await state.Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, stoppingToken);
                response.EnsureSuccessStatusCode();

                var dataStream = await response.Content.ReadAsStreamAsync();
                var transferInformation = new ContentQueryResult(response.StatusCode, dataStream);

                return Outcome.FromResult(transferInformation);
            }, resilienceContext, transferContext);

            return result.Result;
        }
    }
}
