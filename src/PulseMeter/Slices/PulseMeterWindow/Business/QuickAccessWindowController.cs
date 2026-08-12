using System.Windows;
using PulseMeter.Slices.PulseMeterWindow.UI;

namespace PulseMeter.Slices.PulseMeterWindow.Business;

public interface IQuickAccessWindowController
{
    void ToggleQuickAccess();
}

/// <summary>Applies the single user-initiated visibility policy shared by tray and hotkey access.</summary>
public sealed class QuickAccessWindowController : IQuickAccessWindowController
{
    private readonly IPulseMeterWindow _pulseMeterWindow;
    private readonly PulseMeterWindowViewModel _viewModel;
    private bool _isHandling;

    public QuickAccessWindowController(IPulseMeterWindow pulseMeterWindow, PulseMeterWindowViewModel viewModel)
    {
        _pulseMeterWindow = pulseMeterWindow;
        _viewModel = viewModel;
    }

    public void ToggleQuickAccess()
    {
        if (_isHandling)
        {
            return;
        }

        _isHandling = true;
        try
        {
            if (!_pulseMeterWindow.IsVisible || _pulseMeterWindow.WindowState == WindowState.Minimized)
            {
                _viewModel.MarkShownByUser();
                _pulseMeterWindow.ShowAndActivate();
                return;
            }

            if (!_pulseMeterWindow.IsActive)
            {
                _pulseMeterWindow.Activate();
                return;
            }

            _viewModel.MarkHiddenByUser();
            _pulseMeterWindow.Hide();
        }
        finally
        {
            _isHandling = false;
        }
    }
}
