using Asp.Versioning;

using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;

using NSwag.Annotations;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Blizztrack.API
{
    [ApiVersion(1.0)]
    [Tags("Utility endpoints"), OpenApiTag("Utility endpoints", Description = "Endpoints in this category provide utilities to manipulate and explore architectural TACT files.")]
    [ApiController, Route("api/v{version:apiVersion}/encoding")]
    public class EncodingController(EncodingRepository encodingRepository) : ControllerBase
    {
        [HttpGet("{encodingManifest}/{pageIndex}")]
        [OpenApiOperation("Enumerate encoding manifest entries", """
            Returns a collection of all entries in the given encoding manifest.
            
            Each entry associates a single content key with one or many encoding keys, as well as the decompressed size of the file.
            """)]
        public async IAsyncEnumerable<EncodingEntry> EnumerateEntries(
            [Description("An encoding key of the encoding file to enumerate, as seen in build configuration files.")] string encodingManifest,
            [Description("The index of the page to enumerate")] int pageIndex,
            [EnumeratorCancellation] CancellationToken stoppingToken)
        {
            var encodingFile = await OpenEncoding(encodingManifest, stoppingToken);
            foreach (var encodingEntry in encodingFile.Entries.Pages[pageIndex])
                yield return new EncodingEntry(encodingEntry);
        }

        [HttpGet("{encodingManifest}/pages")]
        [OpenApiOperation("Retrieves informations on pages within the encoding file", """
            Returns the amount of entry and specification pages within this file.
            """)]
        public async Task<EncodingMetadata> GetEncodingMetadata(string encodingManifest, CancellationToken stoppingToken)
        {
            var encodingFile = await OpenEncoding(encodingManifest, stoppingToken);
            return new EncodingMetadata(encodingFile.Entries.Pages.Count, encodingFile.Specifications.Pages.Count);
        }

        private ValueTask<Encoding> OpenEncoding(string fileName, CancellationToken stoppingToken)
            => encodingRepository.Obtain(fileName, stoppingToken);

        public record struct EncodingMetadata(int EntryPages, int SpecificationPages);

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
