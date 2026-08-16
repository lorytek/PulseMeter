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
        OpenFolderCommand = new RelayCommand(
            _ => RunAction(
                _actionService.OpenFolder,
                "Folder-open request sent.",
                "File Explorer is unavailable on this device.",
                "Could not open the folder. Try again."),
            _ => CanRunAction);
        OpenCodexCommand = new RelayCommand(
            _ => RunAction(
                _actionService.OpenCodex,
                "Codex launch requested.",
                "Codex is unavailable on this device.",
                "Could not open Codex. Make sure Codex is installed and try again."),
            _ => CanRunAction);
        OpenWindowsPowerShellCommand = new RelayCommand(
            _ => RunAction(
                _actionService.OpenWindowsPowerShell,
                "Windows PowerShell launch requested.",
                "Windows PowerShell is unavailable on this device.",
                "Could not start Windows PowerShell. Try again."),
            _ => CanRunAction);
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
    public RelayCommand OpenCodexCommand { get; }
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

    private void RunAction(
        Func<string?, ProjectLocationActionResult> action,
        string successMessage,
        string launcherUnavailableMessage,
        string launchFailedMessage)
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
                ProjectLocationActionResult.LauncherUnavailable => launcherUnavailableMessage,
                ProjectLocationActionResult.LaunchFailed => launchFailedMessage,
                _ => launchFailedMessage
            };
        }
        catch (Exception)
        {
            Feedback = launchFailedMessage;
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
        OpenCodexCommand.RaiseCanExecuteChanged();
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
