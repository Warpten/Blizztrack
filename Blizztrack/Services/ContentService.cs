using System.Net;
using System.Net.Http.Headers;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;
using Blizztrack.Persistence;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Blizztrack.Services
{
    file class TransferContext
    {
        public required IAsyncEnumerator<PatchEndpoint> Endpoints { get; init; }
        public required HttpClient Client { get; init; }
    
        public required RangeHeaderValue? Range { get; init; }
    }
    
    public readonly record struct ContentQueryResult(HttpStatusCode StatusCode, Stream Body);
    
    public class ContentService(IServiceProvider serviceProvider, IHttpClientFactory clientFactory)
    {
        private readonly MediatorService _mediatorService = serviceProvider.GetRequiredService<MediatorService>();
        private readonly IHttpClientFactory _clientFactory = clientFactory;
    
        private readonly ResiliencePipeline<ContentQueryResult> _acquisitionPipeline = new ResiliencePipelineBuilder<ContentQueryResult>()
            .AddConcurrencyLimiter(permitLimit: 20, queueLimit: 10)
            .AddRetry(new RetryStrategyOptions<ContentQueryResult>()
            {
                BackoffType = DelayBackoffType.Constant,
                MaxDelay = TimeSpan.Zero,
                MaxRetryAttempts = int.MaxValue,
                ShouldHandle = static args => args.Outcome switch
                {
                    { Exception: not null } => PredicateResult.True(),
                    { Result.StatusCode: HttpStatusCode.NotFound } => PredicateResult.False(),
                    _ => PredicateResult.False()
                }
            })
            .Build();

        public async ValueTask<ContentQueryResult> Query(IAsyncEnumerable<PatchEndpoint> hosts, ResourceDescriptor descriptor, CancellationToken stoppingToken)
        {
            var transferContext = new TransferContext()
            {
                Client = _clientFactory.CreateClient(),
                Range = descriptor.Offset != 0 ? new RangeHeaderValue(descriptor.Offset, descriptor.Offset + descriptor.Length) : default,
                Endpoints = hosts.GetAsyncEnumerator(stoppingToken),
            };

            var resilienceContext = ResilienceContextPool.Shared.Get(stoppingToken);
            var result = await _acquisitionPipeline.ExecuteOutcomeAsync(async (context, state) =>
            {
                if (!await state.Endpoints.MoveNextAsync())
                    return Outcome.FromResult(new ContentQueryResult(HttpStatusCode.NotFound, Stream.Null));

                var server = state.Endpoints.Current;
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"http://{server.Host}/{server.DataStem}/{descriptor.RemotePath}")
                {
                    Headers = { Range = state.Range }
                };

                var response = await state.Client.SendAsync(requestMessage, stoppingToken);
                response.EnsureSuccessStatusCode();

                var dataStream = await response.Content.ReadAsStreamAsync();
                var transferInformation = new ContentQueryResult(response.StatusCode, dataStream);

                return Outcome.FromResult(transferInformation);
            }, resilienceContext, transferContext);

            return result.Result;
        }
    
        public ValueTask<ContentQueryResult> Query(string region, ResourceDescriptor descriptor, CancellationToken stoppingSource)
        {
            using var scope = serviceProvider.CreateScope();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var eligibleHosts = databaseContext.Endpoints
                .Select(e => new PatchEndpoint(e.Host, e.DataPath, e.ConfigurationPath))
                .ToAsyncEnumerable();

            return Query(eligibleHosts, descriptor, stoppingSource);
        }
    }
}
