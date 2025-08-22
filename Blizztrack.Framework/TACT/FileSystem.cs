using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;

using Pidgin;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Encoding = Blizztrack.Framework.TACT.Implementation.Encoding;

namespace Blizztrack.Framework.TACT
{
    /// <summary>
    /// A filesystem provides access to a variety of files within a single installation of a game.
    /// </summary>
    public interface IFileSystem
    {
        public string? GetCompressionSpec(in Views.EncodingKey encodingKey);

        /// <summary>
        /// Retrieves every resource descriptor that corresponds to a given file path.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>A collection of resource descriptors.</returns>
        public ResourceDescriptor[] Open(string filePath);

        /// <summary>
        /// Retrieves every resource descriptor that corresponds to a given file data ID.
        /// </summary>
        /// <param name="fileDataID">An unique identifier for the file to look for.</param>
        /// <returns>A collection of resource descriptors.</returns>
        public ResourceDescriptor[] OpenFDID(uint fileDataID);

        /// <summary>
        /// Retrieves every resource descriptor that corresponds to a given content key.
        /// </summary>
        /// <param name="contentKey">The content key to look for.</param>
        /// <returns>A collection of resource descriptors.</returns>
        public ResourceDescriptor[] OpenContentKey(in Views.ContentKey contentKey);

        /// <summary>
        /// Retrieves a resource descriptor that corresponds to a given encoding key.
        /// </summary>
        /// <param name="encodingKey">The encoding key to look for.</param>
        /// <returns>A resource descriptor.</returns>
        public ResourceDescriptor OpenEncodingKey(in Views.EncodingKey encodingKey);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor[] Open(string filePath)
        {
            if (_install is not null)
            {
                ref readonly var installEntry = ref _install.Find(filePath);
                if (!Unsafe.IsNullRef(in installEntry))
                    return OpenContentKey(installEntry.ContentKey);
            }

            return [];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor[] OpenFDID(uint fileDataID)
        {
            if (_root == default)
                return [];

            // TODO: Smell: MD5 != IContentKey<MD5>.
            ref readonly var rootResult = ref _root.FindFileDataID(fileDataID);
            if (Unsafe.IsNullRef(in rootResult))
                return [];

            return OpenContentKey(new Views.ContentKey(rootResult.ContentKey));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor[] OpenContentKey(in Views.ContentKey contentKey)
        {
            var encodingResult = _encoding == null ? default : _encoding.FindContentKey(contentKey);

            var results = new ResourceDescriptor[encodingResult.Count];
            for (var i = 0; i < encodingResult.Count; ++i)
                results[i] = OpenEncodingKey(encodingResult[i], contentKey);

            return results;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor OpenEncodingKey(in Views.EncodingKey encodingKey) => OpenEncodingKey(in encodingKey, ContentKey.Zero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceDescriptor OpenEncodingKey(in Views.EncodingKey encodingKey, in Views.ContentKey contentKey)
        {
            if (_fileIndex is not null)
            {
                // Try non-archived files
                var indexResult = _fileIndex.FindEncodingKey(in encodingKey);
                if (indexResult)
                    return ResourceType.Data.ToDescriptor(_product, indexResult.Archive, encodingKey, contentKey, indexResult.Offset, indexResult.Length);
            }

            {
                var indexResult = _archives.FindEncodingKey(in encodingKey);
                if (indexResult)
                    return ResourceType.Data.ToDescriptor(_product, indexResult.Archive, encodingKey, contentKey, indexResult.Offset, indexResult.Length);
            }

            // Assume the file is a self-contained archive
            return ResourceType.Data.ToDescriptor(_product, encodingKey);
        }

        public string? GetCompressionSpec(in Views.EncodingKey encodingKey)
        {
            if (_encoding is null)
                return default;

            var encodingSpec = _encoding.FindSpecification(encodingKey);
            if (encodingSpec == default)
                return default;

            return encodingSpec.GetSpecificationString(_encoding);
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

        public ResourceDescriptor[] Open(string filePath) => _implementation.Open(filePath);

        public ResourceDescriptor[] OpenFDID(uint fileDataID) => _implementation.OpenFDID(fileDataID);

        public ResourceDescriptor[] OpenContentKey(in Views.ContentKey contentKey)
            => _implementation.OpenContentKey(in contentKey);

        public ResourceDescriptor OpenEncodingKey(in Views.EncodingKey encodingKey)
            => _implementation.OpenEncodingKey(in encodingKey);

        public string? GetCompressionSpec(in Views.EncodingKey encodingKey)
            => _implementation.GetCompressionSpec(in encodingKey);
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
    public class FileSystem<ArchiveIndexT>(string product, ArchiveIndexT archiveIndices, Encoding? encoding = default, Root? root = default, Install? install = default, IIndex? fileIndex = default)
        : IFileSystem
        where ArchiveIndexT : IIndex
    {
        private readonly BaseFileSystem<ArchiveIndexT, IIndex> _implementation = new(product, archiveIndices, encoding, root, install, fileIndex);

        public ResourceDescriptor[] Open(string filePath) => _implementation.Open(filePath);

        public ResourceDescriptor[] OpenFDID(uint fileDataID)
            => _implementation.OpenFDID(fileDataID);

        public ResourceDescriptor[] OpenContentKey(in Views.ContentKey contentKey)
            => _implementation.OpenContentKey(in contentKey);

        public ResourceDescriptor OpenEncodingKey(in Views.EncodingKey encodingKey)
            => _implementation.OpenEncodingKey(in encodingKey);

        public string? GetCompressionSpec(in Views.EncodingKey encodingKey)
            => _implementation.GetCompressionSpec(in encodingKey);
    }
}
