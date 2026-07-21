using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Data;
using System.Windows.Threading;
using TextBlock = System.Windows.Controls.TextBlock;

namespace PulseMeter.Slices.RateLimits.UI;

public partial class RateLimitsSection : System.Windows.Controls.UserControl
{
    private readonly StatusMessageAnnouncementTracker _statusAnnouncementTracker = new();

    public RateLimitsSection()
    {
        InitializeComponent();
    }

    private void StatusMessage_OnTargetUpdated(object sender, DataTransferEventArgs e)
    {
        if (sender is not TextBlock statusMessage)
        {
            return;
        }

        statusMessage.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            () => RaiseStatusMessageLiveRegionChanged(statusMessage));
    }

    private void RaiseStatusMessageLiveRegionChanged(TextBlock statusMessage)
    {
        if (!_statusAnnouncementTracker.ShouldAnnounce(statusMessage.IsVisible, statusMessage.Text))
        {
            return;
        }

        var peer = UIElementAutomationPeer.FromElement(statusMessage)
            ?? new TextBlockAutomationPeer(statusMessage);
        peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}

internal sealed class StatusMessageAnnouncementTracker
{
    private string? _lastAnnouncedMessage;

    public bool ShouldAnnounce(bool isVisible, string? message)
    {
        if (!isVisible || string.IsNullOrWhiteSpace(message))
        {
            _lastAnnouncedMessage = null;
            return false;
        }

        if (string.Equals(_lastAnnouncedMessage, message, StringComparison.Ordinal))
        {
            return false;
        }

        _lastAnnouncedMessage = message;
        return true;
    }
}
