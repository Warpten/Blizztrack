
using Blizztrack.Extensions;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Persistence.Entities;
using Blizztrack.Services.Caching;

using Microsoft.EntityFrameworkCore;

using System.Runtime.CompilerServices;

namespace Blizztrack.Services.Hosted
{
    public class KnownFilesMonitorService(IServiceProvider serviceProvider, EncodingCache encodingRepository) : BackgroundService
    {
        private readonly IResourceLocator _resourceLocator = serviceProvider.GetRequiredService<IResourceLocator>();
        private readonly MediatorService _mediatorService = serviceProvider.GetRequiredService<MediatorService>();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var (product, versions) in _mediatorService.Products.OnVersions.Reader.ReadAllAsync(stoppingToken))
            {
                var buildTasks = versions.Entries
                    .Select(e => e.BuildConfig)
                    .Distinct()
                    .Select(async buildConfig =>
                    {
                        var resourceDescriptor = ResourceType.Config.ToDescriptor(product, buildConfig, ContentKey.Zero);
                        var configurationFile = await _resourceLocator.OpenHandle(resourceDescriptor, stoppingToken);

                        using var mappedData = configurationFile.ToMappedDataSource();
                        return BuildConfiguration.Parse(mappedData);
                    });

                using var executionScope = serviceProvider.CreateScope();
                var databaseContext = executionScope.ServiceProvider.GetRequiredService<DatabaseContext>();
                await foreach (var buildTask in Task.WhenEach(buildTasks))
                {
                    var buildConfiguration = await buildTask;

                    if (!databaseContext.KnownResources.Any(e => e.EncodingKey.SequenceEqual(buildConfiguration.Encoding.Encoding.Key)))
                    {
                        databaseContext.KnownResources.Add(new KnownResource()
                        {
                            Kind = "Encoding",
                            ContentKey = buildConfiguration.Encoding.Content.Key,
                            EncodingKey = buildConfiguration.Encoding.Encoding.Key,
                            Specification = "",
                        });

                        // Needs to be flushed to the database instantly
                        databaseContext.SaveChanges();
                    }

                    // Update well-known root ekey/ckey pairs
                    if (!databaseContext.KnownResources.Any(e => e.ContentKey.SequenceEqual(buildConfiguration.Root)))
                    {
                        var encoding = await encodingRepository.Obtain(product, buildConfiguration.Encoding.Content.Key, buildConfiguration.Encoding.Encoding.Key, stoppingToken);

                        var rootEntry = encoding.FindContentKey(buildConfiguration.Root);
                        if (rootEntry)
                        {
                            for (var i = 0; i < rootEntry.Count; ++i)
                            {
                                databaseContext.KnownResources.Add(new KnownResource()
                                {
                                    Kind = "Root",
                                    ContentKey = buildConfiguration.Root,
                                    EncodingKey = rootEntry[i].AsOwned(),
                                    Specification = "",
                                });
                            }
                        }
                    }
                }

            }
        }
    }
}
