using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Shared.IO;

using System.Diagnostics;

using static System.MemoryExtensions;

namespace Blizztrack.Framework.TACT.Configuration
{
    public class BuildConfiguration : IResourceParser<BuildConfiguration>
    {
        public required string BuildName { get; init; }

        /// <summary>
        /// The content key of the root file. Look it up in an instance of <see cref="Implementation.Encoding" />.
        /// </summary>
        public required ContentKey Root { get; init; }

        /// <summary>
        /// Key pair for the encoding file itself.
        /// </summary>
        public required SizedKeyPair<ContentKey, EncodingKey> Encoding { get; init; }

        /// <summary>
        /// Key pair for the install file. If the encoding key is <see cref="EncodingKey.Zero" />, look up
        /// the <see cref="ContentKey" /> in an instance of <see cref="Implementation.Encoding" />.
        /// </summary>
        public required SizedKeyPair<ContentKey, EncodingKey> Install { get; init; }

        public static BuildConfiguration OpenCompressedResource(ResourceHandle resourceHandle)
            => Parse(resourceHandle.ToMappedDataSource());

        public static BuildConfiguration OpenResource(ResourceHandle resourceHandle)
            => Parse(resourceHandle.ToMappedDataSource());

        public static BuildConfiguration Parse<T>(T fileData)
            where T : IDataSource
        {
            return ConfigurationFile.Parse(fileData, (properties, values, data) =>
            {
                Debug.Assert(properties.Length == values.Length);

                var rootHash = ContentKey.Zero;
                var encodingHashes = (C: default(ContentKey), E: default(EncodingKey));
                long[] encodingSizes = [0L, 0L];
                var installHashes = (C: default(ContentKey), E: default(EncodingKey));
                long[] installSizes = [0L, 0L];
                var buildName = string.Empty;

                for (var i = 0; i < properties.Length; ++i)
                {
                    var property = data[properties[i]];
                    var value = data[values[i]];

                    if (property.SequenceEqual("root"u8))
                        rootHash = value.AsKeyString<ContentKey>();
                    else if (property.SequenceEqual("encoding"u8))
                        encodingHashes = value.AsKeyStringPair<ContentKey, EncodingKey>();
                    else if (property.SequenceEqual("encoding-size"u8))
                    {
                        var j = 0;
                        foreach (var encodingItem in value.Split((byte)' '))
                            encodingSizes[j++] = long.Parse(value[encodingItem]);
                    }
                    else if (property.SequenceEqual("install"u8))
                        installHashes = value.AsKeyStringPair<ContentKey, EncodingKey>();
                    else if (property.SequenceEqual("install-size"u8))
                    {
                        var j = 0;
                        foreach (var installItem in value.Split((byte)' '))
                            installSizes[j++] = long.Parse(value[installItem]);
                    }
                    else if (property.SequenceEqual("build-name"u8))
                        buildName = System.Text.Encoding.ASCII.GetString(value);
                }

                Debug.Assert(encodingHashes.C != ContentKey.Zero && encodingHashes.E != EncodingKey.Zero);

                return new BuildConfiguration()
                {
                    BuildName = buildName,
                    Root = rootHash,
                    Encoding = new(new(encodingHashes.C, encodingSizes[0]), new(encodingHashes.E, encodingSizes[1])),
                    Install = new(new(installHashes.C, installSizes[0]), new(installHashes.E, installSizes[1])),
                };
            });
        }
    }
}
