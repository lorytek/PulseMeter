using PulseMeter.Platform.Windows;

namespace PulseMeter.Tests;

public sealed class PulseMeterSingleInstanceCoordinatorTests
{
    [Fact]
    public void SecondaryLaunch_SignalsThePrimaryInstance()
    {
        var scopeName = $"Local\\PulseMeter.Tests.{Guid.NewGuid():N}";
        using var primary = PulseMeterSingleInstanceCoordinator.Acquire(scopeName);
        using var activationReceived = new ManualResetEventSlim();
        primary.StartListening(activationReceived.Set);
        Exception? secondaryFailure = null;

        var secondaryThread = new Thread(() =>
        {
            try
            {
                using var secondary = PulseMeterSingleInstanceCoordinator.Acquire(scopeName);
                Assert.False(secondary.IsPrimary);
                secondary.SignalPrimary();
            }
            catch (Exception exception)
            {
                secondaryFailure = exception;
            }
        });

        secondaryThread.Start();

        Assert.True(secondaryThread.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(secondaryFailure);
        Assert.True(activationReceived.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Dispose_ReleasesOwnershipForTheNextLaunch()
    {
        var scopeName = $"Local\\PulseMeter.Tests.{Guid.NewGuid():N}";
        using (var first = PulseMeterSingleInstanceCoordinator.Acquire(scopeName))
        {
            Assert.True(first.IsPrimary);
        }

        using var next = PulseMeterSingleInstanceCoordinator.Acquire(scopeName);
        Assert.True(next.IsPrimary);
    }

    [Fact]
    public void Acquire_RecoversOwnershipFromAnAbandonedPreviousOwner()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var scopeName = $"Local\\PulseMeter.Tests.{Guid.NewGuid():N}";
            using var abandonedOwner = new Mutex(initiallyOwned: false, scopeName + ".Owner");
            var acquired = false;
            var ownerThread = new Thread(() => acquired = abandonedOwner.WaitOne());

            ownerThread.Start();

            Assert.True(ownerThread.Join(TimeSpan.FromSeconds(5)));
            Assert.True(acquired);

            using var recovered = PulseMeterSingleInstanceCoordinator.Acquire(scopeName);

            Assert.True(recovered.IsPrimary);
        }
    }

    [Fact]
    public void Acquire_RemainsCompatibleWithARunningLegacySemaphoreOwner()
    {
        var scopeName = $"Local\\PulseMeter.Tests.{Guid.NewGuid():N}";
        using var legacyOwner = new Semaphore(1, 1, scopeName + ".Owner");
        Assert.True(legacyOwner.WaitOne(0));

        using var coordinator = PulseMeterSingleInstanceCoordinator.Acquire(scopeName);

        Assert.False(coordinator.IsPrimary);
        legacyOwner.Release();
    }
}
