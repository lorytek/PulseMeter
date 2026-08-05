using PulseMeter.Shared.Commands;

namespace PulseMeter.Tests;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task Execute_ContainsAsynchronousFailuresAtTheUiEventBoundary()
    {
        var originalContext = SynchronizationContext.Current;
        var context = new ExceptionRecordingSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            var delegateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var command = new AsyncRelayCommand(async () =>
            {
                await Task.Yield();
                delegateStarted.TrySetResult();
                throw new InvalidOperationException("Command failed.");
            });

            command.Execute(parameter: null);

            await delegateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(25);

            Assert.Empty(context.UnhandledExceptions);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PreservesFailuresForAwaitingCallers()
    {
        var expected = new InvalidOperationException("Command failed.");
        var command = new AsyncRelayCommand(() => Task.FromException(expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());

        Assert.Same(expected, actual);
    }

    private sealed class ExceptionRecordingSynchronizationContext : SynchronizationContext
    {
        private readonly object _gate = new();
        private readonly List<Exception> _unhandledExceptions = [];

        public IReadOnlyList<Exception> UnhandledExceptions
        {
            get
            {
                lock (_gate)
                {
                    return _unhandledExceptions.ToArray();
                }
            }
        }

        public override void Post(SendOrPostCallback callback, object? state)
        {
            try
            {
                callback(state);
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    _unhandledExceptions.Add(exception);
                }
            }
        }
    }
}
