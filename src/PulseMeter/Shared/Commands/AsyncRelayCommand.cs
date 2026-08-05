using System.Windows.Input;

using PulseMeter.Platform.Diagnostics;

namespace PulseMeter.Shared.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync();
        }
        catch (Exception exception)
        {
            // ICommand is a UI event boundary. Letting this escape would raise the
            // exception on the WPF dispatcher and could terminate the application.
            PrivacySafeDiagnostics.WriteFailure("command failed", exception);
        }
    }

    public Task ExecuteAsync()
    {
        return CanExecute(null) ? _execute() : Task.CompletedTask;
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
