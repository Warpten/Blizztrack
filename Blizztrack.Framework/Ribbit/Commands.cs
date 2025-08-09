using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;

using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Blizztrack.Framework.Ribbit
{
    using PV = ProtocolVersion;

    public static class Commands
    {
        /// <summary>
        /// Asynchronously queries a Ribbit endpoint for a set of <see cref="Summary"/> objets.
        /// </summary>
        /// <param name="host">A complete hostname to query.</param>
        /// <param name="port">The port to use. Defaults to 1119</param>
        /// <param name="stoppingToken">An optional cancellation token.</param>
        /// <param name="version">The protocol version to use.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">If the given version can't be handled.</exception>
        /// <exception cref="OperationCanceledException">If a cancellation request was issued before the operation completed.</exception>
        public static async Task<Summary> GetEndpointSummary(string host, int port = 1119,
            CancellationToken stoppingToken = default, PV version = PV.BestAvailable)
            => new (version switch
            {
                PV.V1 => await Execute(host, port, "v1/summary", new MultipartCommandExecutor("summary"u8), ParseSummary, stoppingToken),
                PV.V2 or PV.BestAvailable => await Execute(host, port, "v2/summary", new SimpleCommandExecutor(), ParseSummary, stoppingToken),
                _ => throw new ArgumentOutOfRangeException(nameof(version))
            });

        /// <summary>
        /// Asynchronously queries a Ribbit endpoint for a set of <see cref="CDN"/> objets.
        /// </summary>
        /// <param name="product">THe product code to query.</param>
        /// <param name="host">A complete hostname to query.</param>
        /// <param name="port">The port to use. Defaults to 1119</param>
        /// <param name="stoppingToken">An optional cancellation token.</param>
        /// <param name="version">The protocol version to use.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">If the given version can't be handled.</exception>
        /// <exception cref="OperationCanceledException">If a cancellation request was issued before the operation completed.</exception>
        public static async Task<CDN> GetProductCDNs(string product, string host, int port,
            CancellationToken stoppingToken = default, PV version = PV.BestAvailable)
            => new (version switch
            {
                PV.V1 => await Execute(host, port, $"v1/products/{product}/cdns", new MultipartCommandExecutor("cdn"u8), ParseCDNs, stoppingToken),
                PV.V2 or PV.BestAvailable => await Execute(host, port, $"v2/products/{product}/cdns", new SimpleCommandExecutor(), ParseCDNs, stoppingToken),
                _ => throw new ArgumentOutOfRangeException(nameof(version))
            });

        /// <summary>
        /// Asynchronously queries a Ribbit endpoint for a set of <see cref="Version"/> objets.
        /// </summary>
        /// <param name="product">THe product code to query.</param>
        /// <param name="host">A complete hostname to query.</param>
        /// <param name="port">The port to use. Defaults to 1119.</param>
        /// <param name="stoppingToken">An optional cancellation token.</param>
        /// <param name="version">The protocol version to use.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">If the given version can't be handled.</exception>
        /// <exception cref="OperationCanceledException">If a cancellation request was issued before the operation completed.</exception>
        public static async Task<Version> GetProductVersions(string product, string host, int port = 1119,
            CancellationToken stoppingToken = default, PV version = PV.BestAvailable)
            => new (version switch
            {
                PV.V1 => await Execute(host, port, $"v1/products/{product}/versions", new MultipartCommandExecutor("version"u8), ParseVersions, stoppingToken),
                PV.V2 or PV.BestAvailable => await Execute(host, port, $"v2/products/{product}/versions", new SimpleCommandExecutor(), ParseVersions, stoppingToken),
                _ => throw new ArgumentOutOfRangeException(nameof(version))
            });

        /// <summary>
        /// Asynchronously queries a Ribbit endpoint for a set of <see cref="BGDL"/> objets.
        /// </summary>
        /// <param name="product">The product code to query.</param>
        /// <param name="host">A complete hostname to query.</param>
        /// <param name="port">The port to use. Defaults to 1119</param>
        /// <param name="stoppingToken">An optional cancellation token.</param>
        /// <param name="version">The protocol version to use.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">If the given version can't be handled.</exception>
        /// <exception cref="OperationCanceledException">If a cancellation request was issued before the operation completed.</exception>
        public static async Task<Version> GetProductBGDL(string product, string host, int port = 1119,
            CancellationToken stoppingToken = default, PV version = PV.BestAvailable)
            => new Version(version switch
            {
                PV.V1 => await Execute(host, port, $"v1/products/{product}/bgdl", new MultipartCommandExecutor("version"u8), ParseVersions, stoppingToken),
                PV.V2 or PV.BestAvailable => await Execute(host, port, $"v2/products/{product}/bgdl", new SimpleCommandExecutor(), ParseVersions, stoppingToken),
                _ => throw new ArgumentOutOfRangeException(nameof(version))
            });

        private static Summary.Entry ParseSummary(PSV.FieldInfo[] columns, ReadOnlySpan<byte> record)
        {
            var product = string.Empty;
            var seqn = 0;
            SequenceNumberType flags = default;

            foreach (var (columnName, _, _, valueRange) in columns)
            {
                switch (columnName)
                {
                    case "Product": product = Encoding.ASCII.GetString(record[valueRange]); break;
                    case "Seqn": seqn = int.Parse(record[valueRange]); break;
                    case "Flags": flags = Encoding.ASCII.GetString(record[valueRange]) switch
                    {
                        "cdn" => SequenceNumberType.CDN,
                        "bgdl" => SequenceNumberType.BGDL,
                        "" => SequenceNumberType.Version,
                        string value => throw new InvalidOperationException("Unknown flag value '{value}'"),
                    }; break;
                    default: throw new InvalidOperationException($"{columnName} is not a valid column name for a 'summary' PSV file.");
                }
            }

            if (product.Length == 0)
                return default;

            return new Summary.Entry(product, seqn, flags);
        }

        private static CDN.Entry ParseCDNs(PSV.FieldInfo[] columns, ReadOnlySpan<byte> record)
        {
            var name = string.Empty;
            var path = string.Empty;
            string[] hosts = [];
            string configPath = string.Empty;

            foreach (var (columnName, _, _, valueRange) in columns)
            {
                switch (columnName)
                {
                    case "Name": name = Encoding.ASCII.GetString(record[valueRange]); break;
                    case "Path": path = Encoding.ASCII.GetString(record[valueRange]); break;
                    case "Hosts": hosts = Encoding.ASCII.GetString(record[valueRange]).Split(' ', StringSplitOptions.RemoveEmptyEntries); break;
                    case "Servers": break;
                    case "ConfigPath": configPath = Encoding.ASCII.GetString(record[valueRange]); break;
                    default: throw new InvalidOperationException($"{columnName} is not a valid column name for a 'cdns' PSV file.");
                }
            }

            if (hosts.Length == 0)
                return default;

            return new CDN.Entry(name, path, hosts, configPath);
        }

        private static Version.Entry ParseVersions(PSV.FieldInfo[] columns, ReadOnlySpan<byte> record)
        {
            var region = string.Empty;
            var buildConfig = default(EncodingKey);
            var cdnConfig = default(EncodingKey);
            var keyRing = default(EncodingKey);
            var buildID = 0u;
            var versionsName = string.Empty;
            var productConfig = default(EncodingKey);

            foreach (var (columnName, _, _, valueRange) in columns)
            {
                switch (columnName)
                {
                    case "Region": region = Encoding.ASCII.GetString(record[valueRange]); break;
                    case "BuildConfig": buildConfig = record[valueRange].AsKeyString<EncodingKey>(); break;
                    case "CDNConfig": cdnConfig = record[valueRange].AsKeyString<EncodingKey>(); break;
                    case "KeyRing": keyRing = record[valueRange].AsKeyString<EncodingKey>(); break;
                    case "BuildId": buildID = uint.Parse(record[valueRange]); break;
                    case "VersionsName": versionsName = Encoding.ASCII.GetString(record[valueRange]); break;
                    case "ProductConfig": productConfig = record[valueRange].AsKeyString<EncodingKey>(); break;
                    default: throw new InvalidOperationException($"{columnName} is not a valid column name for a 'versions' PSV file.");
                }
            }

            if (buildConfig == default && cdnConfig == default)
                return default;

            return new Version.Entry(region, buildConfig, cdnConfig, keyRing, buildID, versionsName, productConfig);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Task<(int, T[])> Execute<T, Executor>(string host, int port, string command,
            Executor executor,
            PSV.Handler<T> parser,
            CancellationToken stoppingToken = default)
            where Executor : notnull, ICommandExecutor
        {
            var networkStream = OpenNetwork(host, port, command, stoppingToken: stoppingToken);
            var filteredStream = executor.Apply(networkStream, stoppingToken);
            return PSV.ParseAsync(filteredStream, parser);
        }

        private static async IAsyncEnumerable<ArraySegment<byte>> OpenNetwork(string host, int port, string command,
            int bufferSize = 1024, [EnumeratorCancellation] CancellationToken stoppingToken = default)
        {
            using var client = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(host, port, stoppingToken).ConfigureAwait(false);
            var writeCount = await client.SendAsync(Encoding.UTF8.GetBytes(command), stoppingToken).ConfigureAwait(false);
            await client.SendAsync("\n"u8.ToArray(), stoppingToken).ConfigureAwait(false);

            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            var memoryBuffer = new Memory<byte>(buffer);
            try
            {
                int writeOffset = 0;
                int bytesRead;
                while ((bytesRead = await client.ReceiveAsync(memoryBuffer[writeOffset..], stoppingToken).ConfigureAwait(false)) != 0)
                {
                    if (bytesRead + writeOffset >= bufferSize)
                    {
                        yield return buffer;
                        writeOffset = 0;
                    }
                    else
                        writeOffset += bytesRead;
                }

                yield return buffer[..writeOffset];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public record struct CDN(int SequenceNumber, CDN.Entry[] Entries)
    {
        public CDN((int, Entry[]) psv) : this(psv.Item1, psv.Item2) { }

        public record struct Entry(string Name, string Path, string[] Hosts, string ConfigPath);
    }

    public record struct Summary(int SequenceNumber, Summary.Entry[] Entries)
    {
        public Summary((int, Entry[]) psv) : this(psv.Item1, psv.Item2) { }

        public record struct Entry(string Product, int SequenceNumber, SequenceNumberType Flags);
    }

    public record struct Version(int SequenceNumber, Version.Entry[] Entries)
    {
        public Version((int, Entry[]) psv) : this(psv.Item1, psv.Item2) { }

        public record struct Entry(string Region, EncodingKey BuildConfig, EncodingKey CDNConfig, EncodingKey KeyRing, uint BuildID, string VersionsName, EncodingKey ProductConfig);
    }

    public enum SequenceNumberType : int
    {
        Version = 0,
        CDN = 1,
        BGDL = 2
    }
}
