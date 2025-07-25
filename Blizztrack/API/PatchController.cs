using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;

namespace Blizztrack.API
{
    [ApiController, Route("api/v{version:apiVersion}/fs")]
    public class FileSystemController(DatabaseContext databaseContext, FileSystemSupplier fileSystems, IResourceLocator resourceLocator) : ControllerBase
    {
        [HttpGet("{configurationName}/fdid/{fileDataID}")]
        public async Task<IResult> OpenFile(string configurationName, uint fileDataID, CancellationToken stoppingToken)
        {
            var configuration = databaseContext.Configs.SingleOrDefault(c => c.Name == configurationName);
            if (configuration is null)
                return TypedResults.NotFound();

            var productCode = configuration.Product.Code;

            var buildConfiguration = await OpenBuildConfiguration(productCode, configuration.BuildConfig, stoppingToken);
            var cdnConfiguration = await OpenServerConfiguration(productCode, configuration.CDNConfig, stoppingToken);

            var fileSystem = await fileSystems.OpenFileSystem(configuration.Product.Code, buildConfiguration, cdnConfiguration, resourceLocator, stoppingToken);
            var descriptors = fileSystem.OpenFDID(fileDataID);

            if (descriptors.Length == 0)
                return TypedResults.NotFound();

            return TypedResults.Ok(descriptors);
        }

        private async Task<BuildConfiguration> OpenBuildConfiguration(string productCode, EncodingKey encodingKey, CancellationToken stoppingToken)
        {
            var descriptor = new ResourceDescriptor(ResourceType.Config, productCode, encodingKey.AsHexString());
            var resourceHandle = await resourceLocator.OpenHandle(descriptor, stoppingToken);

            return BuildConfiguration.Parse(resourceHandle.ToMappedDataSource());
        }

        private async Task<ServerConfiguration> OpenServerConfiguration(string productCode, EncodingKey encodingKey, CancellationToken stoppingToken)
        {
            var descriptor = new ResourceDescriptor(ResourceType.Config, productCode, encodingKey.AsHexString());
            var resourceHandle = await resourceLocator.OpenHandle(descriptor, stoppingToken);

            return ServerConfiguration.Parse(resourceHandle.ToMappedDataSource());
        }
    }
}
