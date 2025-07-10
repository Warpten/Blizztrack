using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Services.Caching;

using System.Text;
using System.Threading.Tasks;

using Encoding = Blizztrack.Framework.TACT.Implementation.Encoding;
using Index = Blizztrack.Framework.TACT.Implementation.Index;

namespace Blizztrack.Services
{
    public class FileSystemSupplier(EncodingCache encodingRepository, InstallCache installRepository, RootCache rootRepository, IndexCache indexRepository)
    {
        public async Task<FileSystem> OpenFileSystem(string productCode, BuildConfiguration buildConfiguration, ServerConfiguration cdnConfiguration)
        {
            // NOTE: We don't rely on generating group indices because it allows us to reuse indices across multiple filesystems.
            var sequencedLoader = SequencedLoader(productCode, buildConfiguration, cdnConfiguration);
            var indexTasks = cdnConfiguration.Archives
                .Select((index, archive) => indexRepository.Obtain(productCode, archive).AsTask());

            var indice = 

            await Task.WhenAll([sequencedLoader, ..indexTasks]);

            var (encoding, root) = await sequencedLoader;
            var indices = indexTasks.Select(i => i.Result).ToArray();

            var install = installRepository.Obtain(productCode, buildConfiguration.Install.Content.Key, buildConfiguration.Install.Encoding.Key);

            // We don't rely on archive-group here because indices can be reused across installs (seems relatively common too)

            IIndex? fileIndex = cdnConfiguration.FileIndex.Size != 0
                ? await indexRepository.Obtain(productCode, cdnConfiguration.FileIndex.Key)
                : default;

            return new FileSystem(productCode, encoding, root, install, fileIndex);
        }

        public async Task<(Encoding, Root?)> SequencedLoader(string productCode, BuildConfiguration buildConfiguration, ServerConfiguration cdnConfiguration, CancellationToken stoppingToken = default)
        {
            var encoding = await encodingRepository.Obtain(productCode, buildConfiguration.Encoding.Content.Key, buildConfiguration.Encoding.Encoding.Key, stoppingToken);

            var rootEntry = encoding.FindContentKey(buildConfiguration.Root);
            for (var i = 0; i < rootEntry.Count; ++i)
            {
                // Have to transition to an owning type for the corresponding ekey so that async boundary can be crossed
                var root = await rootRepository.Obtain(productCode, buildConfiguration.Root, rootEntry[i].AsOwned(), stoppingToken);
                if (root != null)
                    return (encoding, root);
            }

            return (encoding, null);

        }
    }
}
