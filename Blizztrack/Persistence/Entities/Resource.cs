using Blizztrack.Framework.TACT;

using Microsoft.EntityFrameworkCore;

using System.ComponentModel.DataAnnotations.Schema;

namespace Blizztrack.Persistence.Entities
{
    [Table("KnownResource", Schema = "tact")]
    [Index(nameof(ContentKey)), Index(nameof(EncodingKey))]
    public class KnownResource
    {
        public uint ID { get; set; }

        /// <summary>
        /// A displayable string that specifies what kind of known resource this is.
        /// </summary>
        public required string Kind { get; set; }

        /// <summary>
        /// The encoding key used to identify the compressed file.
        /// </summary>
        public EncodingKey EncodingKey { get; set; }

        /// <summary>
        /// The content key used to identify the decompressed file.
        /// </summary>
        public ContentKey ContentKey { get; set; }

        /// <summary>
        /// A specification string that can be used to regenerate the compressed file.
        /// </summary>
        public required string Specification { get; set; }
    }
}
