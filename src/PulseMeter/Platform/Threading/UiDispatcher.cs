namespace PulseMeter.Platform.Threading;

public interface IUiDispatcher
{
    void Invoke(Action action);

    Task InvokeAsync(Func<Task> action)
    {
        Task? invokedTask = null;
        Invoke(() => invokedTask = action());
        return invokedTask ?? Task.CompletedTask;
    }
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
