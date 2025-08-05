
using Blizztrack.Extensions;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Persistence.Entities;

using System.Runtime.CompilerServices;

namespace Blizztrack.Services.Hosted
{
    public class KnownFilesMonitorService(IServiceProvider serviceProvider) : BackgroundService
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
                        var resourceDescriptor = ResourceType.Config.ToDescriptor(product, buildConfig);
                        var configurationFile = await _resourceLocator.OpenHandle(resourceDescriptor, stoppingToken);

                        using var mappedData = configurationFile.ToMappedDataSource();
                        return BuildConfiguration.Parse(mappedData);
                    });

                using var executionScope = serviceProvider.CreateScope();
                var databaseContext = executionScope.ServiceProvider.GetRequiredService<DatabaseContext>();
                await foreach (var buildTask in Task.WhenEach(buildTasks))
                {
                    var buildConfiguration = await buildTask;

                    var entity = databaseContext.KnownResources.SingleOrDefault(e => e.EncodingKey == buildConfiguration.Encoding.Encoding.Key);
                    if (entity != null)
                        continue;

                    databaseContext.KnownResources.Add(new KnownResource()
                    {
                        ContentKey = buildConfiguration.Encoding.Content.Key,
                        EncodingKey = buildConfiguration.Encoding.Encoding.Key,
                        Specification = "",
                    });
                }

                databaseContext.SaveChanges();
            }
        }
    }
}
