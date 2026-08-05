using System.Drawing;
using System.IO;
using System.ComponentModel;
using System.Windows.Forms;
using PulseMeter.Slices.PulseMeterWindow;

namespace PulseMeter.Platform.Windows;

public sealed class TrayIconService : ITrayIconService
{
    private readonly IPulseMeterWindow _pulseMeterWindow;
    private readonly PulseMeterWindowViewModel _viewModel;
    private readonly Action _shutdown;
    private readonly Icon _appIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _mockModeItem;
    private readonly ToolStripMenuItem _autoShowItem;
    private readonly ToolStripMenuItem _autoHideItem;
    private readonly ToolStripMenuItem _alwaysOnTopItem;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;
    private bool _disposed;

    public TrayIconService(IPulseMeterWindow pulseMeterWindow, PulseMeterWindowViewModel viewModel, Action shutdown)
    {
        _pulseMeterWindow = pulseMeterWindow;
        _viewModel = viewModel;
        _shutdown = shutdown;

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Show PulseMeter", null, (_, _) => ShowPulseMeter());
        _contextMenu.Items.Add("Hide PulseMeter", null, (_, _) => HidePulseMeter());
        _contextMenu.Items.Add("Refresh", null, (_, _) => Refresh());
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
            if (!string.IsNullOrEmpty(e.PropertyName)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.UseMockMode)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.AutoShowWhenCodexFocused)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.AutoHideWhenFocusLeaves)
                && e.PropertyName != nameof(PulseMeterWindowViewModel.IsAlwaysOnTop))
            {
                return;
            }

            _pulseMeterWindow.Invoke(() => SyncMenuCheckmarks(e.PropertyName));
        };
        _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;
        _contextMenu.Items.Add(_alwaysOnTopItem);

        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Exit", null, (_, _) => Exit());

        _appIcon = LoadAppIcon();
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = _appIcon,
            Text = "PulseMeter",
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => ShowPulseMeter(expand: true);
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
        _appIcon.Dispose();
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

    private void Refresh()
    {
        _pulseMeterWindow.Invoke(() => _ = _viewModel.RefreshAsync());
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
