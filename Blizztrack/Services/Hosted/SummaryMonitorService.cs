using Blizztrack.Framework.Ribbit;
using Blizztrack.Options;
using Blizztrack.Persistence;
using Blizztrack.Persistence.Entities;
using Blizztrack.Shared.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.InteropServices;

using static Blizztrack.Program;

namespace Blizztrack.Services.Hosted
{
    using PersistedEndpoint = Persistence.Entities.Endpoint;

    /// <summary>
    /// A singleton service in charge of periodically querying product state for every product declared in <see cref="Settings.Products"/>.
    /// </summary>
    public class SummaryMonitorService(IOptionsMonitor<Settings> configurationMonitor, IServiceProvider serviceProvider) : BackgroundService
    {
        private readonly MediatorService _mediatorService = serviceProvider.GetRequiredService<MediatorService>();
        private readonly IOptionsMonitor<Settings> _settingsMonitor = configurationMonitor;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            PeriodicTimer periodicTimer = new(_settingsMonitor.CurrentValue.Ribbit.Interval);

            _ = _settingsMonitor.OnChange(configuration =>
            {
                if (configuration.Ribbit.Interval != periodicTimer.Period)
                    periodicTimer.Dispose();
            });

            // Staging buffer for sequence numbers of the currently tested product.
            var sequenceNumbers = new int[(int) Enum.GetValues<SequenceNumberType>().Length];

            while (!stoppingToken.IsCancellationRequested)
            {
                while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
                {
                    using var _ = ActivitySupplier.StartActivity("blizztrack.ribbit.summary");

                    sequenceNumbers.AsSpan().Clear(); // Reset the staging buffer

                    // Open a new scope and acquire a new database context for this tick.
                    // (This should not be needed but there is unfortunately no guarantee another service didn't update the database).
                    using var scope = serviceProvider.CreateScope();
                    var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                    var queryEndpoint = _settingsMonitor.CurrentValue.Ribbit.Endpoint;
                    var monitoredProducts = _settingsMonitor.CurrentValue.Products;

                    var summaryTable = await Commands.GetEndpointSummary(queryEndpoint.Host, queryEndpoint.Port, stoppingToken: stoppingToken);

                    // Add "summary" as a fake product.
                    if (await IsUpdatePublishable("summary", summaryTable.SequenceNumber, e => e.Version, databaseContext, stoppingToken))
                    {
                        // Update the "summary" (a fake product)
                        var updatedCount = await databaseContext.Products.Where(e => e.Code == "summary")
                            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Version, summaryTable.SequenceNumber), stoppingToken);
                        if (updatedCount == 0)
                        {
                            databaseContext.Products.Add(new()
                            {
                                Code = "summary",
                                Version = summaryTable.SequenceNumber,
                            });

                            await databaseContext.SaveChangesAsync(stoppingToken);
                        }
                    }

                    List<Func<ValueTask>> valuePublishers = [];

                    foreach (var (productCode, productUpdates) in summaryTable.Entries.GroupBy(e => e.Product))
                    {
                        // Product is not monitored, get out.
                        if (!monitoredProducts.Contains(productCode))
                            continue;

                        // Flatten sequence numbers.
                        sequenceNumbers.AsSpan().Clear();
                        foreach (var kv in productUpdates)
                            sequenceNumbers[(int) kv.Flags] = kv.SequenceNumber;
                        
                        // Get the product record.
                        var productState = databaseContext.Products
                            .Include(e => e.Endpoints)
                            .Include(e => e.Configurations)
                            .SingleOrDefault(e => e.Code == productCode);
                        var publishUpdate = productState?.CanPublishUpdate(sequenceNumbers) ?? true;

                        // CDN handling is special; We also want to check it if somehow there are no CDNs available for this product in database.
                        // If that's the case, try to update.
                        if (productState == null || productState.CDN != sequenceNumbers[(int) SequenceNumberType.CDN])
                        {
                            var (newProduct, eventSource) = await UpdateCDN(productCode, queryEndpoint, sequenceNumbers[(int) SequenceNumberType.CDN],
                                productState, databaseContext, stoppingToken);
                            if (newProduct is not null)
                                databaseContext.Products.Add(newProduct);

                            productState ??= newProduct;
                            valuePublishers.Add(eventSource);
                        }

                        if (productState == null || productState.Version != sequenceNumbers[(int) SequenceNumberType.Version])
                        {
                            var (newProduct, eventSource) = await UpdateVersion(productCode, queryEndpoint, sequenceNumbers[(int) SequenceNumberType.Version],
                                productState, databaseContext, stoppingToken);

                            if (newProduct is not null)
                                databaseContext.Products.Add(newProduct);

                            productState ??= newProduct;
                            valuePublishers.Add(eventSource);
                        }

                        if (productState == null || productState.BGDL != sequenceNumbers[(int) SequenceNumberType.BGDL])
                        {
                            var (newProduct, eventSource) = await UpdateBGDL(productCode, queryEndpoint, sequenceNumbers[(int) SequenceNumberType.BGDL],
                                productState, databaseContext, stoppingToken);

                            if (newProduct is not null)
                                databaseContext.Products.Add(newProduct);

                            productState ??= newProduct;
                            valuePublishers.Add(eventSource);
                        }

                        await databaseContext.SaveChangesAsync(stoppingToken);

                        // If the product record does not exist, or if the update can be published, send it out to the mediator service.
                        if (publishUpdate)
                            await _mediatorService.Products.OnSummary.Writer.WriteAsync((productCode, sequenceNumbers), stoppingToken);
                    }

