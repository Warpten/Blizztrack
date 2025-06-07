namespace Blizztrack.Framework.TACT.Resources
{
    public interface IResourceLocator
    {
         /// <summary>
         /// Asynchronously provides access to a resource handle from a given descriptor.
         /// </summary>
         /// <param name="region"></param>
         /// <param name="descriptor">The resource descriptor.</param>
         /// <param name="stoppingToken">A token that propagates notifications that this operation should be cancelled.</param>
         /// <returns>An operation that eventually returns a resource handle.</returns>
        public Task<ResourceHandle> OpenHandleAsync(string region, ResourceDescriptor descriptor, CancellationToken stoppingToken);

        /// <summary>
        /// Asynchronously provides access to a resource handle from a given descriptor.
        /// </summary>
        /// <param name="endpoints">A collection of endpoints to query.</param>
        /// <param name="descriptor">The resource descriptor.</param>
        /// <param name="stoppingToken">A token that propagates notifications that this operation should be cancelled.</param>
        /// <returns>An operation that eventually returns a resource handle.</returns>
        public Task<ResourceHandle> OpenHandleAsync(IAsyncEnumerable<PatchEndpoint> endpoints, ResourceDescriptor descriptor, CancellationToken stoppingToken);

        public Task<ResourceHandle> OpenHandleAsync(ResourceDescriptor descriptor, CancellationToken stoppingToken);
    }

    public record struct PatchEndpoint(string Host, string DataStem, string ConfigurationStem);

}
