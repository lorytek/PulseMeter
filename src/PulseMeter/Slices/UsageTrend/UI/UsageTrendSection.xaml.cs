using System.Windows;
using System.Windows.Input;
using PulseMeter.Slices.NavigationRail.Models;
using PulseMeter.Slices.NavigationRail.UI;

namespace PulseMeter.Slices.UsageTrend.UI;

public partial class UsageTrendSection
{
    public UsageTrendSection()
    {
        InitializeComponent();
        MomentumPreviewToolTip.PlacementTarget = MomentumPreviewInfo;
    }

    public event EventHandler<NavigationSectionRequestedEventArgs>? SectionRequested;

    private void RunwayForecastTab_Click(object sender, RoutedEventArgs e)
    {
        SectionRequested?.Invoke(this, new NavigationSectionRequestedEventArgs(NavigationSection.RunwayForecast));
    }

    private void MomentumPreviewInfo_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        MomentumPreviewToolTip.PlacementTarget = (UIElement)sender;
        MomentumPreviewToolTip.IsOpen = true;
    }

    private void MomentumPreviewInfo_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        MomentumPreviewToolTip.IsOpen = false;
    }

    private void MomentumPreviewInfo_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        MomentumPreviewToolTip.IsOpen = false;
        e.Handled = true;
    }

}
