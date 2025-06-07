namespace Blizztrack.Extensions
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddBlizzardClient<T>(this IServiceCollection services,
            TimeSpan requestTimeout, TimeSpan connectionLifetime)
            where T : class
        {
            services.AddHttpClient<T>().ConfigureBlizzardClient(requestTimeout, connectionLifetime);

            return services;
        }

        public static IHttpClientBuilder ConfigureBlizzardClient(this IHttpClientBuilder builder,
            TimeSpan requestTimeout, TimeSpan connectionLifetime)
        {
            return builder.ConfigureHttpClient(x => x.Timeout = requestTimeout)
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new SocketsHttpHandler
                    {
                        PooledConnectionLifetime = connectionLifetime,
                        MaxConnectionsPerServer = 10
                    };
                })
                .SetHandlerLifetime(connectionLifetime);
        }
    }
}
