using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Shared.IO;

using Polly;

using System.Net;
using System.Net.Http.Headers;

namespace Blizztrack.Framework.Extensions.Services
{
    file class TransferContext
    {
        public required IEnumerator<PatchEndpoint> Endpoints { get; init; }
        public required HttpClient Client { get; init; }

        public required RangeHeaderValue? Range { get; init; }
    }

    public readonly record struct ContentQueryResult(HttpStatusCode StatusCode, Stream Body);

    /// <summary>
    /// Provides utility methods to access resources, optionally downloading them from Blizzard's CDNs.
    /// </summary>
    /// <param name="clientFactory"></param>
    /// <param name="localCache"></param>
    /// <param name="serviceProvider"></param>
    public abstract class AbstractResourceLocatorService(IHttpClientFactory clientFactory) : IResourceLocator
    {
        private readonly IHttpClientFactory _clientFactory = clientFactory;

        public abstract ContentKey ResolveContentKey(in TACT.Views.EncodingKey encodingKey);

        public abstract ResourceHandle OpenLocalHandle(ResourceDescriptor resourceDescriptor);

        public abstract ResourceHandle CreateLocalHandle(ResourceDescriptor resourceDescriptor, byte[] fileData);

        /// <summary>
        /// Opens a handle to a resource. Attempts to download the resource if it can't be found locally.
        /// </summary>
        /// <param name="resourceDescriptor">A descriptor for the resource.</param>
        /// <param name="stoppingToken">A cancellation token that can be signalled to cancel the request.</param>
        /// <returns>A resource handle.</returns>
        public async Task<ResourceHandle> OpenHandle(ResourceDescriptor resourceDescriptor, CancellationToken stoppingToken)
        {
            var localHandle = OpenLocalHandle(resourceDescriptor);
            if (localHandle.Exists)
                return localHandle;

            var endpoints = GetEndpoints(resourceDescriptor.Product);
            if (endpoints.Count == 0)
                return default;

            var backendQuery = await ExecuteQuery(endpoints, resourceDescriptor, stoppingToken);
            if (backendQuery.StatusCode != HttpStatusCode.OK && backendQuery.StatusCode != HttpStatusCode.PartialContent)
                return default;

            { // Create a stream from the local handle.
                using var fileStream = new FileStream(localHandle.Path, FileMode.Create, FileAccess.Write, FileShare.None, 0, true);
                await backendQuery.Body.CopyToAsync(fileStream, stoppingToken);
                fileStream.Dispose();
            }

            return OpenLocalHandle(resourceDescriptor);
        }

        public virtual Task<ResourceHandle> OpenCompressedHandle(string productCode, in TACT.Views.EncodingKey encodingKey, in TACT.Views.ContentKey contentKey, CancellationToken stoppingToken)
        {
            var compressedDescriptor = ResourceType.Data.ToDescriptor(productCode, encodingKey, contentKey);
            var decompressedDescriptor = ResourceType.Decompressed.ToDescriptor(productCode, encodingKey, contentKey);
            return OpenCompressedHandleImpl(compressedDescriptor, decompressedDescriptor, stoppingToken);
        }

        // VALIDATED API
        public Task<ResourceHandle> OpenCompressedHandle(string productCode, in TACT.Views.EncodingKey encodingKey, CancellationToken stoppingToken)
            => OpenCompressedHandle(ResourceType.Data.ToDescriptor(productCode, encodingKey), stoppingToken);

        public abstract Task<ResourceHandle> OpenCompressedHandle(ResourceDescriptor compressedDescriptor, CancellationToken stoppingToken);

        // VALIDATED IMPLEMENTATION DETAIL
        private async Task<ResourceHandle> OpenCompressedHandleImpl(ResourceDescriptor compressed, ResourceDescriptor decompressed, CancellationToken stoppingToken)
        {
            var decompressedHandle = OpenLocalHandle(decompressed);
            if (decompressedHandle != default)
                return decompressedHandle;

            // Create the decompressed resource now.
            var compressedHandle = await OpenHandle(compressed, stoppingToken);
            var decompressedData = BLTE.Parse(compressedHandle);

            decompressedHandle.Create(decompressedData);
            return decompressedHandle;
        }

        protected abstract ResiliencePipeline<ContentQueryResult> AcquisitionPipeline { get; }

        /// <summary>
        /// Gets all endpoints that match the product (and optionally the region) provided.
        /// </summary>
        /// <param name="productCode"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        protected abstract IList<PatchEndpoint> GetEndpoints(string productCode, string region = "xx");

        /// <summary>
        /// Queries the given descriptor from the first endpoint that responds successfully.
        /// </summary>
        /// <param name="hosts"></param>
        /// <param name="descriptor"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private async ValueTask<ContentQueryResult> ExecuteQuery(IEnumerable<PatchEndpoint> hosts, ResourceDescriptor descriptor, CancellationToken stoppingToken)
        {
            // Debug.Assert(descriptor.Type != ResourceType.Decompressed, "Decompressed descriptors can't be acquired from CDNs.");

            var transferContext = new TransferContext()
            {
                Client = _clientFactory.CreateClient(),
                Range = descriptor.Offset != 0
                    ? new RangeHeaderValue(descriptor.Offset, descriptor.Offset + descriptor.Length - 1)
                    : default,
                Endpoints = hosts.GetEnumerator(),
            };

            var resilienceContext = ResilienceContextPool.Shared.Get(stoppingToken);
            var result = await AcquisitionPipeline.ExecuteOutcomeAsync(async (context, state) =>
            {
                if (!state.Endpoints.MoveNext())
                    return Outcome.FromResult(new ContentQueryResult(HttpStatusCode.NotFound, Stream.Null));

                var server = state.Endpoints.Current;
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"http://{server.Host}/{server.DataStem}/{descriptor.RemotePath}")
                {
                    Headers = { Range = state.Range },
                };

                var response = await state.Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, stoppingToken);
                response.EnsureSuccessStatusCode();

                var dataStream = await response.Content.ReadAsStreamAsync();
                var wrappedStream = new DelegateStream(dataStream, response.Content.Headers.ContentLength);
                var transferInformation = new ContentQueryResult(response.StatusCode, wrappedStream);

                return Outcome.FromResult(transferInformation);
            }, resilienceContext, transferContext);

            return result.Result;
        }

        public async Task<Stream> OpenStream(ResourceDescriptor descriptor, CancellationToken stoppingToken = default)
        {
            var localHandle = OpenLocalHandle(descriptor);
            if (localHandle.Exists && localHandle.Exists)
                return localHandle.ToStream();

            var endpoints = GetEndpoints(descriptor.Product);
            var backendQuery = await ExecuteQuery(endpoints, descriptor, stoppingToken);
            if (backendQuery.StatusCode != HttpStatusCode.OK && backendQuery.StatusCode != HttpStatusCode.PartialContent)
                return Stream.Null;

            return backendQuery.Body;
        }
    }
}
