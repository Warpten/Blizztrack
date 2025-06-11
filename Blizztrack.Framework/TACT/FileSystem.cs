using Blizztrack.Framework.IO;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Shared.Extensions;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Encoding = Blizztrack.Framework.TACT.Implementation.Encoding;

namespace Blizztrack.Framework.TACT
{
    /// <summary>
    /// A filesystem provides access to a variety of files within a single installation of a game.
    /// 
    /// <para>
    /// If you run into a situation where multiple filesystems may be open in parallel and they may share files,
    /// consider acquiring them from a <see cref="FileSystemProvider"/>.
    /// </para>
    /// </summary>
    public class FileSystem(string product, EncodingKey[] archives, Encoding? encoding = default, Root? root = default, Install? install = default, FileIndex? fileIndex = default, GroupIndex? groupIndex = default)
    {
        internal readonly string _product = product;
        internal readonly EncodingKey[] Archives = archives;
        internal readonly Encoding? Encoding = encoding;
        internal readonly Root? Root = root;
        internal readonly Install? Install = install;
        internal readonly GroupIndex? GroupIndex = groupIndex;
        internal readonly FileIndex? FileIndex = fileIndex;

        public IEnumerable<ResourceDescriptor> OpenFDID(uint fileDataID)
        {
            if (Root == default)
                return [];

            // TODO: Smell: MD5 != IContentKey<MD5>.
            var rootResult = Root.FindFileDataID(fileDataID);
            return OpenContentKey(rootResult.ContentKey.AsReadOnlySpan().AsKey<ContentKeyRef>());
        }

        /// <summary>
        /// Retrieves every resource descriptor that corresponds to a given content key.
        /// </summary>
        /// <typeparam name="T">The concrete type of content key used.</typeparam>
        /// <param name="contentKey">The content key to look for.</param>
        /// <returns></returns>
        public ResourceDescriptor[] OpenContentKey<T>(T contentKey) where T : IContentKey<T>, IKey, allows ref struct
        {
            var encodingResult = Encoding == null ? default : Encoding.FindContentKey(contentKey);

            var results = new ResourceDescriptor[encodingResult.Count];
            for (var i = 0; i < encodingResult.Count; ++i)
                results[i] = OpenEncodingKey(encodingResult[i]);

            return results;
        }

        [Experimental(diagnosticId: "BT001")]
        public Enumerator Resources => new(_product, Archives, GroupIndex, FileIndex);

        public readonly ref struct Enumerator(string product, EncodingKey[] archives, AbstractIndex? groupIndex, AbstractIndex? fileIndex)
        {
            private readonly string _product = product;
            private readonly EncodingKey[] _archives = archives;
#pragma warning disable BT002
            private readonly AbstractIndex.Enumerator _groupIndex = groupIndex == null ? default : groupIndex.Entries;
            private readonly AbstractIndex.Enumerator _fileIndex = fileIndex == null ? default : fileIndex.Entries;
#pragma warning restore BT002

            public Enumerator GetEnumerator() => this;

            public bool MoveNext() => _groupIndex.MoveNext() || _fileIndex.MoveNext();

            public ResourceDescriptor Current
            {
                get
                {
                    var currentValue = _groupIndex.Current;
                    if (currentValue != default)
                        return new ResourceDescriptor(ResourceType.Data, _product, currentValue.EncodingKey.AsHexString(), currentValue.Offset, currentValue.Length);

                    currentValue = _fileIndex.Current;
                    if (currentValue != default)
                        return new ResourceDescriptor(ResourceType.Data, _product, _archives[currentValue.ArchiveIndex].AsHexString(), currentValue.Offset, currentValue.Length);

                    return default;
                }
            }
        }

        public ResourceDescriptor OpenEncodingKey<T>(T encodingKey) where T : IEncodingKey<T>, IKey, allows ref struct
        {
            // Try general-purpose indices first
            if (GroupIndex is not null)
            {
                var indexResult = GroupIndex.FindEncodingKey(encodingKey);
                if (indexResult)
                    return new ResourceDescriptor(ResourceType.Data, _product,
                        Archives.UnsafeIndex(indexResult.ArchiveIndex).AsHexString(), indexResult.Offset, indexResult.Length);
            }

            if (FileIndex is not null)
            {
                // Try non-archived files
                var indexResult = FileIndex.FindEncodingKey(encodingKey);
                if (indexResult)
                    return new ResourceDescriptor(ResourceType.Data, _product,
                        Archives.UnsafeIndex(indexResult.ArchiveIndex).AsHexString(), indexResult.Offset, indexResult.Length);
            }

            // Assume the file is a self-contained archive
            return new ResourceDescriptor(ResourceType.Data, _product, encodingKey.AsHexString());
        }

