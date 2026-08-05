using System.Windows;
using System.Windows.Input;

namespace PulseMeter.Slices.ExpandedHeader.UI;

public partial class ExpandedHeader : System.Windows.Controls.UserControl
{
    public ExpandedHeader()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? ToggleExpandedRequested;

    public event RoutedEventHandler? HideRequested;

    public bool FocusExpandCollapseButton()
    {
        if (!ExpandCollapseButton.IsVisible || !ExpandCollapseButton.IsEnabled)
        {
            return false;
        }

        _ = ExpandCollapseButton.Focus();
        return ReferenceEquals(Keyboard.Focus(ExpandCollapseButton), ExpandCollapseButton);
    }

    private void ExpandCollapseButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleExpandedRequested?.Invoke(this, e);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, e);
    }
}
