using Asp.Versioning;

using Blizztrack.API.Bindings;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using NSwag.Annotations;

using System.ComponentModel;


namespace Blizztrack.API
{
    [ApiVersion(1.0)]
    [Tags("File system endpoints"), OpenApiTag("File system endpoints", Description = "Endpoints in this category are able to extract files from TACT file systems.")]
    [ApiController, Route("api/v{version:apiVersion}/fs")]
    public class FileSystemController(DatabaseContext databaseContext, FileSystemSupplier fileSystems, IResourceLocator resourceLocator) : ControllerBase
    {
        [OpenApiOperation("Extracts a file from a file system configuration.")]
        [HttpGet(FileSystemBinder.CONFIGURATION_NAME + "/get/fdid/{fileDataID}")]
        [HttpGet(FileSystemBinder.FILE_SYSTEM_EXPLICIT + "/get/fdid/{fileDataID}")]
        public async Task<IResult> OpenFileDataID(
            FileSystemResolver fileSystemResolver,
            [Description("An unique identifier for the file.")]
            uint fileDataID,
            CancellationToken stoppingToken)
        {
            var fileSystem = await fileSystemResolver.Resolve(databaseContext, resourceLocator, fileSystems, stoppingToken);
            var descriptors = fileSystem.OpenFDID(fileDataID);
            return TypedResults.Ok(descriptors);
        }

        [OpenApiOperation("Extracts a file from a file system configuration.")]
        [HttpGet(FileSystemBinder.CONFIGURATION_NAME + "/get/ek/{encodingKey}")]
        [HttpGet(FileSystemBinder.FILE_SYSTEM_EXPLICIT + "/get/ek/{encodingKey}")]
        public async Task<IResult> OpenFileDataID(
            FileSystemResolver fileSystemResolver,
            [Description("The encoding key associated with a file.")]
            EncodingKey encodingKey,
            CancellationToken stoppingToken)
        {
            var fileSystem = await fileSystemResolver.Resolve(databaseContext, resourceLocator, fileSystems, stoppingToken);
            var descriptor = fileSystem.OpenEncodingKey(encodingKey);
            return TypedResults.Ok(descriptor);
        }

        [OpenApiOperation("Extracts a file from a file system configuration.")]
        [HttpGet(FileSystemBinder.CONFIGURATION_NAME + "/get/ck/{contentKey}")]
        [HttpGet(FileSystemBinder.FILE_SYSTEM_EXPLICIT + "/get/ck/{contentKey}")]
        public async Task<IResult> OpenFileDataID(
            FileSystemResolver fileSystemResolver,
            [Description("The encoding key associated with a file.")]
            ContentKey encodingKey,
            CancellationToken stoppingToken)
        {
            var fileSystem = await fileSystemResolver.Resolve(databaseContext, resourceLocator, fileSystems, stoppingToken);
            var descriptor = fileSystem.OpenContentKey(encodingKey);
            return TypedResults.Ok(descriptor);
        }
    }
}
