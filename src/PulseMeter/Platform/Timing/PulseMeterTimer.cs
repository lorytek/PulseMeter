using System.Windows.Threading;

using PulseMeter.Platform.Diagnostics;

namespace PulseMeter.Platform.Timing;

public interface IPulseMeterTimer
{
    event EventHandler? Tick;

    TimeSpan Interval { get; set; }

    void Start();

    void Stop();
}

public interface IPulseMeterTimerFactory
{
    IPulseMeterTimer Create(TimeSpan interval);
}

public sealed class DispatcherPulseMeterTimerFactory : IPulseMeterTimerFactory
{
    public IPulseMeterTimer Create(TimeSpan interval)
    {
        return new DispatcherPulseMeterTimer(interval);
    }
}

internal sealed class DispatcherPulseMeterTimer : IPulseMeterTimer
{
    private readonly DispatcherTimer _timer;

    public DispatcherPulseMeterTimer(TimeSpan interval)
    {
        _timer = new DispatcherTimer
        {
            Interval = interval
        };
        _timer.Tick += OnTimerTick;
    }

    public event EventHandler? Tick;

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs eventArgs)
    {
        var handlers = Tick;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception exception)
            {
                // Keep the dispatcher and other periodic jobs alive when one timer
                // subscriber has a transient fault.
                PrivacySafeDiagnostics.WriteFailure("timer subscriber failed", exception);
            }
        }
    }
}
