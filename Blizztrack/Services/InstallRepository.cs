using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;

namespace Blizztrack.Services
{
    public class InstallRepository(IServiceProvider serviceProvider) : FileRepository<Install>(serviceProvider, "install", static e => e.Install)
    {
        protected override Install Open(ResourceHandle resourceHandle)
        {
            var decompressed = BLTE.Parse(resourceHandle);
            return Install.Open(new InMemoryDataSupplier(decompressed));
        }
    }
}
