using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Platform.Windows;
using PulseMeter.Shared.Commands;

namespace PulseMeter.Slices.SupportSnapshot.UI;

public sealed class SupportSnapshotViewModel : INotifyPropertyChanged
{
    private readonly IClipboardService _clipboardService;
    private string _copyFeedback = string.Empty;

    public SupportSnapshotViewModel(string previewText, IClipboardService clipboardService)
    {
        PreviewText = previewText;
        _clipboardService = clipboardService;
        CopyPreviewCommand = new RelayCommand(_ => CopyPreview());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PreviewText { get; }

    public string CopyFeedback
    {
        get => _copyFeedback;
        private set
        {
            if (_copyFeedback != value)
            {
                _copyFeedback = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CopyFeedback)));
            }
        }
    }

    public RelayCommand CopyPreviewCommand { get; }

    public void CopyPreview()
    {
        try
        {
            _clipboardService.SetText(PreviewText);
            CopyFeedback = "Preview copied.";
        }
        catch (Exception)
        {
            CopyFeedback = "Could not copy the preview. Try again.";
        }
    }
}
