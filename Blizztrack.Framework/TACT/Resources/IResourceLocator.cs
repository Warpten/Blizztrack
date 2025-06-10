using Blizztrack.Framework.TACT.Implementation;

namespace Blizztrack.Framework.TACT.Resources
{
    public interface IResourceLocator
    {
        /// <summary>
        /// Opens a handle to a resource.
        /// </summary>
        /// <param name="resourceDescriptor"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public Task<ResourceHandle> OpenHandle(ResourceDescriptor resourceDescriptor, CancellationToken stoppingToken);

        /// <summary>
        /// Opens a compressed resource.
        /// </summary>
        /// <typeparam name="E">A type that implements <see cref="IEncodingKey{T}"/> and <see cref="IKey{T}"/>.</typeparam>
        /// <typeparam name="T">The type of object to return, which must implement <see cref="IResourceParser{T}"/>.</typeparam>
        /// <param name="productCode">A string identifying the product that caused this call.</param>
        /// <param name="encodingKey">The encoding key of the file.</param>
        /// <param name="stoppingToken">A cancellation token that will be signaled of the operation needs to be canceled.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the operation represented by this method needs to be cancelled.</exception>
        public Task<T> OpenCompressed<T, E>(string productCode, E encodingKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey, allows ref struct
            where T : class, IResourceParser<T>;

        /// <summary>
        /// Opens a compressed resource.
        /// </summary>
        /// <typeparam name="E">A type that implements <see cref="IEncodingKey{T}"/> and <see cref="IKey{T}"/>.</typeparam>
        /// <param name="productCode">A string identifying the product that caused this call.</param>
        /// <param name="encodingKey">The encoding key of the file.</param>
        /// <param name="stoppingToken">A cancellation token that will be signaled of the operation needs to be canceled.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the operation represented by this method needs to be cancelled.</exception>
        public Task<ResourceHandle> OpenCompressedHandle<E>(string productCode, E encodingKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey, allows ref struct;

        /// <summary>
        /// Opens a compressed resource.
        /// </summary>
        /// <typeparam name="E">A type that implements <see cref="IEncodingKey{T}"/> and <see cref="IKey{T}"/>.</typeparam>
        /// <typeparam name="C">A type that implements <see cref="IContentKey{T}"/> and <see cref="IKey{T}"/>.</typeparam>
        /// <typeparam name="T">The type of object to return, which must implement <see cref="IResourceParser{T}"/>.</typeparam>
        /// <param name="productCode">A string identifying the product that caused this call.</param>
        /// <param name="encodingKey">The encoding key of the compressed file.</param>
        /// <param name="contentKey">The content key of the decompressed file.</param>
        /// <param name="stoppingToken">A cancellation token that will be signaled of the operation needs to be canceled.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the operation represented by this method needs to be cancelled.</exception>
        public Task<T> OpenCompressed<E, C, T>(string productCode, E encodingKey, C contentKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey, allows ref struct
            where C : IContentKey<C>, IKey, allows ref struct
            where T : class, IResourceParser<T>;

        /// <summary>
        /// Opens a compressed resource.
        /// </summary>
        /// <typeparam name="E">A type that implements <see cref="IEncodingKey{T}"/> and <see cref="IKey{T}"/>.</typeparam>
        /// <typeparam name="C">A type that implements <see cref="IContentKey{T}"/> and <see cref="IKey{T}"/>.</typeparam>
        /// <param name="productCode">A string identifying the product that caused this call.</param>
        /// <param name="encodingKey">The encoding key of the compressed file.</param>
        /// <param name="contentKey">The content key of the decompressed file.</param>
        /// <param name="stoppingToken">A cancellation token that will be signaled of the operation needs to be canceled.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the operation represented by this method needs to be cancelled.</exception>
        public Task<ResourceHandle> OpenCompressedHandle<E, C>(string productCode, E encodingKey, C contentKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey, allows ref struct
            where C : IContentKey<C>, IKey, allows ref struct;
    }

    public record struct PatchEndpoint(string Host, string DataStem, string ConfigurationStem);
}
