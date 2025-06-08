
namespace Blizztrack.Options
{
    /// <summary>
    /// Root node of all application-specific settings in <pre>appsettings.json</pre>.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// A set of product codes that should be monitored.
        /// </summary>
        public required string[] Products { get; init; }
        public required CacheSettings Cache { get; init; }
        public required RibbitSettings Ribbit { get; init; }
        public required DatabaseConnectionOptions Backend { get; init; }
    }

    public class CacheSettings
    {
        /// <summary>
        /// The path of the cache directory on disk.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// A set of extraneous CDNs to use when querying files.
        /// </summary>
        public required string[] CDNs { get; init; }

        public required ExpirySettings Expirations { get; init; }
    }

    public class ExpirySettings
    {
        public TimeSpan Encoding { get; init; } = TimeSpan.FromMinutes(10);
        public TimeSpan Install { get; init; } = TimeSpan.FromMinutes(10);
    }

    public class RibbitSettings
    {
        public required Endpoint Endpoint { get; init; }
        public required TimeSpan Interval { get; init; }
    }

    public class Endpoint
    {
        public required string Host { get; init; }
        public required int Port { get; init; }
    }

    public class DatabaseConnectionOptions
    {
        public required string Host { get; init; }
        public required int Port { get; init; } = 5432;
        public required string Database { get; init; }
        public required string User { get; init; }
        public required string Password { get; init; }

        public override string ToString() => $"Host={Host}; Port={Port}; Database={Database}; User Id={User}; Password={Password}";
    }
}
