using System.Windows;
using System.Windows.Input;
using PulseMeter.Slices.NavigationRail.Models;

namespace PulseMeter.Slices.NavigationRail.UI;

public partial class NavigationRail
{
    private bool _restoreCustomizeFocusAfterClose;

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
    }

    private void CustomizeDashboardPopup_Opened(object? sender, EventArgs e)
    {
        FocusPopupControl(RateLimitsVisibilityCheckBox);
    }

    private void CustomizeDashboardPopup_Closed(object? sender, EventArgs e)
    {
        if (!_restoreCustomizeFocusAfterClose)
        {
            return;
        }

        _restoreCustomizeFocusAfterClose = false;
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            new Action(() => CustomizeDashboardButton.Focus()));
    }

    private void VisibilityCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox checkBox)
        {
            return;
        }

        FocusPopupControl(checkBox);
    }

    private void CustomizeDashboardPopup_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        _restoreCustomizeFocusAfterClose = true;
        CustomizeDashboardPopup.IsOpen = false;
        e.Handled = true;
    }

    private void FocusPopupControl(System.Windows.Controls.Control control)
    {
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (!CustomizeDashboardPopup.IsOpen)
                {
                    return;
                }

                control.Focus();
                Keyboard.Focus(control);
            }));
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
