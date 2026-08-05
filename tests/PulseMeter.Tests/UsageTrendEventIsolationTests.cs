using System.Reflection;
using PulseMeter.Slices.UsageTrend.Business;
using PulseMeter.Slices.UsageTrend.UI;

namespace PulseMeter.Tests;

public sealed class UsageTrendEventIsolationTests
{
    [Fact]
    public void RecoveryWatchChanged_ContainsFailureAndContinuesToHealthySubscribers()
    {
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
        var healthySubscriberCalls = 0;
        viewModel.RecoveryWatchesChanged += (_, _) => throw new InvalidOperationException("Persistence failed.");
        viewModel.RecoveryWatchesChanged += (_, _) => healthySubscriberCalls++;

        var exception = InvokePrivate(viewModel, "PublishRecoveryWatchesChanged");

        Assert.Null(exception);
        Assert.Equal(1, healthySubscriberCalls);
    }

    [Fact]
    public void RecoveryWatchCompleted_ContainsFailureAndContinuesToHealthySubscribers()
    {
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
        var healthySubscriberCalls = 0;
        viewModel.RecoveryWatchCompleted += (_, _) => throw new InvalidOperationException("Notification failed.");
        viewModel.RecoveryWatchCompleted += (_, _) => healthySubscriberCalls++;

        var exception = InvokePrivate(viewModel, "PublishRecoveryWatchCompleted", "Ready", "A block now fits.");

        Assert.Null(exception);
        Assert.Equal(1, healthySubscriberCalls);
    }

    private static Exception? InvokePrivate(object target, string methodName, params object?[] arguments)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Record.Exception(() => method.Invoke(target, arguments));
    }
}
