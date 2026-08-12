using System.Windows;

namespace PulseMeter.Slices.SupportSnapshot.UI;

public partial class SupportSnapshotWindow : Window
{
    public SupportSnapshotWindow(SupportSnapshotViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
