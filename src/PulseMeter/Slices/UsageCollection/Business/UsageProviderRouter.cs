using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

/// <summary>
/// Lets the UI choose which coding assistant PulseMeter monitors.
/// </summary>
public interface IUsageProviderSwitch
{
    UsageProvider Provider { get; set; }
}

/// <summary>
/// Routes usage requests to the selected provider (Codex or Claude Code) and only
/// forwards snapshot updates that come from the active provider, so a late update from
/// the previously selected provider can never overwrite the current view.
/// </summary>
public sealed class UsageProviderRouter : IUsageService, IUsageProviderSwitch
{
    private readonly IReadOnlyDictionary<UsageProvider, IUsageService> _services;
    private volatile UsageProvider _provider;
    private bool _useMockMode;

    public UsageProviderRouter(
        IUsageService codexUsageService,
        IUsageService claudeUsageService,
        UsageProvider initialProvider = UsageProvider.Codex)
    {
        _services = new Dictionary<UsageProvider, IUsageService>
        {
            [UsageProvider.Codex] = codexUsageService,
            [UsageProvider.Claude] = claudeUsageService
        };
        _provider = _services.ContainsKey(initialProvider) ? initialProvider : UsageProvider.Codex;
        _useMockMode = codexUsageService.UseMockMode;

        foreach (var service in _services.Values)
        {
            service.SnapshotUpdated += OnInnerSnapshotUpdated;
        }
    }

    public event EventHandler<UsageSnapshot>? SnapshotUpdated;

    public UsageProvider Provider
    {
        get => _provider;
        set
        {
            if (_services.ContainsKey(value))
            {
                _provider = value;
            }
        }
    }

    public bool UseMockMode
    {
        get => _useMockMode;
        set
        {
            _useMockMode = value;
            foreach (var service in _services.Values)
            {
                service.UseMockMode = value;
            }
        }
    }

    private IUsageService Active => _services[_provider];

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_services.Values.Select(service => service.StartAsync(cancellationToken)));
    }

    public async Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var provider = _provider;
        var snapshot = await _services[provider].GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        // The user switched providers while this refresh was in flight: answer with the
        // provider they now expect instead of the one they just left.
        var current = _provider;
        return current == provider
            ? snapshot
            : await _services[current].GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private void OnInnerSnapshotUpdated(object? sender, UsageSnapshot snapshot)
    {
        if (!ReferenceEquals(sender, Active))
        {
            return;
        }

        SnapshotUpdated?.Invoke(this, snapshot);
    }
}
