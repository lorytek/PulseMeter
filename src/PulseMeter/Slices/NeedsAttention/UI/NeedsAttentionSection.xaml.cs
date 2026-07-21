using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Data;
using System.Windows.Threading;
using PulseMeter.Slices.NeedsAttention.Models;
using TextBlock = System.Windows.Controls.TextBlock;

namespace PulseMeter.Slices.NeedsAttention.UI;

public partial class NeedsAttentionSection
{
    private bool _restoreAttentionToggleFocusAfterCollapse;

    public event EventHandler<NeedsAttentionReviewRequestedEventArgs>? ReviewRequested;

    public NeedsAttentionSection()
    {
        InitializeComponent();
    }

    private void CollapseAttentionItemsButton_Click(object sender, RoutedEventArgs e)
    {
        _restoreAttentionToggleFocusAfterCollapse = true;
    }

    private void CollapseAttentionItemsButton_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_restoreAttentionToggleFocusAfterCollapse || CollapseAttentionItemsButton.IsVisible)
        {
            return;
        }

        _restoreAttentionToggleFocusAfterCollapse = false;
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() =>
            {
                ToggleAttentionItemsButton.Focus();
                System.Windows.Input.Keyboard.Focus(ToggleAttentionItemsButton);
            }));
    }

    private void ReviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NeedsAttentionItem { ReviewTarget: NeedsAttentionReviewTarget target } })
        {
            return;
        }

        ReviewRequested?.Invoke(this, new NeedsAttentionReviewRequestedEventArgs(target));
    }

    private void CopyFeedbackText_OnTargetUpdated(object sender, DataTransferEventArgs e)
    {
        if (sender is not TextBlock { IsVisible: true, Text.Length: > 0 } feedbackText)
        {
            return;
        }

        feedbackText.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            () =>
            {
                var peer = UIElementAutomationPeer.FromElement(feedbackText)
                    ?? new TextBlockAutomationPeer(feedbackText);
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            });
    }
}

public sealed class NeedsAttentionReviewRequestedEventArgs(NeedsAttentionReviewTarget target) : EventArgs
{
    public NeedsAttentionReviewTarget Target { get; } = target;
}
