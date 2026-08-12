using System.Windows;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.SupportSnapshot.Business;

namespace PulseMeter.Slices.SupportSnapshot.UI;

public interface ISupportSnapshotPresenter
{
    void ShowPreview();
}

public sealed class SupportSnapshotPresenter : ISupportSnapshotPresenter
{
    private readonly ISupportSnapshotFactsStore _factsStore;
    private readonly SupportSnapshotFormatter _formatter;
    private readonly IClipboardService _clipboardService;
    private readonly IPulseMeterWindow _owner;

    public SupportSnapshotPresenter(
        ISupportSnapshotFactsStore factsStore,
        SupportSnapshotFormatter formatter,
        IClipboardService clipboardService,
        IPulseMeterWindow owner)
    {
        _factsStore = factsStore;
        _formatter = formatter;
        _clipboardService = clipboardService;
        _owner = owner;
    }

    public void ShowPreview()
    {
        var dialog = new SupportSnapshotWindow(new SupportSnapshotViewModel(_formatter.Format(_factsStore.Capture()), _clipboardService));
        if (_owner is Window owner)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }
}
