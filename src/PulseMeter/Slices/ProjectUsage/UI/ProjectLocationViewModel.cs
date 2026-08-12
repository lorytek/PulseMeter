using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Shared.Commands;
using PulseMeter.Slices.ProjectUsage.Business;

namespace PulseMeter.Slices.ProjectUsage.UI;

public sealed class ProjectLocationViewModel : INotifyPropertyChanged
{
    private readonly IProjectLocationActionService _actionService;
    private readonly string _observedPath;
    private string _candidatePath;
    private bool _hasWindowOnlyChoice;
    private bool _isRunningAction;
    private string _feedback = string.Empty;

    public ProjectLocationViewModel(string projectDisplayName, string observedPath, IProjectLocationActionService actionService)
    {
        ProjectDisplayName = projectDisplayName ?? string.Empty;
        _observedPath = observedPath ?? string.Empty;
        _candidatePath = _observedPath;
        _actionService = actionService ?? throw new ArgumentNullException(nameof(actionService));
        OpenFolderCommand = new RelayCommand(_ => RunAction(_actionService.OpenFolder, "Folder-open request sent."), _ => CanRunAction);
        OpenWindowsPowerShellCommand = new RelayCommand(
            _ => RunAction(_actionService.OpenWindowsPowerShell, "Windows PowerShell launch requested."), _ => CanRunAction);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProjectDisplayName { get; }
    public string ObservedPath => _observedPath;
    public string CandidatePath => _candidatePath;
    public bool HasWindowOnlyChoice => _hasWindowOnlyChoice;
    public string ChoiceStatusText => _hasWindowOnlyChoice ? "Chosen for this window only — not saved" : string.Empty;

    public string Feedback
    {
        get => _feedback;
        private set => SetProperty(ref _feedback, value);
    }

    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenWindowsPowerShellCommand { get; }

    public void ApplyBrowsedFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        _candidatePath = folder;
        _hasWindowOnlyChoice = true;
        Feedback = string.Empty;
        OnPropertyChanged(nameof(CandidatePath));
        OnPropertyChanged(nameof(HasWindowOnlyChoice));
        OnPropertyChanged(nameof(ChoiceStatusText));
        OnActionStateChanged();
    }

    public void ReportFolderPickerUnavailable()
    {
        Feedback = "The folder picker could not open. Try again.";
    }

    private bool CanRunAction => !_isRunningAction && !string.IsNullOrWhiteSpace(_candidatePath);

    private void RunAction(Func<string?, ProjectLocationActionResult> action, string successMessage)
    {
        if (!CanRunAction)
        {
            return;
        }

        _isRunningAction = true;
        OnActionStateChanged();
        try
        {
            var result = action(_candidatePath);
            Feedback = result switch
            {
                ProjectLocationActionResult.Succeeded => successMessage,
                ProjectLocationActionResult.InvalidLocation => "This folder is unavailable. Choose another folder or close this dialog.",
                ProjectLocationActionResult.LauncherUnavailable => "Windows PowerShell is unavailable on this device.",
                ProjectLocationActionResult.LaunchFailed => "Could not start the requested Windows action. Try again.",
                _ => "Could not start the requested Windows action. Try again."
            };
        }
        catch (Exception)
        {
            Feedback = "Could not start the requested Windows action. Try again.";
        }
        finally
        {
            _isRunningAction = false;
            OnActionStateChanged();
        }
    }

    private void OnActionStateChanged()
    {
        OpenFolderCommand.RaiseCanExecuteChanged();
        OpenWindowsPowerShellCommand.RaiseCanExecuteChanged();
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
