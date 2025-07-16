using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;

using System.Runtime.CompilerServices;

using Encoding = Blizztrack.Framework.TACT.Implementation.Encoding;
using Index = Blizztrack.Framework.TACT.Implementation.Index;

namespace Blizztrack.Framework.TACT
{
    public interface IFileSystem
    {

        /// <summary>
        /// Retrieves every resource descriptor that corresponds to a given file data ID.
        /// </summary>
        /// <param name="fileDataID">An unique identifier for the file to look for.</param>
        /// <returns>A collection of resource descriptors that match the given identifier.</returns>
        public ResourceDescriptor[] OpenFDID(uint fileDataID);

        /// <summary>
        /// Retrieves every resource descriptor that corresponds to a given content key.
        /// </summary>
        /// <typeparam name="K">The concrete type of content key used.</typeparam>
        /// <param name="contentKey">The content key to look for.</param>
        /// <returns>A collection of resource descriptors.</returns>
        public ResourceDescriptor[] OpenContentKey<T>(T contentKey) where T : IContentKey<T>, allows ref struct;

        /// <summary>
        /// Retrieves a resource descriptor that corresponds to a given encoding key.
        /// </summary>
        /// <typeparam name="K">The concrete type of encoding key used.</typeparam>
        /// <param name="contentKey">The encoding key to look for.</param>
        /// <returns>A resource descriptor.</returns>
        public ResourceDescriptor OpenEncodingKey<T>(T encodingKey) where T : IEncodingKey<T>, allows ref struct;

    }

    /// <summary>
    /// A filesystem provides access to a variety of files within a single installation of a game.
    /// </summary>
    public class FileSystem<T>(string product, T archiveIndices, Encoding? encoding = default, Root? root = default, Install? install = default, Index? fileIndex = default)
        : IFileSystem
        where T : IIndex
    {
        internal readonly string _product = product;
        internal readonly T Archives = archiveIndices;
        internal readonly Encoding? Encoding = encoding;
        internal readonly Root? Root = root;
        internal readonly Install? Install = install;
        internal readonly Index? FileIndex = fileIndex;

        public ResourceDescriptor[] OpenFDID(uint fileDataID)
        {
            if (Root == default)
                return [];

            // TODO: Smell: MD5 != IContentKey<MD5>.
            ref readonly var rootResult = ref Root.FindFileDataID(fileDataID);
            if (Unsafe.IsNullRef(in rootResult))
                return [];

            return OpenContentKey(rootResult.ContentKey.AsReadOnlySpan().AsKey<ContentKeyRef>());
        }

        public ResourceDescriptor[] OpenContentKey<K>(K contentKey) where K : IContentKey<K>, allows ref struct
        {
            var encodingResult = Encoding == null ? default : Encoding.FindContentKey(contentKey);

            var results = new ResourceDescriptor[encodingResult.Count];
            for (var i = 0; i < encodingResult.Count; ++i)
                results[i] = OpenEncodingKey(encodingResult[i]);

            return results;
        }

        public ResourceDescriptor OpenEncodingKey<K>(K encodingKey) where K : IEncodingKey<K>, allows ref struct
        {
            if (FileIndex is not null)
            {
                // Try non-archived files
                var indexResult = FileIndex.FindEncodingKey(encodingKey);
                if (indexResult)
                    return new ResourceDescriptor(ResourceType.Data, _product, indexResult.Archive.AsHexString(), indexResult.Offset, indexResult.Length);
            }

            // Assume the file is a self-contained archive
            return new ResourceDescriptor(ResourceType.Data, _product, encodingKey.AsHexString());
        }
    }
}
