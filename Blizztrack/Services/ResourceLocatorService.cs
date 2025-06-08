using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;

namespace Blizztrack.Services
{
    /// <summary>
    /// Provides utility methods to access resources, optionally downloading them from Blizzard's CDNs.
    /// </summary>
    /// <param name="contentService"></param>
    /// <param name="localCache"></param>
    /// <param name="serviceProvider"></param>
    public class ResourceLocatorService(ContentService contentService, LocalCacheService localCache, IServiceProvider serviceProvider) : IResourceLocator
    {
        public async Task<ResourceHandle> OpenHandleAsync(string region, ResourceDescriptor descriptor, CancellationToken stoppingToken)
        {
            var outcome = localCache.OpenHandle(descriptor);
            if (outcome == default)
                await Transfer(await contentService.Query(region, descriptor, stoppingToken), descriptor);

            return localCache.OpenHandle(descriptor);
        }
        public async Task<ResourceHandle> OpenHandleAsync(ResourceDescriptor descriptor, CancellationToken stoppingToken)
        {
            using var scope = serviceProvider.CreateScope();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var endpoints = databaseContext.Endpoints.Select(e => new PatchEndpoint(e.Host, e.DataPath, e.ConfigurationPath))
                .ToAsyncEnumerable();

            var outcome = localCache.OpenHandle(descriptor);
            if (outcome == default)
                await Transfer(await contentService.Query(endpoints, descriptor, stoppingToken), descriptor);

            return localCache.OpenHandle(descriptor);
        }

        public async Task<ResourceHandle> OpenHandleAsync(IAsyncEnumerable<PatchEndpoint> endpoints, ResourceDescriptor descriptor, CancellationToken stoppingToken)
        {
            var outcome = localCache.OpenHandle(descriptor);
            if (outcome != default)
                return outcome;

            await Transfer(await contentService.Query(endpoints, descriptor, stoppingToken), descriptor);
            return localCache.OpenHandle(descriptor);
        }

        private async Task Transfer(ContentQueryResult queryResult, ResourceDescriptor descriptor)
        {
            var localPath = localCache.CreatePath(descriptor.LocalPath);
        
            using var targetStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 0, true);
            await queryResult.Body.CopyToAsync(targetStream);
        }
    }
}
