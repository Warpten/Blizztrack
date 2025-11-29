using Blizztrack.Framework.Extensions.Services;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;
using Blizztrack.Persistence;
using Blizztrack.Services.Caching;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;
using Polly.Telemetry;

using System.Diagnostics;
using System.Net;

using static Blizztrack.Program;

namespace Blizztrack.Services
{
    using Views = Framework.TACT.Views;

    /// <summary>
    /// Provides utility methods to access resources, optionally downloading them from Blizzard's CDNs.
    /// </summary>
    /// <param name="clientFactory"></param>
    /// <param name="localCache"></param>
    /// <param name="serviceProvider"></param>
    public class ResourceLocatorService : AbstractResourceLocatorService
    {
        private readonly LocalCacheService _localCache;
        private readonly DatabaseContext _databaseContext;
        private readonly IOptionsMonitor<Settings> _settings;

        //! TODO: Nop this out if non-telemetry enabled builds become a thing
        private static Activity? BeginActivity(string activityCode, string productCode, in Views.EncodingKey encodingKey, in Views.ContentKey contentKey)
        {
            var activity = ActivitySupplier.StartActivity(activityCode);
            if (activity is not null && activity.IsAllDataRequested)
            {
                activity.SetTag("blizztrack.blte.product", productCode);
                activity.SetTag("blizztrack.blte.ckey", contentKey.AsHexString());
                activity.SetTag("blizztrack.blte.ekey", encodingKey.AsHexString());
            }

            return activity;
        }

        public ResourceLocatorService(IHttpClientFactory clientFactory, IServiceProvider serviceProvider) : base(clientFactory)
        {
            _localCache = serviceProvider.GetRequiredService<LocalCacheService>();
            _settings = serviceProvider.GetRequiredService<IOptionsMonitor<Settings>>();

            var scope = serviceProvider.CreateScope();
            _databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        }

        public override Task<ResourceHandle> OpenCompressedHandle(string productCode, in Views.EncodingKey encodingKey, in Views.ContentKey contentKey, CancellationToken stoppingToken)
        {
            using var activity = BeginActivity("blizztrack.resources.open_compressed_handle", productCode, encodingKey, contentKey);

            return base.OpenCompressedHandle(productCode, encodingKey, contentKey, stoppingToken);
        }

        protected override ResiliencePipeline<ContentQueryResult> AcquisitionPipeline { get; } = new ResiliencePipelineBuilder<ContentQueryResult>()
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
        protected override IList<PatchEndpoint> GetEndpoints(string productCode, string region = "xx")
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

        public override ResourceHandle OpenLocalHandle(ResourceDescriptor resourceDescriptor)
            => _localCache.OpenHandle(resourceDescriptor);

        public override ResourceHandle CreateLocalHandle(ResourceDescriptor resourceDescriptor, byte[] fileData)
        {
            _localCache.Write(resourceDescriptor.LocalPath, fileData);
            return OpenLocalHandle(resourceDescriptor);
        }

        public override async Task<ResourceHandle> OpenCompressedHandle(ResourceDescriptor compressedDescriptor, CancellationToken stoppingToken)
        {
            // Look for this resource in the well known table.
            // If it's well known, creeate a file on disk if it doesn't exist, decompressed the resource
            // in it, and call the decompressed loader. Otherwise, call the compressed loader.
            var knownResource = _databaseContext.KnownResources
                .SingleOrDefault(e => e.EncodingKey.SequenceEqual(compressedDescriptor.Archive));

            if (knownResource is not null)
            {
                var decompressedDescriptor = ResourceType.Decompressed.ToDescriptor(compressedDescriptor.Product, knownResource.EncodingKey, knownResource.ContentKey);

                return await OpenCompressedHandleImpl(compressedDescriptor, decompressedDescriptor, stoppingToken);
            }
            else
            {
                // Not a known resource... Just use the compressed handler.
                return await OpenHandle(compressedDescriptor, stoppingToken);
            }
        }

        // VALIDATED IMPLEMENTATION DETAIL
        private async Task<ResourceHandle> OpenCompressedHandleImpl(ResourceDescriptor compressed, ResourceDescriptor decompressed, CancellationToken stoppingToken)
        {
            var decompressedHandle = _localCache.OpenHandle(decompressed);
            if (decompressedHandle != default)
                return decompressedHandle;

            // Create the decompressed resource now.
            var compressedHandle = await OpenHandle(compressed, stoppingToken);
            var decompressedData = BLTE.Parse(compressedHandle);

            decompressedHandle.Create(decompressedData);
            return decompressedHandle;
        }

        public override ContentKey ResolveContentKey(in Views.EncodingKey encodingKey)
        {
            var ekey = encodingKey.Upgrade();

            var knownResource = _databaseContext.KnownResources
                .SingleOrDefault(e => e.EncodingKey.SequenceEqual(ekey));
            return knownResource?.ContentKey ?? default;
        }
    }
}
