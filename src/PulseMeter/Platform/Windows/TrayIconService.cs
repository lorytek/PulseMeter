using System.Drawing;
using System.IO;
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

    public TrayIconService(IPulseMeterWindow pulseMeterWindow, PulseMeterWindowViewModel viewModel, Action shutdown)
    {
        _pulseMeterWindow = pulseMeterWindow;
        _viewModel = viewModel;
        _shutdown = shutdown;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show PulseMeter", null, (_, _) => ShowPulseMeter());
        menu.Items.Add("Hide PulseMeter", null, (_, _) => HidePulseMeter());
        menu.Items.Add("Refresh", null, (_, _) => Refresh());
        menu.Items.Add(new ToolStripSeparator());

        var mockModeItem = new ToolStripMenuItem("Mock Mode")
        {
            Checked = _viewModel.UseMockMode,
            CheckOnClick = true
        };
        mockModeItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() => _viewModel.UseMockMode = mockModeItem.Checked);
        };
        menu.Items.Add(mockModeItem);

        var autoShowItem = new ToolStripMenuItem("Auto-show when monitored app focused")
        {
            Checked = _viewModel.AutoShowWhenCodexFocused,
            CheckOnClick = true
        };
        autoShowItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() => _viewModel.AutoShowWhenCodexFocused = autoShowItem.Checked);
        };
        menu.Items.Add(autoShowItem);

        var autoHideItem = new ToolStripMenuItem("Auto-hide when focus leaves")
        {
            Checked = _viewModel.AutoHideWhenFocusLeaves,
            CheckOnClick = true
        };
        autoHideItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() => _viewModel.AutoHideWhenFocusLeaves = autoHideItem.Checked);
        };
        menu.Items.Add(autoHideItem);

        var alwaysOnTopItem = new ToolStripMenuItem("Always on top")
        {
            Checked = _viewModel.IsAlwaysOnTop,
            CheckOnClick = true
        };
        alwaysOnTopItem.CheckedChanged += (_, _) =>
        {
            _pulseMeterWindow.Invoke(() => _viewModel.IsAlwaysOnTop = alwaysOnTopItem.Checked);
        };
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PulseMeterWindowViewModel.IsAlwaysOnTop))
            {
                _pulseMeterWindow.Invoke(() => alwaysOnTopItem.Checked = _viewModel.IsAlwaysOnTop);
            }
        };
        menu.Items.Add(alwaysOnTopItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _appIcon = LoadAppIcon();
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _appIcon,
            Text = "PulseMeter",
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => ShowPulseMeter(expand: true);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _appIcon.Dispose();
    }

    private static Icon LoadAppIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("/Assets/PulseMeter.ico", UriKind.Relative));
        if (resource is null)
        {
            return LoadFallbackIcon();
        }

        using var stream = resource.Stream;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
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

            if (!_pulseMeterWindow.IsVisible)
            {
                _pulseMeterWindow.Show();
            }

            if (_pulseMeterWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _pulseMeterWindow.WindowState = System.Windows.WindowState.Normal;
            }

            if (expand && !_viewModel.IsExpanded)
            {
                _viewModel.ToggleExpanded();
            }

            _pulseMeterWindow.Activate();
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
        _pulseMeterWindow.Invoke(_shutdown);
    }
}
