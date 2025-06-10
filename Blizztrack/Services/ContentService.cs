using System.Net;
using System.Net.Http.Headers;

using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;

using Polly;
using Polly.Retry;

namespace Blizztrack.Services
{
    /// <summary>
    /// Provides access to data off of Blizzard's CDNs.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="clientFactory"></param>
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

    }
}
