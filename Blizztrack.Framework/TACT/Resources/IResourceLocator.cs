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
        /// <returns></returns>
        public Task<Stream> OpenStream(ResourceDescriptor descriptor, CancellationToken stoppingToken = default);

        /// <summary>
        /// Opens a handle to a resource.
        /// </summary>
        /// <param name="resourceDescriptor"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public Task<ResourceHandle> OpenHandle(ResourceDescriptor resourceDescriptor, CancellationToken stoppingToken = default);

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
        public Task<T> OpenCompressed<E, T>(string productCode, E encodingKey, CancellationToken stoppingToken = default)
            where E : IEncodingKey<E>, allows ref struct
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
        public Task<ResourceHandle> OpenCompressedHandle<E>(string productCode, E encodingKey, CancellationToken stoppingToken = default)
            where E : IEncodingKey<E>, allows ref struct;

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
        public Task<T> OpenCompressed<E, C, T>(string productCode, E encodingKey, C contentKey, CancellationToken stoppingToken = default)
            where E : IEncodingKey<E>, allows ref struct
            where C : IContentKey<C>, allows ref struct
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
        public Task<ResourceHandle> OpenCompressedHandle<E, C>(string productCode, E encodingKey, C contentKey, CancellationToken stoppingToken = default)
            where E : IEncodingKey<E>, allows ref struct
            where C : IContentKey<C>, allows ref struct;
    }

    //< Utility wrappers that should not be overridable.
    public static class ResourceLocatorExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> OpenCompressed<TLocator, E, T>(this TLocator self, string productCode, SizeAware<E> encodingKey, CancellationToken stoppingToken = default)
            where TLocator : IResourceLocator
            where E : IEncodingKey<E>
            where T : class, IResourceParser<T>
            => self.OpenCompressed<E, T>(productCode, encodingKey.Key, stoppingToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<ResourceHandle> OpenCompressedHandle<TLocator, E, T>(this TLocator self, string productCode, SizeAware<E> encodingKey, CancellationToken stoppingToken = default)
            where TLocator : IResourceLocator
            where E : IEncodingKey<E>
            => self.OpenCompressedHandle(productCode, encodingKey.Key, stoppingToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> OpenCompressed<TLocator, E, C, T>(this TLocator self, string productCode, SizeAware<E> encodingKey, SizeAware<C> contentKey, CancellationToken stoppingToken = default)
            where TLocator : IResourceLocator
            where E : IEncodingKey<E>
            where C : IContentKey<C>
            where T : class, IResourceParser<T>
            => self.OpenCompressed<E, C, T>(productCode, encodingKey.Key, contentKey.Key, stoppingToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> OpenCompressed<TLocator, E, C, T>(this TLocator self, string productCode, KeyPair<C, E> keys, CancellationToken stoppingToken = default)
            where TLocator : IResourceLocator
            where E : IEncodingKey<E>
            where C : IContentKey<C>
            where T : class, IResourceParser<T>
            => self.OpenCompressed<E, C, T>(productCode, keys.Encoding, keys.Content, stoppingToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> OpenCompressed<TLocator, E, C, T>(this TLocator self, string productCode, SizedKeyPair<C, E> keys, CancellationToken stoppingToken = default)
            where TLocator : IResourceLocator
            where E : IEncodingKey<E>
            where C : IContentKey<C>
            where T : class, IResourceParser<T>
            => self.OpenCompressed<E, C, T>(productCode, keys.Encoding.Key, keys.Content.Key, stoppingToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<ResourceHandle> OpenCompressedHandle<U, E, C>(this U self, string productCode, SizeAware<E> encodingKey, SizeAware<C> contentKey, CancellationToken stoppingToken = default)
            where U : IResourceLocator
            where E : IEncodingKey<E>
            where C : IContentKey<C>
            => self.OpenCompressedHandle(productCode, encodingKey.Key, contentKey.Key, stoppingToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<ResourceHandle> OpenCompressedHandle<U, E, C>(this U self, string productCode, KeyPair<C, E> keys, CancellationToken stoppingToken = default)
            where U : IResourceLocator
            where E : IEncodingKey<E>
            where C : IContentKey<C>
            => self.OpenCompressedHandle(productCode, keys.Encoding, keys.Content, stoppingToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<ResourceHandle> OpenCompressedHandle<U, E, C>(this U self, string productCode, SizedKeyPair<C, E> keys, CancellationToken stoppingToken = default)
            where U : IResourceLocator
            where E : IEncodingKey<E>
            where C : IContentKey<C>
            => self.OpenCompressedHandle(productCode, keys.Encoding.Key, keys.Content.Key, stoppingToken);
    }

    public record struct PatchEndpoint(string Host, string DataStem, string ConfigurationStem);
}