                    foreach (var publisher in valuePublishers)
                        await publisher();
                }

                // If we got here, the timer got pulled by configuration; start a new one.
                periodicTimer = new(_settingsMonitor.CurrentValue.Ribbit.Interval);
            }
        }

        /// <summary>
        /// Updates CDN information on a product.
        /// </summary>
        /// <param name="productCode">The product that's getting an update.</param>
        /// <param name="queryEndpoint">The CDN endpoint that provided the update.</param>
        /// <param name="sequenceNumber">The newly seen sequence number.</param>
        /// <param name="cacheEntry">The product, if it already is in database.</param>
        /// <param name="stoppingToken">A cancellation token.</param>
        /// <returns>A product to insert. If the product needed to be updated, it will be.</returns>
        private async Task<(Product?, Func<ValueTask>)> UpdateCDN(string productCode, Options.Endpoint queryEndpoint, int sequenceNumber,
            Product? cacheEntry, 
            DatabaseContext databaseContext,
            CancellationToken stoppingToken)
        {
            using var _ = StartTaggedActivity("blizztrack.ribbit.cdns", 
                ("blizztrack.ribbit.product", productCode),
                ("blizztrack.ribbit.sequence", sequenceNumber));

            var cdns = await Commands.GetProductCDNs(productCode, queryEndpoint.Host, queryEndpoint.Port, stoppingToken: stoppingToken);
            if (cdns.SequenceNumber == sequenceNumber)
            {
                var isInsertion = cacheEntry is null;
                cacheEntry ??= new () {
                    Code = productCode
                };

                cacheEntry!.CDN = cdns.SequenceNumber;

                // Update the product: update the endpoints associated with it, using the CDN reply as source of truth.
                foreach (var (groupingKey, regions) in cdns.Entries
                    .SelectMany(e => e.Hosts.Select(h => (Host: h, e.Name, e.ConfigPath, e.Path)))
                    .GroupBy(e => (e.Host, e.ConfigPath, e.Path), e => e.Name))
                {
                    // Find an endpoint that matches; if none, create it.
                    var persistedEndpoint = cacheEntry.Endpoints.SingleOrDefault(e => e.Host == groupingKey.Host
                        && e.ConfigurationPath == groupingKey.ConfigPath
                        && e.DataPath == groupingKey.Path);
                    if (persistedEndpoint is null)
                    {
                        cacheEntry.Endpoints.Add(new()
                        {
                            Host = groupingKey.Host,
                            ConfigurationPath = groupingKey.ConfigPath,
                            DataPath = groupingKey.Path,
                            Regions = [.. regions]
                        });
                    }
                    else
                    {
                        string[] newRegions = [.. regions];

                        if (!persistedEndpoint.Regions.SequenceEqual(newRegions))
                            persistedEndpoint.Regions = newRegions;
                    }
                }

                return (
                    isInsertion ? cacheEntry : default,
                    () => _mediatorService.Products.OnCDNs.Writer.WriteAsync((productCode, cdns), stoppingToken)
                );
            }

            return (default, () => ValueTask.CompletedTask);
        }

        /// <summary>
        /// Updates version information on a Product.
        /// </summary>
        /// <param name="productCode">The product that's getting an update.</param>
        /// <param name="queryEndpoint">The CDN endpoint that provided the update.</param>
        /// <param name="sequenceNumber">The newly seen sequence number.</param>
        /// <param name="cacheEntry">The product, if it already is in database.</param>
        /// <param name="stoppingToken">A cancellation token.</param>
        /// <returns>A product to insert. If the product needed to be updated, it will be.</returns>
        private async Task<(Product?, Func<ValueTask>)> UpdateVersion(string productCode, Options.Endpoint queryEndpoint, int sequenceNumber,
            Product? cacheEntry,
            DatabaseContext databaseContext,
            CancellationToken stoppingToken)
        {
            using var _ = StartTaggedActivity("blizztrack.ribbit.cdns",
                ("blizztrack.ribbit.product", productCode),
                ("blizztrack.ribbit.sequence", sequenceNumber));

            var versions = await Commands.GetProductVersions(productCode, queryEndpoint.Host, queryEndpoint.Port, stoppingToken: stoppingToken);
            if (versions.SequenceNumber == sequenceNumber)
            {
                var isInsertion = cacheEntry is null;
                cacheEntry ??= new() {
                    Code = productCode
                };

                cacheEntry!.Version = versions.SequenceNumber;
                cacheEntry.Configurations.Clear();

                foreach (var version in versions.Entries.GroupBy(v => (v.BuildConfig, v.CDNConfig, v.KeyRing, v.ProductConfig, v.BuildID, v.VersionsName)))
                {
                    var regions = version.Select(v => v.Region);

                    var persistedConfiguration = cacheEntry.Configurations.SingleOrDefault(c => c.BuildConfig == version.Key.BuildConfig
                        && c.CDNConfig == version.Key.CDNConfig
                        && c.KeyRing == version.Key.KeyRing
                        && c.Config == version.Key.ProductConfig
                        && c.BuildID == version.Key.BuildID
                        && c.Name == version.Key.VersionsName);
                    
                    if (persistedConfiguration is null)
                    {
                        cacheEntry.Configurations.Add(new()
                        {
                            BuildConfig = version.Key.BuildConfig,
                            CDNConfig = version.Key.CDNConfig,
                            BuildID = version.Key.BuildID,
                            Config = version.Key.ProductConfig,
                            KeyRing = version.Key.KeyRing,
                            Regions = [.. regions],
                            Name = version.Key.VersionsName,
                            Product = cacheEntry!
                        });
                    }
                    else
                    {
                        // ?
                        int x = 1;
                    }
                }

                return (
                    isInsertion ? cacheEntry : default,
                    () => _mediatorService.Products.OnVersions.Writer.WriteAsync((productCode, versions), stoppingToken)
                );
            }

            return (default, () => ValueTask.CompletedTask);
        }

        /// <summary>
        /// Updates BGDL information on a Product.
        /// </summary>
        /// <param name="productCode">The product that's getting an update.</param>
        /// <param name="queryEndpoint">The CDN endpoint that provided the update.</param>
        /// <param name="sequenceNumber">The newly seen sequence number.</param>
        /// <param name="cacheEntry">The product, if it already is in database.</param>
        /// <param name="stoppingToken">A cancellation token.</param>
        /// <returns>A product to insert. If the product needed to be updated, it will be.</returns>
        private async Task<(Product?, Func<ValueTask>)> UpdateBGDL(string productCode, Options.Endpoint queryEndpoint, int sequenceNumber,
            Product? cacheEntry,
            DatabaseContext databaseContext,
            CancellationToken stoppingToken)
        {
            using var _ = StartTaggedActivity("blizztrack.ribbit.bgdl",
                ("blizztrack.ribbit.product", productCode),
                ("blizztrack.ribbit.sequence", sequenceNumber));


            var bgdl = await Commands.GetProductBGDL(productCode, queryEndpoint.Host, queryEndpoint.Port, stoppingToken: stoppingToken);
            if (bgdl.SequenceNumber == sequenceNumber)
            {
                var isInsertion = cacheEntry is null;
                cacheEntry ??= new() {
                    Code = productCode
                };

                cacheEntry!.BGDL = bgdl.SequenceNumber;

                return (
                    isInsertion ? cacheEntry : default,
                    () => _mediatorService.Products.OnVersions.Writer.WriteAsync((productCode, bgdl), stoppingToken)
                );
            }

            return (default, () => ValueTask.CompletedTask);
        }

        private static async Task<bool> IsUpdatePublishable(string productCode, int summarySequenceNumber, Expression<Func<Product, int>> fieldAccessor,
            DatabaseContext databaseContext, CancellationToken stoppingToken)
        {
            Expression<Func<Product, bool>> productFilter = e => e.Code == productCode;
            var entityParameter = Expression.Parameter(typeof(Product));

            var seqnFilter = Expression.Lambda<Func<Product, bool>>(
                Expression.AndAlso(
                    Expression.Equal(
                        Expression.Invoke(fieldAccessor, entityParameter),
                        Expression.Constant(summarySequenceNumber)
                    ),
                    Expression.Invoke(productFilter, entityParameter)
                ),
                entityParameter
            );

            return !await databaseContext.Products.AnyAsync(seqnFilter, stoppingToken);
        }
    }
}
