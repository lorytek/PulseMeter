using System.Windows;
using System.Windows.Input;
using PulseMeter.Slices.NavigationRail.Models;

namespace PulseMeter.Slices.NavigationRail.UI;

public partial class NavigationRail
{
    public event EventHandler<NavigationSectionRequestedEventArgs>? SectionRequested;

    public NavigationRail()
    {
        InitializeComponent();
    }

    private void NavigationPanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is NavigationRailViewModel viewModel)
        {
            viewModel.ToggleNavigationPanel();
        }
    }

    private void SectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { CommandParameter: NavigationSection section }
            || DataContext is not NavigationRailViewModel viewModel)
        {
            return;
        }

        viewModel.SelectSection(section);
        SectionRequested?.Invoke(this, new NavigationSectionRequestedEventArgs(section));
    }

    private void CustomizeDashboardButton_Click(object sender, RoutedEventArgs e)
    {
        CustomizeDashboardPopup.IsOpen = true;
        Dispatcher.BeginInvoke(new Action(() => RateLimitsVisibilityCheckBox.Focus()));
    }

    private void CustomizeDashboardPopup_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CustomizeDashboardPopup.IsOpen = false;
        CustomizeDashboardButton.Focus();
        e.Handled = true;
    }

    private void ResetVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is NavigationRailViewModel viewModel)
        {
            viewModel.ApplyVisibility(new PulseMeter.Platform.Persistence.DashboardVisibilitySettings());
        }
    }
}

public sealed class NavigationSectionRequestedEventArgs : EventArgs
{
    public NavigationSectionRequestedEventArgs(NavigationSection section)
    {
        Section = section;
    }

    public NavigationSection Section { get; }
}
