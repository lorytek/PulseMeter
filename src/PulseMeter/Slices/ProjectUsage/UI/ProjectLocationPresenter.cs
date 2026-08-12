using System.Windows;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.ProjectUsage.Business;

namespace PulseMeter.Slices.ProjectUsage.UI;

public interface IProjectLocationPresenter
{
    void ShowLocation(string projectDisplayName, string observedPath);
}

public interface IProjectLocationDialog
{
    event EventHandler? Closed;
    Window? Owner { get; set; }
    bool IsVisible { get; }
    bool Activate();
    bool? ShowDialog();
}

public sealed class ProjectLocationPresenter : IProjectLocationPresenter
{
    private readonly IProjectLocationActionService _actionService;
    private readonly IProjectFolderPicker _folderPicker;
    private readonly Func<Window?> _ownerProvider;
    private readonly Func<ProjectLocationViewModel, IProjectFolderPicker, IProjectLocationDialog> _dialogFactory;
    private readonly Action<IProjectLocationDialog, Window> _ownerAssigner;
    private readonly object _activeDialogLock = new();
    private IProjectLocationDialog? _activeDialog;

    public ProjectLocationPresenter(
        IProjectLocationActionService actionService,
        IProjectFolderPicker folderPicker,
        Func<Window?> ownerProvider,
        Func<ProjectLocationViewModel, IProjectFolderPicker, IProjectLocationDialog>? dialogFactory = null,
        Action<IProjectLocationDialog, Window>? ownerAssigner = null)
    {
        _actionService = actionService ?? throw new ArgumentNullException(nameof(actionService));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
        _dialogFactory = dialogFactory ?? ((viewModel, picker) => new ProjectLocationWindow(viewModel, picker));
        _ownerAssigner = ownerAssigner ?? ((dialog, owner) => dialog.Owner = owner);
    }

    public void ShowLocation(string projectDisplayName, string observedPath)
    {
        if (string.IsNullOrWhiteSpace(observedPath))
        {
            return;
        }

        var owner = _ownerProvider();
        if (owner is not null && !owner.Dispatcher.CheckAccess())
        {
            owner.Dispatcher.Invoke(() => ShowLocation(projectDisplayName, observedPath));
            return;
        }

        IProjectLocationDialog dialog;
        lock (_activeDialogLock)
        {
            if (_activeDialog is { } activeDialog)
            {
                if (activeDialog.IsVisible)
                {
                    activeDialog.Activate();
                }

                return;
            }

            dialog = _dialogFactory(
                new ProjectLocationViewModel(projectDisplayName, observedPath, _actionService),
                _folderPicker);
            _activeDialog = dialog;
        }

        EventHandler? closed = null;
        closed = (_, _) => ClearActiveDialog(dialog);
        var closedSubscribed = false;
        try
        {
            dialog.Closed += closed;
            closedSubscribed = true;
            if (owner is not null)
            {
                _ownerAssigner(dialog, owner);
            }

            dialog.ShowDialog();
        }
        finally
        {
            if (closedSubscribed)
            {
                dialog.Closed -= closed;
            }

            ClearActiveDialog(dialog);
        }
    }

    private void ClearActiveDialog(IProjectLocationDialog dialog)
    {
        lock (_activeDialogLock)
        {
            if (ReferenceEquals(_activeDialog, dialog))
            {
                _activeDialog = null;
            }
        }
    }
}
