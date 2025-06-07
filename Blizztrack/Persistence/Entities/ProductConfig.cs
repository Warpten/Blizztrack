using Blizztrack.Framework.TACT;

using Microsoft.EntityFrameworkCore;

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Blizztrack.Persistence.Entities
{
    /// <summary>
    /// This object is an aggregate of a <see cref="Framework.Ribbit.CDN"/> and <see cref="Framework.Ribbit.Version"/> tuple.
    /// </summary>
    [Table("ProductConfiguration", Schema = "tact")]
    public class ProductConfig
    {
        [JsonIgnore]
        public uint ID { get; set; }
        public required uint BuildID { get; set; }

        /// <summary>
        /// The product that provided, at some date, this build configuration.
        /// </summary>
        public required Product Product { get; set; }

        /// <summary>
        /// The actual name of the build.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// The date at which this configuration was detected by Blizztrack.
        /// </summary>
        public DateTime DetectionDate { get; init; } = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);

        /// <summary>
        /// An encoding key that refers to a <see cref="Framework.TACT.Configuration">Build Configuration</see> file.
        /// </summary>
        public required EncodingKey BuildConfig { get; set; }
        public required EncodingKey CDNConfig { get; set; }
        public required EncodingKey KeyRing { get; set; }
        public required EncodingKey Config { get; set; }

        /// <summary>
        /// The regions to which this configuration is available.
        /// </summary>
        public required string[] Regions { get; set; }
    }
}
