using Asp.Versioning;

using Blizztrack.API.Converters;
using Blizztrack.Caching;
using Blizztrack.Discord;
using Blizztrack.Extensions;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Services;
using Blizztrack.Options;
using Blizztrack.Persistence;
using Blizztrack.Services;
using Blizztrack.Services.Hosted;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Services;

using Polly;

using System.Text.Json;

namespace Blizztrack
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services
                .AddDbContextPool<DatabaseContext>(opt =>
                {
                    var backendSection = builder.Configuration.GetSection("Backend").Get<DatabaseConnectionOptions>();
                    if (backendSection is null)
                        throw new InvalidProgramException();

                    opt.UseNpgsql(backendSection.ToString());
                })
                .AddHttpClient()
                // Configuration sections
                .Configure<Settings>(builder.Configuration)
                .AddSingleton<IOptionsMonitor<Settings>, OptionsMonitor<Settings>>()
                .AddSingleton<MediatorService>()
                .AddHostedService<SummaryMonitorService>()
                .AddSingleton<ContentService>()
                .AddSingleton<LocalCacheService>()
                .AddSingleton<ResourceLocatorService>()
                // Global repositories that reuse object instances.
                .AddSingleton<InstallRepository>();
                // Discord API
                // .AddDiscordGateway()
                // .AddApplicationCommands()
                ;

            builder.Services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new KeyConverter<EncodingKey>());
            });
            builder.Services.AddProblemDetails();
            builder.Services.AddOpenApi();

            // TODO: Make this automatically happen
            builder.Services.AddOpenApiDocument(config =>
            {
                config.DocumentName = "v1";
                config.ApiGroupNames = ["v1"];

                config.Title = "Blizztrack";
                config.Description = """
                This file provides a detailed specification of the various API endpoints exposed by Blizztrack. A short glossary of various terms is provided below.
                | Term             | Description                                                                                                                                                                                                                                                                                                                  |
                |------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
                | **Encoding key** | A hash (usually, but not necessarily MD5) of the potentially encoded file. <br>1. For chunkless [BLTE](https://wowdev.wiki/BLTE) files, this hash covers the entirety of the encoded file. <br>2. For chunked [BLTE](https://wowdev.wiki/BLTE) files, this hash covers only the BLTE headers as specified by the `headerSize` field. |
                | **Content key**  | A hash (usually, but not necessarily MD5) of the decompressed file. |
                | **BLTE**         | A proprietary compression format used by Blizzard to reduce the size of various files. More information can be found [here](https://wowdev.wiki/BLTE).                                                                                                                                                                       |
                | **Encoding manifest** | A file that maps **content keys** to **encoding keys**, as well as providing information on how files are BLTE-encoded. |
                | **Install manifest** | A file that lists files that should be installed on disk for a specific game. Because this file is shared across architectures and operating systems, it also contains tags that allow selection of a subset of files. |
                | **Root manifest**| A file that serves as an index of all known content keys for a game configuration. It can also contain Jenkins96 hashes of the file's complete path within the virtual filesystem that TACT and CASC represent. |
                """;
            });

            builder.Services
                .AddApiVersioning(o => {
                    o.ApiVersionReader = new UrlSegmentApiVersionReader();
                    o.AssumeDefaultVersionWhenUnspecified = true;
                    o.DefaultApiVersion = new ApiVersion(1, 0);
                })
                .AddMvc()
                .AddApiExplorer(o =>
                {
                    o.GroupNameFormat = "'v'VVV";
                    o.DefaultApiVersion = new ApiVersion(1, 0);
                    o.AssumeDefaultVersionWhenUnspecified = true;
                    o.SubstituteApiVersionInUrl = true;
                });

            var host = builder.Build();
            if (host.Environment.IsDevelopment())
            {
                host.UseOpenApi();
                host.UseSwaggerUi();
            }

            host.UseHttpsRedirection()
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapControllers());

            // host.AddApplicationCommandModule<FileCommandModule>().UseGatewayEventHandlers();
            await host.RunAsync();
        }
    }
}
