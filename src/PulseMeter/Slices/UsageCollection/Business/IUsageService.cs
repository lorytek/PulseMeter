using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

public interface IUsageService
{
    event EventHandler<UsageSnapshot>? SnapshotUpdated;

    bool UseMockMode { get; set; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
