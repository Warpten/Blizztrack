using Microsoft.EntityFrameworkCore;

using System.ComponentModel.DataAnnotations.Schema;

namespace Blizztrack.Persistence.Entities
{
    [Index(nameof(Host), IsUnique = true)]
    [Table("Endpoint", Schema = "ribbit")]
    public class Endpoint
    {
        public int ID { get; set; }

        public string[] Regions { get; set; } = [];

        /// <summary>
        /// The actual host name.
        /// </summary>
        public required string Host { get; set; }

        public required string DataPath { get; set; }

        public required string ConfigurationPath { get; set; }

        public List<Product> Products { get; set; } = [];
    }
}
