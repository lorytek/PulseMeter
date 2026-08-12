using System.Windows;

namespace PulseMeter.Slices.SupportSnapshot.UI;

public partial class DesktopProcessSnapshotWindow : Window, IDesktopProcessSnapshotDialog
{
    private readonly DesktopProcessSnapshotViewModel _viewModel;

    public DesktopProcessSnapshotWindow(DesktopProcessSnapshotViewModel viewModel, bool measureOnOpen = false)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        if (measureOnOpen)
        {
            Loaded += (_, _) => _viewModel.BeginMeasurement();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => _viewModel.Dispose();
}
