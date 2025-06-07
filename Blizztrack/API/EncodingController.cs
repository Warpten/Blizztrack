using Asp.Versioning;

using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;

using NSwag.Annotations;

using System.ComponentModel;

namespace Blizztrack.API
{
    [ApiVersion(1.0)]
    [Tags("Utility endpoints"), OpenApiTag("Utility endpoints", Description = "Endpoints in this category provide utilities to manipulate and explore architectural TACT files.")]
    [ApiController, Route("api/v{version:apiVersion}/encoding")]
    public class EncodingController(ResourceLocatorService locator, DatabaseContext databaseContext) : ControllerBase
    {
        [HttpGet("{fileHash}/entries")]
        [OpenApiOperation("Enumerate encoding manifest entries", """
            Returns a collection of all entries in the given encoding manifest.
            
            Each entry associates a single content key with one or many encoding keys, as well as the decompressed size of the file.
            """)]
        public async Task<List<EncodingEntry>> EnumerateEntries(
            [Description("An encoding key of the encoding file to enumerate, as seen in build configuration files.")] string fileHash,
            CancellationToken stoppingToken)
        {
            var entries = new List<EncodingEntry>();

            var encodingFile = await OpenEncoding(fileHash, stoppingToken);
            foreach (var encodingEntry in encodingFile.Entries)
                entries.Add(new EncodingEntry(encodingEntry));

            return entries;
        }

        private async Task<Encoding> OpenEncoding(string fileName, CancellationToken stoppingToken)
        {
            var endpoints = databaseContext.Endpoints.Select(e => new PatchEndpoint(e.Host, e.DataPath, e.ConfigurationPath))
                .ToAsyncEnumerable();

            var resourceDescriptor = new ResourceDescriptor(ResourceType.Data, fileName);
            var resourceHandle = await locator.OpenHandleAsync(endpoints, resourceDescriptor, stoppingToken);
            if (resourceHandle == default)
                throw new FileNotFoundException(fileName);

            return Encoding.Open(new MemoryMappedDataSupplier(resourceHandle));
        }

        public record struct EncodingEntry(string ContentKey, ulong FileSize, string[] Keys)
        {
            public EncodingEntry(Encoding.Entry entry) : this(entry.ContentKey.AsHexString(), entry.FileSize, MapKeys(ref entry)) { }

            private static string[] MapKeys(ref Encoding.Entry entry)
            {
                var keys = GC.AllocateUninitializedArray<string>(entry.Count);
                for (var i = 0; i < entry.Count; ++i)
                    keys[i] = entry[i].AsHexString();
                return keys;
            }
        }
    }
}
