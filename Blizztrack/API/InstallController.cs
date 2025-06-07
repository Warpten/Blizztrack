using Asp.Versioning;

using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;

using NSwag.Annotations;

using System.ComponentModel;
using System.Net;

namespace Blizztrack.API
{
    /// <summary>
    /// A controller that lists out the contents of installer files.
    /// </summary>
    [ApiVersion(1.0), Tags("Utility endpoints")]
    [SwaggerResponse(HttpStatusCode.NotFound, null, Description = "If the install manifest file could not be found.")]
    [ApiController, Route("api/v{version:apiVersion}/install")]
    public class InstallController(ResourceLocatorService locator, InstallRepository installRepository) : ControllerBase
    {
        [HttpGet("{installManifest}/entries")]
        [SwaggerResponse(HttpStatusCode.OK, typeof(DetailedInstallEntry[]), Description = "Information about all files within the manifest.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [OpenApiOperation("Enumerate install manifest entries", """
            Returns a collection of all entries in the given install manifest.
            
            An entry associates a path to a file with a content key, the size of said file, and a collection of tags.
            """)]
        public async Task<IResult> EnumerateEntries(
            [Description("An encoding key of the install manifest file to enumerate, as seen in build configuration files.")] string installManifest,
            CancellationToken stoppingToken)
        {
            try
            {
                var installFile = await OpenInstall(installManifest, stoppingToken);

                return TypedResults.Ok(installFile.Entries
                    .Select(entry => new DetailedInstallEntry(entry.Name, entry.ContentKey.AsHexString(),
                        [.. installFile.EnumerateTags(entry).Select(e => new InstallTag(e.Name, e.Type))]))
                    .ToList());
            }
            catch (FileNotFoundException)
            {
                return TypedResults.NotFound();
            }
        }

        [HttpGet("{installManifest}/tags")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerResponse(HttpStatusCode.OK, typeof(InstallTag[]), Description = "A collection of all tags within the manifest.")]
        [OpenApiOperation("Enumerate install manifest tags", "Returns a collection of all tags in the given install manifest.")]
        public async Task<IResult> EnumerateTags(
            [Description("An encoding key of the install manifest file to enumerate, as seen in build configuration files.")] string installManifest, 
            CancellationToken stoppingToken)
        {
            try
            {
                var installFile = await OpenInstall(installManifest, stoppingToken);
                return TypedResults.Ok(installFile.Tags
                    .Select(entry => new InstallTag(entry.Name, entry.Type))
                    .ToList());
            }
            catch (FileNotFoundException)
            {
                return TypedResults.NotFound();
            }
        }

        [HttpGet("{installManifest}/tag/{tagName}")]
        [SwaggerResponse(HttpStatusCode.OK, typeof(DetailedInstallTag[]), Description = "Information about the tag as well as all associated files.")]
        [OpenApiOperation("Lists files associated with a specific tag", "Returns a collection of all the files associated with a given tag.")]
        public async Task<IResult> EnumerateTag(
            [Description("An encoding key of the install manifest file to enumerate, as seen in build configuration files.")] string installManifest,
            [Description("The name of the tag to look for.")] string tagName,
            CancellationToken stoppingToken)
        {
            try
            {
                var installFile = await OpenInstall(installManifest, stoppingToken);

                var installTag = installFile.Tags.SingleOrDefault(t => t.Name == tagName);
                if (installTag.Name != tagName)
                    return TypedResults.NotFound();

                var matchingFiles = installFile.Entries.Where(e => e.Matches(ref installTag))
                    .Select(e => new InstallEntry(e.Name, e.ContentKey.AsHexString()));

                return TypedResults.Ok(new DetailedInstallTag(installTag.Name, installTag.Type, [.. matchingFiles]));
            }
            catch (FileNotFoundException)
            {
                return TypedResults.NotFound();
            }
        }

        private ValueTask<Install> OpenInstall(string fileName, CancellationToken stoppingToken)
            => installRepository.Obtain(fileName, stoppingToken);

        public record InstallEntry(string Name, string ContentKey);
        public record DetailedInstallEntry(string Name, string ContentKey, InstallTag[] Tags);

        public record InstallTag(string Name, ushort Type);
        public record DetailedInstallTag(string Name, ushort Type, InstallEntry[] Files);
    }
}