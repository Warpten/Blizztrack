using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Encoding = Blizztrack.Framework.TACT.Implementation.Encoding;
using Index = Blizztrack.Framework.TACT.Implementation.Index;

namespace Blizztrack.Framework.TACT
{
    /// <summary>
    /// A filesystem provides access to a variety of files within a single installation of a game.
    /// </summary>
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

    internal readonly struct BaseFileSystem<AT, FT>(string product, AT archiveIndices, Encoding? encoding = default, Root? root = default, Install? install = default, FT? fileIndex = default)
        where AT : IIndex
        where FT : IIndex
    {
        private readonly string _product = product;
        private readonly AT _archives = archiveIndices;
        private readonly FT? _fileIndex = fileIndex;
        private readonly Encoding? _encoding = encoding;
        private readonly Root? _root = root;
        private readonly Install? _install = install;

        public ResourceDescriptor[] OpenFDID(uint fileDataID)
        {
            if (_root == default)
                return [];

            // TODO: Smell: MD5 != IContentKey<MD5>.
            ref readonly var rootResult = ref _root.FindFileDataID(fileDataID);
            if (Unsafe.IsNullRef(in rootResult))
                return [];

            return OpenContentKey(rootResult.ContentKey.AsReadOnlySpan().AsKey<ContentKeyRef>());
        }

        public ResourceDescriptor[] OpenContentKey<K>(K contentKey) where K : IContentKey<K>, allows ref struct
        {
            var encodingResult = _encoding == null ? default : _encoding.FindContentKey(contentKey);

            var results = new ResourceDescriptor[encodingResult.Count];
            for (var i = 0; i < encodingResult.Count; ++i)
                results[i] = OpenEncodingKey(encodingResult[i]);

            return results;
        }

        public ResourceDescriptor OpenEncodingKey<K>(K encodingKey) where K : IEncodingKey<K>, allows ref struct
        {
            if (_fileIndex is not null)
            {
                // Try non-archived files
                var indexResult = _fileIndex.FindEncodingKey(encodingKey);
                if (indexResult)
                    return new ResourceDescriptor(ResourceType.Data, _product, indexResult.Archive.AsHexString(), indexResult.Offset, indexResult.Length);
            }

            // Assume the file is a self-contained archive
            return new ResourceDescriptor(ResourceType.Data, _product, encodingKey.AsHexString());
        }
    }

    /// <summary>
    /// An encapsulation of the entire file system TACT describes.
    /// </summary>
    /// <param name="product">The code that identifies the product</param>
    /// <param name="archiveIndices">An object that acts as an index for all resources in this file system.</param>
    /// <param name="encoding">An optional data structure that effectively maps <see cref="IContentKey"/>s to <see cref="EncodingKey"/>s.</param>
    /// <param name="root">An optional data structure that associates unique file IDs to <see cref="IContentKey"/>s.</param>
    /// <param name="install"></param>
    /// <param name="fileIndex"></param>
    public class FileSystem(string product, IIndex archiveIndices, Encoding? encoding = default, Root? root = default, Install? install = default, IIndex? fileIndex = default)
        : IFileSystem
    {
        private readonly BaseFileSystem<IIndex, IIndex> _implementation = new(product, archiveIndices, encoding, root, install, fileIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor[] OpenFDID(uint fileDataID) => _implementation.OpenFDID(fileDataID);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor[] OpenContentKey<K>(K contentKey) where K : IContentKey<K>, allows ref struct
            => _implementation.OpenContentKey(contentKey);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor OpenEncodingKey<K>(K encodingKey) where K : IEncodingKey<K>, allows ref struct
            => _implementation.OpenEncodingKey(encodingKey);
    }

    /// <summary>
    /// An experimental, non-type erased implementation of <see cref="FileSystem"/>.
    /// 
    /// This version exists because some nutjob (me) wanted a version of file systems that <b>do not</b> virtualize method calls
    /// on varying data structures internal to the implementation of a file system.
    /// </summary>
    /// <typeparam name="ArchiveIndexT"></typeparam>
    /// <typeparam name="FileIndexT"></typeparam>
    /// <param name="product"></param>
    /// <param name="archiveIndices"></param>
    /// <param name="encoding"></param>
    /// <param name="root"></param>
    /// <param name="install"></param>
    /// <param name="fileIndex"></param>
    [Experimental("BT002")]
    public class FileSystem<ArchiveIndexT, FileIndexT>(string product, ArchiveIndexT archiveIndices, Encoding? encoding = default, Root? root = default, Install? install = default, FileIndexT? fileIndex = default)
        : IFileSystem
        where ArchiveIndexT : IIndex
        where FileIndexT : IIndex
    {
        private readonly BaseFileSystem<ArchiveIndexT, FileIndexT> _implementation = new(product, archiveIndices, encoding, root, install, fileIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor[] OpenFDID(uint fileDataID) => _implementation.OpenFDID(fileDataID);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor[] OpenContentKey<K>(K contentKey) where K : IContentKey<K>, allows ref struct
            => _implementation.OpenContentKey(contentKey);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor OpenEncodingKey<K>(K encodingKey) where K : IEncodingKey<K>, allows ref struct
            => _implementation.OpenEncodingKey(encodingKey);
    }
}
