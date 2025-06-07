using Blizztrack.Framework.TACT.Services;

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