        /// <summary>
        /// Opens a file system according to the given configuration, resolving internal data structures via the provided resource locator.
        /// </summary>
        /// <typeparam name="T">The concrete type of resource locator.</typeparam>
        /// <param name="buildConfiguration">The configuration for the game build that is being opened.</param>
        /// <param name="serverConfiguration">The CDN configuration for the game build that is being opened.</param>
        /// <param name="locator">An instance of <see cref="IResourceLocator"/> that allows to locate files on disk.</param>
        /// <param name="stoppingToken">A stop token for asynchronous operations.</param>
        /// <returns></returns>
        public async static Task<FileSystem> Open<T>(BuildConfiguration buildConfiguration, ServerConfiguration serverConfiguration,
            string product, T locator, CancellationToken stoppingToken)
            where T : IResourceLocator
        {
            Debug.Assert(buildConfiguration.Encoding.ContentKey.Key != ContentKey.Zero);

            // 1. Load archive-group (or each individual indice, aggregated in a single IIndex implementation).
            var indicesTask = OpenIndices(buildConfiguration, serverConfiguration, product, locator, stoppingToken);

            // 2. Load file-index (if it exists, but ignore if it doesn't)
            var fileIndex = Open(serverConfiguration.FileIndex.Key, product, locator, stoppingToken)
                .ContinueWith(static x => x.Exception == default ? null : new FileIndex(x.Result));

            // 3. Load encoding (must exist)
            var encodingHandle = await Open(buildConfiguration.Encoding.EncodingKey, product, locator, stoppingToken);
            var encodingInstance = Encoding.Open(new MappedMemoryData(encodingHandle));

            // 4. Load root (if it exists)
            var rootHandle = Open(buildConfiguration.Root, product, locator, encodingInstance, stoppingToken);

            // 5. Load install (if it exists)
            var installHandle = Open(buildConfiguration.Install, product, locator, encodingInstance, stoppingToken);

            // Execute tasks in parallel and wait for everything.
            await Task.WhenAll(rootHandle, installHandle, indicesTask, fileIndex);

            var rootInstance = rootHandle.Result != default ? Root.OpenResource(rootHandle.Result) : null;
            var installInstance = installHandle.Result != default ? Install.Open(new MappedMemoryData(installHandle.Result)) : null;

            return new FileSystem(product, serverConfiguration.Archives, encodingInstance, rootInstance, installInstance);
        }

        private static async Task<ResourceHandle> Open<T, U>((SizeAware<T> Content, SizeAware<U> Encoding) pair,
            string product,
            IResourceLocator locator,
            Encoding encoding,
            CancellationToken stoppingToken)
            where T : IContentKey<T>, IKey
            where U : IEncodingKey<U>, IKey
        {
            var decompressedDescriptor = new ResourceDescriptor(ResourceType.Data, product, pair.Encoding.Key.AsHexString(), 0, pair.Encoding.Size);
            var decompressedHandle = await locator.OpenCompressedHandle(product, pair.Encoding.Key, pair.Content.Key, stoppingToken);
            if (decompressedHandle != default)
                return decompressedHandle;

            return await Open(pair.Content, product, locator, encoding, stoppingToken);
        }

        private static async Task<ResourceHandle> Open<T>(SizeAware<T> contentKey,
            string product,
            IResourceLocator locator,
            Encoding encoding,
            CancellationToken stoppingToken)
            where T : IContentKey<T>, IKey
        {
            var encodingEntry = encoding.FindContentKey(contentKey.Key);
            if (encodingEntry == default)
                return default;

            var queryTasks = new Task<ResourceHandle>[encodingEntry.Count];
            for (var i = 0; i < encodingEntry.Count; ++i)
                queryTasks[i] = locator.OpenCompressedHandle(product, encodingEntry[i], contentKey.Key, stoppingToken);

            await foreach (var queryTask in Task.WhenEach(queryTasks))
                if (queryTask.IsCompletedSuccessfully)
                    return queryTask.Result;

            return default;
        }

        private static async Task<ResourceHandle> Open<T>(T contentKey,
            string product,
            IResourceLocator locator,
            Encoding encoding,
            CancellationToken stoppingToken)
            where T : IContentKey<T>, IKey
        {
            var encodingEntry = encoding.FindContentKey(contentKey);
            if (encodingEntry == default)
                return default;

            var queryTasks = new Task<ResourceHandle>[encodingEntry.Count];
            for (var i = 0; i < encodingEntry.Count; ++i)
                queryTasks[i] = locator.OpenCompressedHandle(product, encodingEntry[i], contentKey, stoppingToken);

            await foreach (var queryTask in Task.WhenEach(queryTasks))
                if (queryTask.IsCompletedSuccessfully)
                    return queryTask.Result;

            return default;
        }

        private static async Task<ResourceHandle> Open<T>(T encodingKey,
            string product,
            IResourceLocator locator,
            CancellationToken stoppingToken)
            where T : IEncodingKey<T>, IKey
            => await locator.OpenCompressedHandle(product, encodingKey, stoppingToken);

        private static async Task<ResourceHandle> Open<T>(SizeAware<T> encodingKey,
            string product,
            IResourceLocator locator,
            CancellationToken stoppingToken)
            where T : IEncodingKey<T>, IKey
            => await locator.OpenCompressedHandle(product, encodingKey.Key, stoppingToken);

        private static async Task<IIndex[]> OpenIndices(BuildConfiguration buildConfiguration,
            ServerConfiguration serverConfiguration,
            string product,
            IResourceLocator locator,
            CancellationToken stoppingToken)
        {
            if (serverConfiguration.ArchiveGroup == default)
            {
                var indicesTasks = serverConfiguration.Archives
                    .Select(archive => new ResourceDescriptor(ResourceType.Indice, product, $"{archive.AsHexString()}.index"))
                    .Select(async (descriptor, index) =>
                    {
                        var resourceHandle = await locator.OpenHandle(descriptor, stoppingToken);
                        return new ArchiveIndex(resourceHandle, (short) index);
                    });

                var indices = new IIndex[serverConfiguration.Archives.Length];

                await foreach (var locatingTask in Task.WhenEach(indicesTasks))
                {
                    var indexFile = await locatingTask;
                    indices.UnsafeIndex(indexFile.ArchiveIndex) = indexFile;
                }

                return indices;
            }
            else
            {
                var descriptor = new ResourceDescriptor(ResourceType.Indice, product, $"{serverConfiguration.ArchiveGroup.AsHexString()}.index");
                var resourceHandle = await locator.OpenHandle(descriptor, stoppingToken);
                return [new GroupIndex(resourceHandle)];
            }
        }
    }
}
