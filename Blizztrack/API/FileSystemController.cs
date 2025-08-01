using Asp.Versioning;

using Blizztrack.API.Bindings;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Persistence.Translators;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;

using NSwag.Annotations;

using System.ComponentModel;


namespace Blizztrack.API
{
    [ApiVersion(1.0)]
    [Tags("File system endpoints"), OpenApiTag("File system endpoints", Description = "Endpoints in this category are able to extract files from TACT file systems.")]
    [ApiController, Route("api/v{version:apiVersion}/fs")]
    public class FileSystemController(DatabaseContext databaseContext, FileSystemSupplier fileSystems, IResourceLocator resourceLocator) : ControllerBase
    {
        #region p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/...
        [HttpGet("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/fdid/{fileDataID:uint}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", "Streams back the decompressed file to the REST client.")]
        public Task<IResult> OpenFileDataID(
            [Description("The product code.")] string productCode,
            [Description("The build configuration hash.")] BoundEncodingKey buildConfiguration,
            [Description("The CDN configuration hash.")] BoundEncodingKey serverConfiguration,
            [Description("An unique identifier for the file to obtain.")] uint fileDataID,
            CancellationToken stoppingToken)
            => WithFileSystem(ResolveFileSystem(productCode, buildConfiguration, serverConfiguration, stoppingToken),
                OpenFileDataID, fileDataID);

        [HttpGet("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/ek/{encodingKey}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", "Streams back the decompressed file to the REST client.")]
        public Task<IResult> OpenFileDataID(
            [Description("The product code.")] string productCode,
            [Description("The build configuration hash.")] BoundEncodingKey buildConfiguration,
            [Description("The CDN configuration hash.")] BoundEncodingKey serverConfiguration,
            [Description("An encoding key for the file.")] BoundEncodingKey encodingKey,
            CancellationToken stoppingToken)
            => WithFileSystem<EncodingKey, IResult>(ResolveFileSystem(productCode, buildConfiguration, serverConfiguration, stoppingToken),
                OpenEncodingKey, encodingKey);

        [HttpGet("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/ck/{contentKey}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", "Streams back the decompressed file to the REST client.")]
        public Task<IResult> OpenContentKey(
            [Description("The product code.")] string productCode,
            [Description("The build configuration hash.")] BoundEncodingKey buildConfiguration,
            [Description("The CDN configuration hash.")] BoundEncodingKey serverConfiguration,
            [Description("A content key for the file.")] BoundContentKey contentKey,
            CancellationToken stoppingToken)
            => WithFileSystem<ContentKey, IResult>(ResolveFileSystem(productCode, buildConfiguration, serverConfiguration, stoppingToken),
                OpenContentKey, contentKey);

        #endregion

        #region c/{configurationName}/...
        [HttpGet("c/{configurationName}/fdid/{fileDataID:uint}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", "Streams back the decompressed file to the REST client.")]
        public Task<IResult> OpenFileDataID(
            [Description("The name of the build configuration.")] string configurationName,
            [Description("An unique identifier for the file to obtain.")] uint fileDataID,
            CancellationToken stoppingToken)
            => WithFileSystem(ResolveFileSystem(configurationName, stoppingToken),
                OpenFileDataID, fileDataID);

        [HttpGet("c/{configurationName}/ek/{encodingKey}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", "Streams back the decompressed file to the REST client.")]
        public Task<IResult> OpenEncodingKey(
            [Description("The name of the build configuration.")] string configurationName,
            [Description("An encoding key for the file.")] BoundEncodingKey encodingKey,
            CancellationToken stoppingToken)
            => WithFileSystem<EncodingKey, IResult>(ResolveFileSystem(configurationName, stoppingToken),
                OpenEncodingKey, encodingKey);

        [HttpGet("c/{configurationName}/ck/{contentKey}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", "Streams back the decompressed file to the REST client.")]
        public Task<IResult> OpenContentKey(
            [Description("The name of the build configuration.")] string configurationName,
            [Description("A content key for the file.")] BoundContentKey contentKey,
            CancellationToken stoppingToken)
            => WithFileSystem<ContentKey, IResult>(ResolveFileSystem(configurationName, stoppingToken),
                OpenContentKey, contentKey);
        #endregion

        private IResult OpenFileDataID(IFileSystem fileSystem, uint fileDataID)
        {
            var descriptors = fileSystem.OpenFDID(fileDataID);
            return TypedResults.Ok(descriptors);
        }

        private IResult OpenEncodingKey(IFileSystem fileSystem, EncodingKey encodingKey)
        {
            var descriptor = fileSystem.OpenEncodingKey(encodingKey);
            return TypedResults.Ok(descriptor);
        }

        private IResult OpenContentKey(IFileSystem fileSystem, ContentKey contentKey)
        {
            var descriptors = fileSystem.OpenContentKey(contentKey);
            return TypedResults.Ok(descriptors);
        }

        private static Task<T> WithFileSystem<U, T>(Task<IFileSystem> fileSystem, Func<IFileSystem, U, T> handler, U queryParameter)
            => fileSystem.ContinueWith(task => handler(task.Result, queryParameter));

        private async Task<IFileSystem> ResolveFileSystem(string configurationName, CancellationToken stoppingToken)
        {
            var configuration = databaseContext.Configs.SingleOrDefault(c => c.Name == configurationName);
            if (configuration is null)
                throw new InvalidOperationException();

            return await ResolveFileSystem(configuration.Product.Code, configuration.BuildConfig, configuration.CDNConfig, stoppingToken);
        }

        private async Task<IFileSystem> ResolveFileSystem(string productCode, EncodingKey buildConfiguration, EncodingKey serverConfiguration, CancellationToken stoppingToken)
        {
            var build = await OpenConfig<BuildConfiguration>(productCode, buildConfiguration, stoppingToken);
            var server = await OpenConfig<ServerConfiguration>(productCode, serverConfiguration, stoppingToken);

            return await fileSystems.OpenFileSystem(productCode, build, server, resourceLocator, stoppingToken);
        }

        private async Task<T> OpenConfig<T>(string productCode, EncodingKey encodingKey, CancellationToken stoppingToken)
            where T : class, IResourceParser<T>
        {
            var descriptor = new ResourceDescriptor(ResourceType.Config, productCode, encodingKey.AsHexString());
            var resourceHandle = await resourceLocator.OpenHandle(descriptor, stoppingToken);

            return T.OpenResource(resourceHandle);
        }
    }
}
