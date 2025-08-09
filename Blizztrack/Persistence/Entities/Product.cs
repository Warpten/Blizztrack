using Blizztrack.Framework.Ribbit;
using Microsoft.EntityFrameworkCore;

using System.ComponentModel.DataAnnotations.Schema;

namespace Blizztrack.Persistence.Entities
{
    /// <summary>
    /// This table stores the last seen sequence numbers on a product.
    /// </summary>
    [Index(nameof(Code), IsUnique = true)]
    [Table("Product", Schema = "ribbit")]
    public class Product
    {
        public uint ID { get; set; }

        /// <summary>
        /// The product code.
        /// </summary>
        public required string Code { get; set; }

        public int GetSequenceNumber(SequenceNumberType type)
        {
            return type switch {
                SequenceNumberType.Version => Version,
                SequenceNumberType.CDN => CDN,
                SequenceNumberType.BGDL => BGDL,
                _ => 0
            };
        }

        internal bool CanPublishUpdate(int[] sequenceNumbers)
        {
            foreach (var sequenceNumberType in Enum.GetValues<SequenceNumberType>())
                if (GetSequenceNumber(sequenceNumberType) != sequenceNumbers[(int) sequenceNumberType])
                    return true;

            return false;
        }

        /// <summary>
        /// Its last seen CDN sequence number.
        /// </summary>
        public int CDN { get; set; } = 0;

        /// <summary>
        /// The last seen version sequence number for this product.
        /// </summary>
        public int Version { get; set; } = 0;

        /// <summary>
        /// The last seen BGDL sequence number for this product.
        /// </summary>
        public int BGDL { get; set; } = 0;

        /// <summary>
        /// The set of endpoints this product can use.
        /// </summary>
        public List<Endpoint> Endpoints { get; set; } = [];

        /// <summary>
        /// A set of configurations that use this product.
        /// </summary>
        public List<ProductConfig> Configurations { get; set; } = [];
    }
}
