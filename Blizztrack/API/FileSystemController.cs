using Asp.Versioning;

using Blizztrack.API.Bindings;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        [HttpGet("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/fdid/{fileDataID:int}/get")]
        [HttpHead("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/fdid/{fileDataID:int}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", """
            Streams back the decompressed file to the REST client.

            If a HEAD request is issued, this endpoint instead returns a list of metadata for the resource
            as part of the response headers.
            """)]
        public async Task<IResult> OpenFileDataID(
            [Description("The product code.")] string productCode,
            [Description("The build configuration hash.")] BoundEncodingKey buildConfiguration,
            [Description("The CDN configuration hash.")] BoundEncodingKey serverConfiguration,
            [Description("An unique identifier for the file to obtain.")] uint fileDataID,
            CancellationToken stoppingToken)
        {
            var fs = await ResolveFileSystem(productCode, buildConfiguration, serverConfiguration, stoppingToken);
            if (fs is null)
                return TypedResults.NotFound();

            return await OpenFileDataID(fs, fileDataID, stoppingToken);
        }

        [HttpGet("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/ek/{encodingKey}/get")]
        [HttpHead("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/ek/{encodingKey}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", """
            Streams back the decompressed file to the REST client.

            If a HEAD request is issued, this endpoint instead returns a list of metadata for the resource
            as part of the response headers.
            """)]
            
        public async Task<IResult> OpenFileDataID(
            [Description("The product code.")] string productCode,
            [Description("The build configuration hash.")] BoundEncodingKey buildConfiguration,
            [Description("The CDN configuration hash.")] BoundEncodingKey serverConfiguration,
            [Description("An encoding key for the file.")] BoundEncodingKey encodingKey,
            CancellationToken stoppingToken)
        {
            var fs = await ResolveFileSystem(productCode, buildConfiguration, serverConfiguration, stoppingToken);
            if (fs is null)
                return TypedResults.NotFound();

            return await OpenEncodingKey(fs, encodingKey, stoppingToken);
        }

        [HttpGet("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/ck/{contentKey}/get")]
        [HttpHead("p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}/ck/{contentKey}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", """
            Streams back the decompressed file to the REST client.

            If a HEAD request is issued, this endpoint instead returns a list of metadata for the resource
            as part of the response headers.
            """)]
        public async Task<IResult> OpenContentKey(
            [Description("The product code.")] string productCode,
            [Description("The build configuration hash.")] BoundEncodingKey buildConfiguration,
            [Description("The CDN configuration hash.")] BoundEncodingKey serverConfiguration,
            [Description("A content key for the file.")] BoundContentKey contentKey,
            CancellationToken stoppingToken)
        {
            var fs = await ResolveFileSystem(productCode, buildConfiguration, serverConfiguration, stoppingToken);
            if (fs is null)
                return TypedResults.NotFound();

            return await OpenContentKey(fs, contentKey, stoppingToken);
        }

        #endregion

        #region c/{configurationName}/...
        [HttpGet("c/{configurationName}/fdid/{fileDataID:int}/get")]
        [HttpHead("c/{configurationName}/fdid/{fileDataID:int}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", """
            Streams back the decompressed file to the REST client.

            If a HEAD request is issued, this endpoint instead returns a list of metadata for the resource
            as part of the response headers.
            """)]
        public async Task<IResult> OpenFileDataID(
            [Description("The name of the build configuration.")] string configurationName,
            [Description("An unique identifier for the file to obtain.")] uint fileDataID,
            CancellationToken stoppingToken)
        {
            var fs = await ResolveFileSystem(configurationName, stoppingToken);
            if (fs is null)
                return TypedResults.NotFound();

            return await OpenFileDataID(fs, fileDataID, stoppingToken);
        }

        [HttpGet("c/{configurationName}/ek/{encodingKey}/get")]
        [HttpHead("c/{configurationName}/ek/{encodingKey}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", """
            Streams back the decompressed file to the REST client.

            If a HEAD request is issued, this endpoint instead returns a list of metadata for the resource
            as part of the response headers.
            """)]
        public async Task<IResult> OpenEncodingKey(
            [Description("The name of the build configuration.")] string configurationName,
            [Description("An encoding key for the file.")] BoundEncodingKey encodingKey,
            CancellationToken stoppingToken)
        {
            var fs = await ResolveFileSystem(configurationName, stoppingToken);
            if (fs is null)
                return TypedResults.NotFound();

            return await OpenEncodingKey(fs, encodingKey, stoppingToken);
        }

        [HttpGet("c/{configurationName}/ck/{contentKey}/get")]
        [HttpHead("c/{configurationName}/ck/{contentKey}/get")]
        [OpenApiOperation("Extracts a file from a file system configuration.", """
            Streams back the decompressed file to the REST client.

            If a HEAD request is issued, this endpoint instead returns a list of metadata for the resource
            as part of the response headers.
            """)]
        public async Task<IResult> OpenContentKey(
            [Description("The name of the build configuration.")] string configurationName,
            [Description("A content key for the file.")] BoundContentKey contentKey,
            CancellationToken stoppingToken)
        {
            var fs = await ResolveFileSystem(configurationName, stoppingToken);
            if (fs is null)
                return TypedResults.NotFound();

            return await OpenContentKey(fs, contentKey, stoppingToken);
        }
        #endregion

        private async Task<IResult> OpenFileDataID(IFileSystem fileSystem, uint fileDataID, CancellationToken stoppingToken)
        {
            var descriptors = fileSystem.OpenFDID(fileDataID);
            if (descriptors.Length == 0)
                return TypedResults.NotFound();

            if (Request.Method[0] == 'h') // Sue me
            {
                Response.Headers["X-Blizztrack-FileDataID"] = fileDataID.ToString();
                Response.Headers["X-Blizztrack-Archives"] = descriptors.Select(d => d.Archive.AsHexString()).ToArray();
                Response.Headers["X-Blizztrack-Offsets"] = descriptors.Select(d => d.Offset.ToString()).ToArray();
                Response.Headers["X-Blizztrack-Length"] = descriptors.Select(d => d.Length.ToString()).ToArray();

                return TypedResults.Ok();
            }

            foreach (var descriptor in descriptors)
            {
                var dataStream = await resourceLocator.OpenStream(descriptor, stoppingToken);
                if (dataStream != Stream.Null)
                    return TypedResults.Ok(dataStream);
            }

            return TypedResults.NotFound();
        }

        private async Task<IResult> OpenEncodingKey(IFileSystem fileSystem, EncodingKey encodingKey, CancellationToken stoppingToken)
        {
            var descriptor = fileSystem.OpenEncodingKey(encodingKey);

            if (Request.Method[0] == 'h') // Sue me
            {
                Response.Headers["X-Blizztrack-EncodingKey"] = encodingKey.ToString();
                Response.Headers["X-Blizztrack-Archive"] = descriptor.Archive.AsHexString();
                Response.Headers["X-Blizztrack-Offset"] = descriptor.Offset.ToString();
                Response.Headers["X-Blizztrack-Length"] = descriptor.Length.ToString();

                return TypedResults.Ok();
            }

            var dataStream = await resourceLocator.OpenStream(descriptor, stoppingToken);
            if (dataStream != Stream.Null)
                return TypedResults.Ok(dataStream);

            return TypedResults.NotFound();
        }

        private async Task<IResult> OpenContentKey(IFileSystem fileSystem, ContentKey contentKey, CancellationToken stoppingToken)
        {
            var descriptors = fileSystem.OpenContentKey(contentKey);
            if (descriptors.Length == 0)
                return TypedResults.NotFound();

            if (Request.Method[0] == 'h') // Sue me
            {
                Response.Headers["X-Blizztrack-ContentKey"] = contentKey.AsHexString();
                Response.Headers["X-Blizztrack-Archives"] = descriptors.Select(d => d.Archive.AsHexString()).ToArray();
                Response.Headers["X-Blizztrack-Offsets"] = descriptors.Select(d => d.Offset.ToString()).ToArray();
                Response.Headers["X-Blizztrack-Length"] = descriptors.Select(d => d.Length.ToString()).ToArray();

                return TypedResults.Ok();
            }

            foreach (var descriptor in descriptors)
            {
                var dataStream = await resourceLocator.OpenStream(descriptor, stoppingToken);
                if (dataStream != Stream.Null)
                    return TypedResults.Ok(dataStream);
            }

            return TypedResults.NotFound();
        }

        private async Task<IFileSystem> ResolveFileSystem(string configurationName, CancellationToken stoppingToken)
        {
            var configuration = databaseContext.Configs
                .Include(e => e.Product)
                .SingleOrDefault(c => c.Name == configurationName);
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
            var descriptor = ResourceType.Config.ToDescriptor(productCode, encodingKey);
            var resourceHandle = await resourceLocator.OpenHandle(descriptor, stoppingToken);

            return T.OpenResource(resourceHandle);
        }
    }
}
