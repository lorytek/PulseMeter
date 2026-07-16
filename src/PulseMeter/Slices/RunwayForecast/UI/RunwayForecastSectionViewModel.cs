using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseMeter.Slices.RunwayForecast.UI;

public sealed class RunwayForecastSectionViewModel : INotifyPropertyChanged
{
    private readonly IRunwayForecastPresenter _presenter;
    private IReadOnlyList<LimitRunwayForecast> _forecasts = [];
    private string? _selectedLimitKey;

    public RunwayForecastSectionViewModel(IRunwayForecastPresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RunwayForecastDisplayRow> Rows { get; } = new();

    public bool HasRows => Rows.Count > 0;

    public string EmptyStateText => "Runway forecast will appear after live rate-limit data arrives.";

    public void ApplySignals(UsageSignalsSnapshot signals, string? selectedLimitKey, DateTimeOffset now)
    {
        _forecasts = signals.RunwayForecasts;
        _selectedLimitKey = selectedLimitKey;
        Refresh(now);
    }

    public void SelectLimit(string? selectedLimitKey, DateTimeOffset now)
    {
        if (string.Equals(_selectedLimitKey, selectedLimitKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedLimitKey = selectedLimitKey;
        Refresh(now);
    }

    public void Refresh(DateTimeOffset now)
    {
        Rows.Clear();
        foreach (var row in _presenter.BuildRows(_forecasts, _selectedLimitKey, now))
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(EmptyStateText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
