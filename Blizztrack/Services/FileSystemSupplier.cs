using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Services.Caching;
using Blizztrack.Shared.IO;

using System.Runtime.CompilerServices;

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

            var encodingTask = encodingRepository.Obtain(productCode, buildConfiguration.Encoding.Content.Key, buildConfiguration.Encoding.Encoding.Key, stoppingToken)
                .AsTask();

            var indexTasks = cdnConfiguration.Archives
                .Select(archive => OpenIndex(productCode, archive, locator, stoppingToken));

            var fileIndexTask = cdnConfiguration.FileIndex.Size != 0
                ? OpenIndex(productCode, cdnConfiguration.FileIndex.Key, locator, stoppingToken)
                : Task.FromResult<IIndex?>(default);

            await Task.WhenAll([encodingTask, fileIndexTask, ..indexTasks]);

            var fileIndex = await fileIndexTask;
            var encoding = await encodingTask;
            var compoundedIndex = new CompoundingIndex([.. indexTasks.Select(i => i.Result!)]);

            var rootTask = ResolveRoot(productCode, encoding, compoundedIndex, fileIndex, buildConfiguration.Root, stoppingToken);
            var installTask = installRepository.Obtain(productCode, buildConfiguration.Install.Content.Key, buildConfiguration.Install.Encoding.Key, stoppingToken).AsTask();

            await Task.WhenAll([rootTask, installTask]);

            var root = await rootTask;
            var install = await installTask;

#pragma warning disable BT002
            return new FileSystem<CompoundingIndex>(productCode, compoundedIndex, encoding, root, install, fileIndex);
#pragma warning restore BT002
        }

        private async Task<Root?> ResolveRoot(string productCode, Encoding encoding, CompoundingIndex compoundedIndex, IIndex? fileIndex, ContentKey root, CancellationToken stoppingToken)
        {
            var results = encoding.FindContentKey(root);
            if (results.Count == 0)
                return default;

            foreach (Framework.TACT.Views.EncodingKey encodingKey in results.Keys)
            {
                var archiveInfo = compoundedIndex.FindEncodingKey(in encodingKey);
                if (!archiveInfo && fileIndex is not null)
                    archiveInfo = fileIndex.FindEncodingKey(in encodingKey);

                if (archiveInfo)
                    return await rootRepository.Obtain(productCode, archiveInfo.Archive, stoppingToken);
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<IIndex?> OpenIndex(string productCode, EncodingKey encodingKey, IResourceLocator locator, CancellationToken stoppingToken)
        {
            var descriptor = ResourceType.Indice.ToDescriptor(productCode, encodingKey, ContentKey.Zero);
            var handle = await locator.OpenHandle(descriptor, stoppingToken);
            // TODO: Pool this call.
            return Index.Open(handle.ToMappedDataSource(), encodingKey);
        }
    }
}
