using System.Windows;

namespace PulseMeter.Slices.DataBar.UI;

public partial class DataBar : System.Windows.Controls.UserControl
{
    public DataBar()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? ToggleExpandedRequested;

    public event RoutedEventHandler? HideRequested;

    private void ExpandCollapseButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleExpandedRequested?.Invoke(this, e);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, e);
    }
}
