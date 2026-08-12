using System.Drawing;
using System.IO;
using System.ComponentModel;
using System.Windows.Forms;
using PulseMeter.Slices.PulseMeterWindow.Business;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Slices.SupportSnapshot.UI;

namespace PulseMeter.Platform.Windows;

public sealed class TrayIconService : ITrayIconService
{
    private readonly IPulseMeterWindow _pulseMeterWindow;
    private readonly PulseMeterWindowViewModel _viewModel;
    private readonly Action _shutdown;
    private readonly Icon _appIcon;
    private readonly TrayConfidenceIconCache _confidenceIcons;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _mockModeItem;
    private readonly ToolStripMenuItem _autoShowItem;
    private readonly ToolStripMenuItem _autoHideItem;
    private readonly ToolStripMenuItem _alwaysOnTopItem;
    private readonly ToolStripMenuItem _quickAccessHotkeyItem;
    private readonly IQuickAccessWindowController _quickAccessWindowController;
    private readonly ISupportSnapshotPresenter? _supportSnapshotPresenter;
    private readonly ICodexDesktopProcessSnapshotPresenter? _desktopProcessSnapshotPresenter;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;
    private readonly TrayConfidenceTransitionTracker _confidenceTransitions = new();
    private bool _disposed;

    public TrayIconService(
        IPulseMeterWindow pulseMeterWindow,
        PulseMeterWindowViewModel viewModel,
        Action shutdown,
        IQuickAccessWindowController? quickAccessWindowController = null,
        ISupportSnapshotPresenter? supportSnapshotPresenter = null,
        Func<Icon, TrayConfidenceState, Icon>? confidenceIconFactory = null,
        ICodexDesktopProcessSnapshotPresenter? desktopProcessSnapshotPresenter = null)
    {
        _pulseMeterWindow = pulseMeterWindow;
        _viewModel = viewModel;
        _shutdown = shutdown;
        _quickAccessWindowController = quickAccessWindowController ?? new QuickAccessWindowController(pulseMeterWindow, viewModel);
        _supportSnapshotPresenter = supportSnapshotPresenter;
        _desktopProcessSnapshotPresenter = desktopProcessSnapshotPresenter;

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Show PulseMeter", null, (_, _) => ShowPulseMeter());
        _contextMenu.Items.Add("Hide PulseMeter", null, (_, _) => HidePulseMeter());
        _contextMenu.Items.Add("Quick access PulseMeter", null, (_, _) => QuickAccessPulseMeter());
        _contextMenu.Items.Add("Refresh", null, (_, _) => Refresh());
        _contextMenu.Items.Add("Support snapshot…", null, (_, _) => ShowSupportSnapshot());
        _contextMenu.Items.Add("Desktop process snapshot…", null, (_, _) => ShowDesktopProcessSnapshot());
        _contextMenu.Items.Add(new ToolStripSeparator());

        _mockModeItem = new ToolStripMenuItem("Mock Mode")
        {
            Checked = _viewModel.UseMockMode,
            CheckOnClick = true
        };
        _mockModeItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() =>
            {
                if (_viewModel.UseMockMode != _mockModeItem.Checked)
                {
                    _viewModel.UseMockMode = _mockModeItem.Checked;
                }
            });
        };
        _contextMenu.Items.Add(_mockModeItem);

        _autoShowItem = new ToolStripMenuItem("Auto-show when monitored app focused")
        {
            Checked = _viewModel.AutoShowWhenCodexFocused,
            CheckOnClick = true
        };
        _autoShowItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() =>
            {
                if (_viewModel.AutoShowWhenCodexFocused != _autoShowItem.Checked)
                {
                    _viewModel.AutoShowWhenCodexFocused = _autoShowItem.Checked;
                }
            });
        };
        _contextMenu.Items.Add(_autoShowItem);

        _autoHideItem = new ToolStripMenuItem("Auto-hide when focus leaves")
        {
            Checked = _viewModel.AutoHideWhenFocusLeaves,
            CheckOnClick = true
        };
        _autoHideItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() =>
            {
                if (_viewModel.AutoHideWhenFocusLeaves != _autoHideItem.Checked)
                {
                    _viewModel.AutoHideWhenFocusLeaves = _autoHideItem.Checked;
                }
            });
        };
        _contextMenu.Items.Add(_autoHideItem);

        _alwaysOnTopItem = new ToolStripMenuItem("Always on top")
        {
            Checked = _viewModel.IsAlwaysOnTop,
            CheckOnClick = true
        };
        _alwaysOnTopItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() =>
            {
                if (_viewModel.IsAlwaysOnTop != _alwaysOnTopItem.Checked)
                {
                    _viewModel.IsAlwaysOnTop = _alwaysOnTopItem.Checked;
                }
            });
        };
        _viewModelPropertyChangedHandler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(PulseMeterWindowViewModel.TrayConfidenceState))
            {
                _pulseMeterWindow.Invoke(UpdateConfidenceBeacon);
            }

            if (!string.IsNullOrEmpty(e.PropertyName)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.UseMockMode)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.AutoShowWhenCodexFocused)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.AutoHideWhenFocusLeaves)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.IsAlwaysOnTop)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.IsQuickAccessHotkeyRequested))
            {
                return;
            }

            _pulseMeterWindow.Invoke(() => SyncMenuCheckmarks(e.PropertyName));
        };
        _contextMenu.Items.Add(_alwaysOnTopItem);

        _quickAccessHotkeyItem = new ToolStripMenuItem("System-wide quick access shortcut (Ctrl+Alt+Shift+P)")
        {
            Checked = _viewModel.IsQuickAccessHotkeyRequested,
            CheckOnClick = true
        };
        _quickAccessHotkeyItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() =>
            {
                if (_viewModel.IsQuickAccessHotkeyRequested != _quickAccessHotkeyItem.Checked)
                {
                    _viewModel.IsQuickAccessHotkeyRequested = _quickAccessHotkeyItem.Checked;
                }
            });
        };
        _contextMenu.Items.Add(_quickAccessHotkeyItem);

        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Exit", null, (_, _) => Exit());

        Icon? appIcon = null;
        TrayConfidenceIconCache? confidenceIcons = null;
        NotifyIcon? notifyIcon = null;
        var propertyChangedSubscribed = false;
        try
        {
            appIcon = LoadAppIcon();
            confidenceIcons = new TrayConfidenceIconCache(appIcon, confidenceIconFactory);
            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = _contextMenu;
            notifyIcon.Icon = appIcon;
            notifyIcon.Text = TrayConfidenceBeacon.Tooltip(TrayConfidenceState.Starting);

            _appIcon = appIcon;
            _confidenceIcons = confidenceIcons;
            _notifyIcon = notifyIcon;
            _notifyIcon.DoubleClick += (_, _) => ShowPulseMeter(expand: true);
            _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;
            propertyChangedSubscribed = true;
            UpdateConfidenceBeacon();
            _notifyIcon.Visible = true;
        }
        catch
        {
            if (propertyChangedSubscribed)
            {
                _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            }
            notifyIcon?.Dispose();
            confidenceIcons?.Dispose();
            appIcon?.Dispose();
            _contextMenu.Dispose();
            throw;
        }
    }

    private void SyncMenuCheckmarks(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)
            || propertyName == nameof(PulseMeterWindowViewModel.UseMockMode))
        {
            SetChecked(_mockModeItem, _viewModel.UseMockMode);
        }

        if (string.IsNullOrEmpty(propertyName)
            || propertyName == nameof(PulseMeterWindowViewModel.AutoShowWhenCodexFocused))
        {
            SetChecked(_autoShowItem, _viewModel.AutoShowWhenCodexFocused);
        }

        if (string.IsNullOrEmpty(propertyName)
            || propertyName == nameof(PulseMeterWindowViewModel.AutoHideWhenFocusLeaves))
        {
            SetChecked(_autoHideItem, _viewModel.AutoHideWhenFocusLeaves);
        }

        if (string.IsNullOrEmpty(propertyName)
            || propertyName == nameof(PulseMeterWindowViewModel.IsAlwaysOnTop))
        {
            SetChecked(_alwaysOnTopItem, _viewModel.IsAlwaysOnTop);
        }

        if (string.IsNullOrEmpty(propertyName)
            || propertyName == nameof(PulseMeterWindowViewModel.IsQuickAccessHotkeyRequested))
        {
            SetChecked(_quickAccessHotkeyItem, _viewModel.IsQuickAccessHotkeyRequested);
        }
    }

    private static void SetChecked(ToolStripMenuItem item, bool isChecked)
    {
        if (item.Checked != isChecked)
        {
            item.Checked = isChecked;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _confidenceIcons.Dispose();
        _appIcon.Dispose();
    }

    private void UpdateConfidenceBeacon()
    {
        if (_disposed)
        {
            return;
        }

        var state = _viewModel.TrayConfidenceState;
        if (!_confidenceTransitions.ShouldApply(state))
        {
            return;
        }
        _notifyIcon.Text = TrayConfidenceBeacon.Tooltip(state);
        try
        {
            _notifyIcon.Icon = _confidenceIcons.Get(state);
        }
        catch (Exception)
        {
            // Keep the last-good/base icon. The fixed tooltip is still the accessible state signal.
        }
        _confidenceTransitions.MarkApplied(state);
    }

    public void ShowNotification(string title, string message)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            timeout: 5_000,
            tipTitle: title,
            tipText: message,
            tipIcon: ToolTipIcon.Info);
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("/PulseMeter;component/Assets/PulseMeter.ico", UriKind.Relative));
            if (resource is null)
            {
                return LoadFallbackIcon();
            }

            using var stream = resource.Stream;
            using var icon = new Icon(stream);
            return (Icon)icon.Clone();
        }
        catch (IOException)
        {
            return LoadFallbackIcon();
        }
    }

    private static Icon LoadFallbackIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            var extracted = Icon.ExtractAssociatedIcon(processPath);
            if (extracted is not null)
            {
                return extracted;
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private void ShowPulseMeter(bool expand = false)
    {
        _pulseMeterWindow.Invoke(() =>
        {
            _viewModel.MarkShownByUser();

            if (expand && !_viewModel.IsExpanded)
            {
                _viewModel.ToggleExpanded();
            }

            _pulseMeterWindow.ShowAndActivate();
        });
    }

    private void HidePulseMeter()
    {
        _pulseMeterWindow.Invoke(() =>
        {
            _viewModel.MarkHiddenByUser();
            _pulseMeterWindow.Hide();
        });
    }

    private void QuickAccessPulseMeter()
    {
        _pulseMeterWindow.Invoke(_quickAccessWindowController.ToggleQuickAccess);
    }

    private void Refresh()
    {
        _pulseMeterWindow.Invoke(() => _ = _viewModel.RefreshAsync());
    }

    private void ShowSupportSnapshot()
    {
        _pulseMeterWindow.Invoke(() => _supportSnapshotPresenter?.ShowPreview());
    }

    private void ShowDesktopProcessSnapshot()
    {
        _pulseMeterWindow.Invoke(() => _desktopProcessSnapshotPresenter?.ShowSnapshot());
    }

    private void Exit()
    {
        Dispose();
        _pulseMeterWindow.Invoke(() =>
        {
            _pulseMeterWindow.CloseForShutdown();
            _shutdown();
        });
    }
}
