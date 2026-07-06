using System.Windows;

namespace PulseMeter.Slices.DailyUsage.UI;

public partial class DailyUsageSection : System.Windows.Controls.UserControl
{
    public DailyUsageSection()
    {
        InitializeComponent();
    }

    private void DailyUsageExpandCollapseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DailyUsageSectionViewModel viewModel)
        {
            viewModel.ToggleDailyUsageExpanded();
        }
    }
}
