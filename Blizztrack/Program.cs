using Asp.Versioning;

using Blizztrack.API;
using Blizztrack.API.Bindings;
using Blizztrack.API.Converters;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;
using Blizztrack.Persistence;
using Blizztrack.Persistence.Translators;
using Blizztrack.Services;
using Blizztrack.Services.Caching;
using Blizztrack.Services.Hosted;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Reflection;

namespace Blizztrack
{
    public class Program
    {
        public static readonly ActivitySource ActivitySupplier;

        public static CancellableActivity StartCancellableActivity(string activityName, ActivityKind activityKind, CancellationToken stoppingToken)
        {
            var currentActivity = Activity.Current;
            var newActivity = ActivitySupplier.CreateActivity(activityName, activityKind);
            var cancellable = new CancellableActivity(currentActivity, newActivity, stoppingToken);

            return cancellable;
        }

        public static Activity? StartTaggedActivity(string activityName, Func<IEnumerable<ValueTuple<string, object?>>> tags)
        {
            var activity = ActivitySupplier.StartActivity(activityName);
            if (activity is not null)
                foreach (var (k, v) in tags())
                    activity.AddTag(k, v);

            return activity;
        }

        public static CancellableActivity StartCancellableActivity(string activityName, CancellationToken stoppingToken)
            => StartCancellableActivity(activityName, ActivityKind.Internal, stoppingToken);

        public readonly ref struct CancellableActivity(Activity? previous, Activity? current, CancellationToken stoppingToken)
        {
            public readonly Activity? Activity = current;

            private readonly Activity? _previous = previous;
            private readonly CancellationTokenRegistration _registration = stoppingToken.Register(() => Cancel(current, previous));

            private static void Cancel(Activity? current, Activity? previous)
            {
                if (current is not null)
                {
                    current.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    current.IsAllDataRequested = false;
                }

                Activity.Current = previous;
            }

            public readonly void Cancel() => Cancel(Activity, _previous);

            public void Dispose()
            {
                // Unregister the cancellation
                _registration.Dispose();

                // And finish the activity.
                Activity?.Dispose();
            }
        }

        static Program()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            ActivitySupplier = new ActivitySource(assembly.Name!, assembly.Version!.ToString());
        }

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var telemetryEndpoint = builder.Configuration["Telemetry"];
            if (telemetryEndpoint is not null)
            {
                var openTelemetry = builder.Services.AddOpenTelemetry();
                openTelemetry.ConfigureResource(resource => resource.AddService(serviceName: builder.Environment.ApplicationName))
                    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation()
                        .AddFusionCacheInstrumentation()
                        .AddMeter(nameof(Polly))
                        .AddMeter("Microsoft.AspNetCore." + nameof(Microsoft.AspNetCore.Hosting))
                        .AddMeter("Microsoft.AspNetCore.Server." + nameof(Microsoft.AspNetCore.Server.Kestrel))
                        .AddMeter("System.Net." + nameof(System.Net.Http))
                        .AddOtlpExporter(options => options.Endpoint = new(telemetryEndpoint)))
                    .WithTracing(tracing => tracing.AddHttpClientInstrumentation()
                        .AddEntityFrameworkCoreInstrumentation()
                        .AddFusionCacheInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddSource(ActivitySupplier.Name)
                        .AddOtlpExporter(options => options.Endpoint = new(telemetryEndpoint)));
            }

            builder.Services
                .AddDbContextPool<DatabaseContext>(opt =>
                {
                    var backendSection = builder.Configuration.GetSection("Backend").Get<DatabaseConnectionOptions>()
                        ?? throw new InvalidProgramException();
                    opt.ReplaceService<IMethodCallTranslatorProvider, CustomTranslators>();
                    opt.UseNpgsql(backendSection.ToString());
                })
                .AddHttpClient()
                // Configuration sections
                .Configure<Settings>(builder.Configuration)
                .AddSingleton<IOptionsMonitor<Settings>, OptionsMonitor<Settings>>()
                .AddSingleton<MediatorService>()
                .AddHostedService<SummaryMonitorService>()
                .AddHostedService<KnownFilesMonitorService>()
                .AddSingleton<ContentService>()
                .AddSingleton<LocalCacheService>()
                .AddSingleton<IResourceLocator, ResourceLocatorService>()
                // Repositories that provide shared instances of various TACT file types.
                .AddSingleton<InstallCache>()
                .AddSingleton<EncodingCache>()
                // Discord API
                // .AddDiscordGateway()
                // .AddApplicationCommands()
                ;

            builder.Services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new KeyConverter<EncodingKey>());
                o.JsonSerializerOptions.Converters.Add(new KeyConverter<ContentKey>());
            });
            builder.Services.AddProblemDetails();
            builder.Services.AddOpenApi();

            // TODO: Make this automatically happen
            builder.Services.AddOpenApiDocument(config =>
            {
                config.SchemaSettings.TypeMappers.Add(new KeyBinder<EncodingKey>.Mapper());
                config.SchemaSettings.TypeMappers.Add(new KeyBinder<ContentKey>.Mapper());

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
