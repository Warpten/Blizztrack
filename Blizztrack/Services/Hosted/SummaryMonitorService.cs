using Blizztrack.Framework.Ribbit;
using Blizztrack.Options;
using Blizztrack.Persistence;
using Blizztrack.Persistence.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using System.Linq.Expressions;
using System.Net;
using System.Runtime.InteropServices;

namespace Blizztrack.Services.Hosted
{
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
            var sequenceNumbers = new int[Enum.GetValues<SequenceNumberType>().Length];

            while (!stoppingToken.IsCancellationRequested)
            {
                while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
                {
                    sequenceNumbers.AsSpan().Clear(); // Reset the staging buffer

                    // Open a new scope and acquire a new database context for this tick.
                    // (This should not be needed but there is unfortunately no guarantee another service didn't update the database).
                    using var scope = serviceProvider.CreateScope();
                    var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                    var queryEndpoint = _settingsMonitor.CurrentValue.Ribbit.Endpoint;
                    var monitoredProducts = _settingsMonitor.CurrentValue.Products;

                    var summaryTable = await Commands.GetEndpointSummary(queryEndpoint.Host, queryEndpoint.Port, stoppingToken);

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

                    var productConfigStaging = new List<ProductConfig>();
                    var productStaging = databaseContext.Products.ToDictionary(e => e.Code);
                    var endpointStaging = databaseContext.Endpoints.ToDictionary(e => (e.Host, e.ConfigurationPath, e.DataPath));

                    foreach (var productUpdate in summaryTable.Entries.GroupBy(e => e.Product))
                    {
                        // Product is not monitored, get out.
                        if (!monitoredProducts.Contains(productUpdate.Key))
                            continue;

                        // Flatten sequence numbers.
                        sequenceNumbers.AsSpan().Clear();
                        foreach (var kv in productUpdate)
                            sequenceNumbers[(int) kv.Flags] = kv.SequenceNumber;
                        
                        // Can't use GetValueRefOrAddDefault because of await boundaries...
                        var entityExists = productStaging.TryGetValue(productUpdate.Key, out var cacheEntry);
                        if (!entityExists) 
                            productStaging.Add(productUpdate.Key, cacheEntry = new Product() { Code = productUpdate.Key });

                        if (cacheEntry!.CanPublishUpdate(sequenceNumbers))
                            await _mediatorService.Products.OnSummary.Writer.WriteAsync((productUpdate.Key, sequenceNumbers), stoppingToken);

                        // CDN handling is special; We also want to check it if somehow there are no CDNs available for this product in database.
                        // If that's the case, try to update.
                        if (!entityExists || sequenceNumbers[(int) SequenceNumberType.CDN] != cacheEntry!.CDN)
                        {
                            var cdns = await Commands.GetProductCDNs(productUpdate.Key, queryEndpoint.Host, queryEndpoint.Port, stoppingToken);
                            if (cdns.SequenceNumber == sequenceNumbers[(int) SequenceNumberType.CDN])
                            {
                                // Coherent data; update.
                                cacheEntry!.CDN = cdns.SequenceNumber;

                                await _mediatorService.Products.OnCDNs.Writer.WriteAsync((productUpdate.Key, cdns), stoppingToken);

                                // Also populate endpoints
                                cacheEntry.Endpoints.Clear();
                                foreach (var endpoint in cdns.Entries.SelectMany(e => e.Hosts.Select(h => (Host: h, e.Name, e.ConfigPath, e.Path)))
                                    .GroupBy(e => (e.Host, e.ConfigPath, e.Path), e => e.Name))
                                {
                                    ref var endpointCacheEntry = ref CollectionsMarshal.GetValueRefOrAddDefault(endpointStaging, endpoint.Key, out var endpointExists);
                                    if (!endpointExists)
                                        endpointCacheEntry = new() { Host = endpoint.Key.Host, ConfigurationPath = endpoint.Key.ConfigPath, DataPath = endpoint.Key.Path };

                                    endpointCacheEntry!.Regions = [.. endpoint];

                                    cacheEntry.Endpoints.Add(endpointCacheEntry);
                                }
                            }
                        }

                        if (!entityExists || sequenceNumbers[(int) SequenceNumberType.Version] != cacheEntry!.Version)
                        {
                            var versions = await Commands.GetProductVersions(productUpdate.Key, queryEndpoint.Host, queryEndpoint.Port, stoppingToken);
                            if (versions.SequenceNumber == sequenceNumbers[(int) SequenceNumberType.Version])
                            {

                                cacheEntry!.Version = versions.SequenceNumber;

                                await _mediatorService.Products.OnVersions.Writer.WriteAsync((productUpdate.Key, versions), stoppingToken);

                                // Look through the PSV file, aggregating rows that are identical (diregarding the region), and store any new tuple in database.
                                foreach (var version in versions.Entries.GroupBy(v => (v.BuildConfig, v.CDNConfig, v.KeyRing, v.ProductConfig, v.BuildID, v.VersionsName)))
                                {
                                    var regions = version.Select(v => v.Region);

                                    if (!databaseContext.Configs.Any(c => c.BuildConfig == version.Key.BuildConfig
                                        && c.CDNConfig == version.Key.CDNConfig
                                        && c.KeyRing == version.Key.KeyRing
                                        && c.Config == version.Key.ProductConfig
                                        && c.BuildID == version.Key.BuildID
                                        && c.Name == version.Key.VersionsName))
                                    {
                                        // New entry.
                                        productConfigStaging.Add(new ProductConfig()
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

                                }
                            }
                        }

                        if (!entityExists || sequenceNumbers[(int) SequenceNumberType.BGDL] != cacheEntry!.BGDL)
                        {
                            var bgdl = await Commands.GetProductBGDL(productUpdate.Key, queryEndpoint.Host, queryEndpoint.Port, stoppingToken);
                            if (bgdl.SequenceNumber == sequenceNumbers[(int) SequenceNumberType.BGDL])
                            {
                                cacheEntry!.BGDL = bgdl.SequenceNumber;

                                await _mediatorService.Products.OnBGDL.Writer.WriteAsync((productUpdate.Key, bgdl), stoppingToken);
                            }
                        }
                    }

                    databaseContext.Endpoints.UpdateRange(endpointStaging.Values);
                    databaseContext.Products.UpdateRange(productStaging.Values);
                    databaseContext.Configs.UpdateRange(productConfigStaging);
                    await databaseContext.SaveChangesAsync(stoppingToken);
                }

                // If we got here, the timer got pulled by configuration; start a new one.
                periodicTimer = new(_settingsMonitor.CurrentValue.Ribbit.Interval);
            }
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
