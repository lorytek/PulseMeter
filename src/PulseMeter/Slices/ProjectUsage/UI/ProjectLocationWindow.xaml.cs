using System.Windows;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.ProjectUsage.Business;

namespace PulseMeter.Slices.ProjectUsage.UI;

public partial class ProjectLocationWindow : Window, IProjectLocationDialog
{
    private readonly ProjectLocationViewModel _viewModel;
    private readonly IProjectFolderPicker _folderPicker;

    public ProjectLocationWindow(ProjectLocationViewModel viewModel, IProjectFolderPicker folderPicker)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        ChooseFolder();
    }

    internal void ChooseFolder()
    {
        try
        {
            _viewModel.ApplyBrowsedFolder(_folderPicker.PickFolder(this));
        }
        catch (Exception)
        {
            _viewModel.ReportFolderPickerUnavailable();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
