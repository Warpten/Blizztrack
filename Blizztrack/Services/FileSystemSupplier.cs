using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Services.Caching;
using Blizztrack.Shared.IO;

using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Encoding = Blizztrack.Framework.TACT.Implementation.Encoding;
using Index = Blizztrack.Framework.TACT.Implementation.Index;

namespace Blizztrack.Services
{
    public class FileSystemSupplier(EncodingCache encodingRepository, InstallCache installRepository, RootCache rootRepository)
    {
        public async Task<IFileSystem> OpenFileSystem(string productCode, BuildConfiguration buildConfiguration, ServerConfiguration cdnConfiguration, IResourceLocator locator,
            CancellationToken stoppingToken = default)
        {
            // NOTE: We don't rely on generating group indices because it allows us to reuse indices across multiple filesystems.
            var sequencedLoader = OpenCore(productCode, buildConfiguration, stoppingToken);
            var indexTasks = cdnConfiguration.Archives
                .Select(archive => OpenIndex(productCode, archive, locator, stoppingToken));

            await Task.WhenAll([sequencedLoader, ..indexTasks]);

            var compoundedIndex = new CompoundingIndex([.. indexTasks.Select(i => i.Result)]);
            var (encoding, root) = await sequencedLoader;

            var install = await installRepository.Obtain(productCode, buildConfiguration.Install.Content.Key, buildConfiguration.Install.Encoding.Key, stoppingToken);

            var fileIndex = cdnConfiguration.FileIndex.Size != 0
                ? await OpenIndex(productCode, cdnConfiguration.FileIndex.Key, locator, stoppingToken)
                : default;

#pragma warning disable BT002
            return new FileSystem<CompoundingIndex, Index<MappedDataSource>>(productCode, compoundedIndex, encoding, root, install, fileIndex);
#pragma warning restore BT002
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<Index<MappedDataSource>> OpenIndex(string productCode, EncodingKey encodingKey, IResourceLocator locator, CancellationToken stoppingToken)
        {
            var descriptor = new ResourceDescriptor(ResourceType.Indice, productCode, encodingKey.AsSpan());
            var handle = await locator.OpenHandle(descriptor, stoppingToken);
            // TODO: Pool this call.
            return Index.Open(handle.ToMappedDataSource(), encodingKey);
        }

        public async Task<(Encoding, Root?)> OpenCore(string productCode, BuildConfiguration buildConfiguration,  CancellationToken stoppingToken)
        {
            var encoding = await encodingRepository.Obtain(productCode, buildConfiguration.Encoding.Content.Key, buildConfiguration.Encoding.Encoding.Key, stoppingToken);

            var encodingEntry = encoding.FindContentKey(buildConfiguration.Root);
            var encodingKeys = encodingEntry.Keys; // Forced in order to go through async boundary.

            for (var i = 0; i < encodingKeys.Length; ++i)
            {
                var root = await rootRepository.Obtain(productCode, buildConfiguration.Root, encodingKeys[i], stoppingToken);
                if (root != null)
                    return (encoding, root);
            }

            return (encoding, null);

        }
    }
}
