using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Shared.Extensions;
using Blizztrack.Shared.IO;

using System.Diagnostics;

using static System.MemoryExtensions;

namespace Blizztrack.Framework.TACT.Configuration
{
    public class ServerConfiguration(EncodingKey[] archives, SizeAware<EncodingKey> fileIndex, EncodingKey archiveGroup) : IResourceParser<ServerConfiguration>
    {
        public readonly EncodingKey[] Archives = archives;
        public readonly SizeAware<EncodingKey> FileIndex = fileIndex;
        public readonly EncodingKey ArchiveGroup = archiveGroup;

        public static ServerConfiguration OpenCompressedResource(ResourceHandle resourceHandle)
            => Parse(resourceHandle.ToMappedDataSource());

        public static ServerConfiguration OpenResource(ResourceHandle resourceHandle)
            => Parse(resourceHandle.ToMappedDataSource());

        public static ServerConfiguration Parse<T>(T fileData)
            where T : IDataSource
        {
            return ConfigurationFile.Parse(fileData, (properties, values, data) =>
            {
                Debug.Assert(properties.Length == values.Length);

                var archives = Array.Empty<EncodingKey>();
                var fileIndex = EncodingKey.Zero;
                var archiveGroup = EncodingKey.Zero;
                var fileIndexSize = 0L;

                for (var i = 0; i < properties.Length; ++i)
                {
                    var property = data.UnsafeIndex(properties[i]);
                    var value = data.UnsafeIndex(values[i]);

                    if (property.SequenceEqual("archives"u8))
                        archives = value.AsKeyString<EncodingKey>((byte)' ');
                    else if (property.SequenceEqual("file-index"u8))
                        fileIndex = value.AsKeyString<EncodingKey>() ?? default;
                    else if (property.SequenceEqual("file-index-size"u8))
                        fileIndexSize = long.Parse(value);
                    else if (property.SequenceEqual("archive-group"u8))
                        archiveGroup = value.AsKeyString<EncodingKey>() ?? default;
                }

                return new ServerConfiguration(archives, new (fileIndex, fileIndexSize), archiveGroup);
            });
        }
    }
}
