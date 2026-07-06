using System.Windows;

namespace PulseMeter.Slices.NavigationRail.UI;

public partial class NavigationRail
{
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
}
