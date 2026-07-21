using System.Windows;
using PulseMeter.Slices.NavigationRail.Models;
using PulseMeter.Slices.NavigationRail.UI;

namespace PulseMeter.Slices.UsageTrend.UI;

public partial class UsageTrendSection
{
    public UsageTrendSection()
    {
        InitializeComponent();
    }

    public event EventHandler<NavigationSectionRequestedEventArgs>? SectionRequested;

    private void RunwayForecastTab_Click(object sender, RoutedEventArgs e)
    {
        SectionRequested?.Invoke(this, new NavigationSectionRequestedEventArgs(NavigationSection.RunwayForecast));
    }

}
