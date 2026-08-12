using Microsoft.Win32;
using System.Windows;

namespace PulseMeter.Platform.Windows;

/// <summary>Lets the UI ask Windows for a folder without retaining the selection.</summary>
public interface IProjectFolderPicker
{
    string? PickFolder(Window owner);
}

internal interface IWindowsProjectFolderDialog
{
    string FolderName { get; }

    bool? ShowDialog(Window owner);
}

internal interface IWindowsProjectFolderDialogFactory
{
    IWindowsProjectFolderDialog Create();
}

public sealed class WindowsProjectFolderPicker : IProjectFolderPicker
{
    private readonly IWindowsProjectFolderDialogFactory _dialogFactory;

    public WindowsProjectFolderPicker()
        : this(new WindowsProjectFolderDialogFactory())
    {
    }

    internal WindowsProjectFolderPicker(IWindowsProjectFolderDialogFactory dialogFactory)
    {
        _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
    }

    public string? PickFolder(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dialog = _dialogFactory.Create();
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }

    private sealed class WindowsProjectFolderDialogFactory : IWindowsProjectFolderDialogFactory
    {
        public IWindowsProjectFolderDialog Create() => new WindowsProjectFolderDialog();
    }

    private sealed class WindowsProjectFolderDialog : IWindowsProjectFolderDialog
    {
        private readonly OpenFolderDialog _dialog = new()
        {
            Multiselect = false,
            Title = "Choose project folder"
        };

        public string FolderName => _dialog.FolderName;

        public bool? ShowDialog(Window owner) => _dialog.ShowDialog(owner);
    }
}
