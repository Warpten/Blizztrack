using Asp.Versioning;

using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services.Caching;

using Microsoft.AspNetCore.Mvc;

using NSwag.Annotations;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using static Blizztrack.API.EncodingController.EncodingEntry;

namespace Blizztrack.API
{
    [ApiVersion(1.0)]
    [Tags("Utility endpoints"), OpenApiTag("Utility endpoints", Description = "Endpoints in this category provide utilities to manipulate and explore architectural TACT files.")]
    [ApiController, Route("api/v{version:apiVersion}/encoding")]
    public class EncodingController(EncodingCache encodingRepository) : ControllerBase
    {
        [HttpGet("{product}/{encodingManifest}/{pageIndex}")]
        [OpenApiOperation("Enumerate encoding manifest entries", """
            Returns a collection of all entries in the given encoding manifest.
            
            Each entry associates a single content key with one or many encoding keys, as well as the decompressed size of the file.
            """)]
        public async IAsyncEnumerable<EncodingEntry> EnumerateEntries(
            [Description("The product that uses this encoding file")]
            string product,
            [Description("The encoding key that identifies the encoding manifest file.")]
            string encodingManifest,
            [Description("The index of the page to enumerate")]
            int pageIndex,
            [EnumeratorCancellation] CancellationToken stoppingToken)
        {
            var encodingFile = await OpenEncoding(product, encodingManifest.AsKey<EncodingKey>(), stoppingToken);
            foreach (var encodingEntry in encodingFile.Entries.Pages[pageIndex])
                yield return new EncodingEntry(encodingFile, encodingEntry);
        }

        [HttpGet("{product}/{encodingManifest}/pages")]
        [OpenApiOperation("Retrieves informations on pages within the encoding file", """
            Returns the amount of entry and specification pages within this file.
            """)]
        public async Task<EncodingMetadata> GetEncodingMetadata(
            [Description("The product that uses this encoding file")]
            string product,
            [Description("The encoding key that identifies the encoding manifest file.")]
            string encodingManifest,
            CancellationToken stoppingToken)
        {
            var encodingFile = await OpenEncoding(product, encodingManifest.AsKey<EncodingKey>(), stoppingToken);
            return new EncodingMetadata(encodingFile.Entries.Pages.Count, encodingFile.Specifications.Pages.Count);
        }

        private ValueTask<Encoding> OpenEncoding<E>(string product, E encodingKey, CancellationToken stoppingToken)
            where E : IEncodingKey<E>, IKey<E>
            => encodingRepository.Obtain(product, encodingKey, stoppingToken);

        public record struct EncodingMetadata(int EntryPages, int SpecificationPages);

        public record struct EncodingEntry(string ContentKey, ulong FileSize, KeySpec[] Keys)
        {
            public EncodingEntry(Encoding encoding, Encoding.Entry entry) : this(entry.ContentKey.AsHexString(), entry.FileSize, MapKeys(encoding, ref entry)) { }

            private static KeySpec[] MapKeys(Encoding encoding, ref Encoding.Entry entry)
            {
                var keys = GC.AllocateUninitializedArray<KeySpec>(entry.Count);
                for (var i = 0; i < entry.Count; ++i)
                    keys[i] = new (entry[i].AsHexString(), encoding.FindSpecification(entry[i]).GetSpecificationString(encoding));
                return keys;
            }

            public record KeySpec(string Key, string Specification);
        }
    }
}
