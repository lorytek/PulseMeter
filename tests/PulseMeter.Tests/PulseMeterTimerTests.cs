using System.Reflection;
using System.Runtime.CompilerServices;
using PulseMeter.Platform.Timing;

namespace PulseMeter.Tests;

public sealed class PulseMeterTimerTests
{
    [Fact]
    public void TimerTick_ContainsSubscriberFailureAndContinuesToHealthySubscribers()
    {
        var timerType = typeof(DispatcherPulseMeterTimerFactory).Assembly.GetType(
            "PulseMeter.Platform.Timing.DispatcherPulseMeterTimer");
        Assert.NotNull(timerType);
        var timer = Assert.IsAssignableFrom<IPulseMeterTimer>(
            RuntimeHelpers.GetUninitializedObject(timerType));
        var healthySubscriberCalls = 0;
        timer.Tick += (_, _) => throw new InvalidOperationException("Periodic job failed.");
        timer.Tick += (_, _) => healthySubscriberCalls++;
        var onTimerTick = timerType.GetMethod(
            "OnTimerTick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onTimerTick);

        var exception = Record.Exception(() => onTimerTick.Invoke(timer, [null, EventArgs.Empty]));

        Assert.Null(exception);
        Assert.Equal(1, healthySubscriberCalls);
    }
}
