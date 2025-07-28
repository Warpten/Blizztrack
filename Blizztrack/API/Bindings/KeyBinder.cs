using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using System.Reflection;

namespace Blizztrack.API.Bindings
{
    [ModelBinder<FileSystemBinder>]
    public class FileSystemResolver
    {
        public string? ConfigurationName { get; init; }

        public string? ProductCode { get; init; } = null;
        public EncodingKey BuildConfig { get; init; } = default;
        public EncodingKey ServerConfig { get; init; } = default;

        public async Task<IFileSystem> Resolve(DatabaseContext databaseContext, IResourceLocator resourceLocator, FileSystemSupplier fileSystems, CancellationToken stoppingToken)
        {
            if (ConfigurationName != null)
            {
                var configuration = databaseContext.Configs.SingleOrDefault(c => c.Name == ConfigurationName);
                if (configuration is null)
                    throw new InvalidOperationException();

                return await Open(resourceLocator, fileSystems, configuration.Product.Code, configuration.BuildConfig, configuration.CDNConfig, stoppingToken);
            }

            if (BuildConfig == EncodingKey.Zero || ServerConfig == EncodingKey.Zero || ProductCode == null)
                throw new InvalidOperationException();

            return await Open(resourceLocator, fileSystems, ProductCode, BuildConfig, ServerConfig, stoppingToken);
        }

        private static async Task<IFileSystem> Open(IResourceLocator resourceLocator, FileSystemSupplier fileSystems,
            string productCode,
            EncodingKey buildConfig, EncodingKey cdnConfig, CancellationToken stoppingToken)
        {
            var buildConfiguration = await OpenConfig<BuildConfiguration>(resourceLocator, productCode, buildConfig, stoppingToken);
            var serverConfiguration = await OpenConfig<ServerConfiguration>(resourceLocator, productCode, cdnConfig, stoppingToken);

            return await fileSystems.OpenFileSystem(productCode, buildConfiguration, serverConfiguration, resourceLocator, stoppingToken);
        }

        private static async Task<T> OpenConfig<T>(IResourceLocator resourceLocator, string productCode, EncodingKey encodingKey, CancellationToken stoppingToken)
            where T : class, IResourceParser<T>
        {
            var descriptor = new ResourceDescriptor(ResourceType.Config, productCode, encodingKey.AsHexString());
            var resourceHandle = await resourceLocator.OpenHandle(descriptor, stoppingToken);

            return T.OpenResource(resourceHandle);
        }

    }

    public class FileSystemBinder : IModelBinder
    {
        public const string CONFIGURATION_NAME = "cfg/{configurationName}";
        public const string FILE_SYSTEM_EXPLICIT = "p/{productCode}/b/{buildConfiguration}/s/{serverConfiguration}";

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);
            var configurationName = bindingContext.ValueProvider.GetValue("configurationName");

            var resolver = new FileSystemResolver()
            {
                ConfigurationName = GetParameterValue(bindingContext, "configurationName"),

                ProductCode = GetParameterValue(bindingContext, "productCode"),
                BuildConfig = GetParameterValue(bindingContext, "buildConfiguration", x => x.AsKey<EncodingKey>()),
                ServerConfig = GetParameterValue(bindingContext, "serverConfiguration", x => x.AsKey<EncodingKey>()),
            };

            var resolvable = resolver.ConfigurationName is not null
                || (resolver.ProductCode is not null && resolver.BuildConfig != EncodingKey.Zero && resolver.ServerConfig != EncodingKey.Zero);

            bindingContext.Result = resolvable
                ? ModelBindingResult.Success(resolver)
                : ModelBindingResult.Failed();

            return Task.CompletedTask;
        }

        private string? GetParameterValue(ModelBindingContext context, string propertyName)
            => GetParameterValue(context, propertyName, x => x);

        private T? GetParameterValue<T>(ModelBindingContext context, string propertyName, Func<string, T> parser)
        {
            var propertyValue = context.ValueProvider.GetValue(propertyName);
            if (propertyValue == ValueProviderResult.None || string.IsNullOrEmpty(propertyValue.FirstValue))
                return default;

            return parser(propertyValue.FirstValue);
        }
    }

    // TODO: Composite binding source (path(s), querystring, body... you name it)
}
