using System.Windows;

namespace PulseMeter.Slices.ExpandedHeader.UI;

public partial class ExpandedHeader : System.Windows.Controls.UserControl
{
    public ExpandedHeader()
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
