using System.Windows;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.SupportSnapshot.Business;

namespace PulseMeter.Slices.SupportSnapshot.UI;

public interface ICodexDesktopProcessSnapshotPresenter
{
    void ShowSnapshot(bool measureOnOpen = false);
}

public interface IDesktopProcessSnapshotDialog
{
    event EventHandler? Closed;
    Window? Owner { get; set; }
    bool IsVisible { get; }
    bool Activate();
    bool? ShowDialog();
}

public sealed class DesktopProcessSnapshotPresenter : ICodexDesktopProcessSnapshotPresenter
{
    private readonly ICodexDesktopProcessSnapshotService _snapshotService;
    private readonly IClipboardService _clipboardService;
    private readonly IPulseMeterWindow _owner;
    private readonly Func<DesktopProcessSnapshotViewModel, bool, IDesktopProcessSnapshotDialog> _dialogFactory;
    private readonly Action<IDesktopProcessSnapshotDialog, Window> _ownerAssigner;
    private readonly object _activeDialogLock = new();
    private IDesktopProcessSnapshotDialog? _activeDialog;

    public DesktopProcessSnapshotPresenter(
        ICodexDesktopProcessSnapshotService snapshotService,
        IClipboardService clipboardService,
        IPulseMeterWindow owner,
        Func<DesktopProcessSnapshotViewModel, bool, IDesktopProcessSnapshotDialog>? dialogFactory = null,
        Action<IDesktopProcessSnapshotDialog, Window>? ownerAssigner = null)
    {
        _snapshotService = snapshotService;
        _clipboardService = clipboardService;
        _owner = owner;
        _dialogFactory = dialogFactory ?? ((viewModel, measureOnOpen) => new DesktopProcessSnapshotWindow(viewModel, measureOnOpen));
        _ownerAssigner = ownerAssigner ?? ((dialog, ownerWindow) => dialog.Owner = ownerWindow);
    }

    public void ShowSnapshot(bool measureOnOpen = false)
    {
        if (_owner is Window ownerWindow && !ownerWindow.Dispatcher.CheckAccess())
        {
            ownerWindow.Dispatcher.Invoke(() => ShowSnapshot(measureOnOpen));
            return;
        }

        IDesktopProcessSnapshotDialog dialog;
        DesktopProcessSnapshotViewModel viewModel;
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

            viewModel = new DesktopProcessSnapshotViewModel(_snapshotService, _clipboardService);
            dialog = _dialogFactory(viewModel, measureOnOpen);
            _activeDialog = dialog;
        }

        EventHandler? closed = null;
        closed = (_, _) => ClearActiveDialog(dialog);
        var closedSubscribed = false;
        try
        {
            dialog.Closed += closed;
            closedSubscribed = true;
            if (_owner is Window owner)
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
            viewModel.Dispose();
        }
    }

    private void ClearActiveDialog(IDesktopProcessSnapshotDialog dialog)
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
