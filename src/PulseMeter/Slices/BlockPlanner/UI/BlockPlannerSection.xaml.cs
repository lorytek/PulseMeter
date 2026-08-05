using WpfUserControl = System.Windows.Controls.UserControl;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Data;
using System.Windows.Threading;

namespace PulseMeter.Slices.BlockPlanner.UI;

public partial class BlockPlannerSection : WpfUserControl
{
    private string? _lastAnnouncedRecoveryConfirmation;

    internal event EventHandler? RecoveryConfirmationLiveRegionChanged;

    public BlockPlannerSection()
    {
        InitializeComponent();
    }

    private void RecoveryConfirmationTextBlock_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        var text = RecoveryConfirmationTextBlock.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _lastAnnouncedRecoveryConfirmation = null;
            return;
        }

        // The confirmation text and its visibility trigger are updated by separate bindings.
        // Announce only after the new visible layout has been applied, otherwise a busy UI
        // thread can observe the text while the live-region container is still collapsed.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var currentText = RecoveryConfirmationTextBlock.Text;
            if (!RecoveryConfirmationTextBlock.IsVisible
                || string.IsNullOrWhiteSpace(currentText)
                || string.Equals(currentText, _lastAnnouncedRecoveryConfirmation, StringComparison.Ordinal))
            {
                return;
            }

            _lastAnnouncedRecoveryConfirmation = currentText;
            RaiseRecoveryConfirmationLiveRegionChanged();
        });
    }

    private void RaiseRecoveryConfirmationLiveRegionChanged()
    {
        var peer = UIElementAutomationPeer.FromElement(RecoveryConfirmationTextBlock)
            ?? UIElementAutomationPeer.CreatePeerForElement(RecoveryConfirmationTextBlock);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        RecoveryConfirmationLiveRegionChanged?.Invoke(this, EventArgs.Empty);
    }
}
