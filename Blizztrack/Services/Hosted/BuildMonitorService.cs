
namespace Blizztrack.Services.Hosted
{
    public class BuildMonitorService(MediatorService mediator) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var (product, versions) in mediator.Products.OnVersions.Reader.ReadAllAsync(stoppingToken))
            {
                // Start by acquiring the files that match this version.
            }
        }
    }
}
