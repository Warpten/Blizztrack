using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;

namespace Blizztrack.Services
{
    public class EncodingRepository(IServiceProvider serviceProvider) : FileRepository<Encoding>(serviceProvider, "encoding", static e => e.Encoding)
    {
        protected override Encoding Open(ResourceHandle resourceHandle)
        {
            var decompressed = BLTE.Parse(resourceHandle);
            return Encoding.Open(new InMemoryDataSupplier(decompressed));
        }
    }
}
