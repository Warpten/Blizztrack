using Blizztrack.Framework.TACT.Implementation;

using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.TACT.Resources
{
    public interface IResourceLocator
    {
        /// <summary>
        /// Opens a stream to a resource.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="stoppingToken"></param>
        /// <returns>A stream over the resource.</returns>
        public Task<Stream> OpenStream(ResourceDescriptor descriptor, CancellationToken stoppingToken = default);

        /// <summary>
        /// Opens a handle to a resource on disk. If the resource is not found, opens an invalid handle.
        /// </summary>
        /// <param name="resourceDescriptor">A resource descriptor.</param>
        /// <returns>A handle to the resource on disk.</returns>
        public ResourceHandle OpenLocalHandle(ResourceDescriptor resourceDescriptor);
        
        /// <summary>
        /// Creates a local handle on disk.
        /// </summary>
        /// <param name="resourceDescriptor">A resource descriptor.</param>
        /// <param name="fileData">The file's (optionally compressed) data.</param>
        public ResourceHandle CreateLocalHandle(ResourceDescriptor resourceDescriptor, byte[] fileData);

        /// <summary>
        /// Opens a handle to a resource.
        /// </summary>
        /// <param name="resourceDescriptor"></param>
        /// <param name="stoppingToken"></param>
        /// <returns>A resource handle.</returns>
        public Task<ResourceHandle> OpenHandle(ResourceDescriptor resourceDescriptor, CancellationToken stoppingToken = default);

        /// <summary>
        /// Opens a compressed resource.
        /// </summary>
        /// <param name="productCode">A string identifying the product that caused this call.</param>
        /// <param name="encodingKey">The encoding key of the file.</param>
        /// <param name="stoppingToken">A cancellation token that will be signaled of the operation needs to be canceled.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the operation represented by this method needs to be cancelled.</exception>
        public Task<ResourceHandle> OpenCompressedHandle(string productCode, in Views.EncodingKey encodingKey, CancellationToken stoppingToken = default);

        /// <summary>
        /// Opens a compressed resource.
        /// </summary>
        /// <param name="productCode">A string identifying the product that caused this call.</param>
        /// <param name="encodingKey">The encoding key of the compressed file.</param>
        /// <param name="contentKey">The content key of the decompressed file.</param>
        /// <param name="stoppingToken">A cancellation token that will be signaled of the operation needs to be canceled.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the operation represented by this method needs to be cancelled.</exception>
        public Task<ResourceHandle> OpenCompressedHandle(string productCode, in Views.EncodingKey encodingKey, in Views.ContentKey contentKey, CancellationToken stoppingToken = default);

        /// <summary>
        /// Tries to resolve a content key for the given encoding key.
        /// 
        /// If this function returns a valid content key, it will be used to obtain a handle to a <see cref="ResourceType.Decompressed"/> resource.
        /// </summary>
        /// <param name="encodingKey"></param>
        /// <returns></returns>
        public ContentKey ResolveContentKey(in Views.EncodingKey encodingKey);
    }

    public static class ResourceLocatorExtensions
    {
        /// <summary>
        /// Opens a compressed resource.
        /// </summary>
        /// <typeparam name="T">The type of object to return, which must implement <see cref="IResourceParser{T}"/>.</typeparam>
        /// <param name="productCode">A string identifying the product that caused this call.</param>
        /// <param name="encodingKey">The encoding key of the file.</param>
        /// <param name="stoppingToken">A cancellation token that will be signaled of the operation needs to be canceled.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the operation represented by this method needs to be cancelled.</exception>
        public static Task<T> OpenCompressed<T>(this IResourceLocator locator,
            string productCode, in Views.EncodingKey encodingKey, CancellationToken stoppingToken = default)
            where T : class, IResourceParser<T>
        {
            // Look for this resource in the well known table.
            // If it's well known, create a file on disk if it doesn't exist, decompressed the resource
            // in it, and call the decompressed loader. Otherwise, call the compressed loader.
            var contentKey = locator.ResolveContentKey(encodingKey);
            if (contentKey)
                return OpenCompressed<T>(locator, productCode, encodingKey, contentKey, stoppingToken);

            // Not a known resource... Just use the compressed handler.
            var compressedHandle = ResourceType.Data.ToDescriptor(productCode, encodingKey);
            return locator.OpenHandle(compressedHandle, stoppingToken)
                .ContinueWith(result => T.OpenCompressedResource(result.Result));
        }

        /// <summary>
        /// Opens a compressed resource.
        /// </summary>
        /// <typeparam name="T">The type of object to return, which must implement <see cref="IResourceParser{T}"/>.</typeparam>
        /// <param name="productCode">A string identifying the product that caused this call.</param>
        /// <param name="encodingKey">The encoding key of the compressed file.</param>
        /// <param name="contentKey">The content key of the decompressed file.</param>
        /// <param name="stoppingToken">A cancellation token that will be signaled of the operation needs to be canceled.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the operation represented by this method needs to be cancelled.</exception>
        public static Task<T> OpenCompressed<T>(this IResourceLocator locator, string productCode, in Views.EncodingKey encodingKey, in Views.ContentKey contentKey, CancellationToken stoppingToken = default)
            where T : class, IResourceParser<T>
        {
            var compressedDescriptor = ResourceType.Data.ToDescriptor(productCode, encodingKey, contentKey);
            var decompressedDescriptor = ResourceType.Decompressed.ToDescriptor(productCode, encodingKey, contentKey);
            return OpenCompressedImpl<T>(locator, compressedDescriptor, decompressedDescriptor, stoppingToken);
        }

        private static async Task<T> OpenCompressedImpl<T>(IResourceLocator locator, ResourceDescriptor compressed, ResourceDescriptor decompressed, CancellationToken stoppingToken)
            where T : class, IResourceParser<T>
        {
            var decompressedHandle = locator.OpenLocalHandle(decompressed);
            if (decompressedHandle != default && decompressedHandle.Exists)
                return T.OpenResource(decompressedHandle);

            // Create the decompressed resource now.
            var compressedHandle = await locator.OpenHandle(compressed, stoppingToken);
            var decompressedData = BLTE.Parse(compressedHandle);

            var localHandle = locator.CreateLocalHandle(decompressed, decompressedData);
            return T.OpenResource(localHandle);
        }
    }

    public record struct PatchEndpoint(string Host, string DataStem, string ConfigurationStem);
}
