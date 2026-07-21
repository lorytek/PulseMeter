using PulseMeter.Slices.RateLimits.UI;

namespace PulseMeter.Tests;

public sealed class StatusMessageAnnouncementTrackerTests
{
    [Fact]
    public void ShouldAnnounce_SuppressesRepeatedTextButAnnouncesChanges()
    {
        var tracker = new StatusMessageAnnouncementTracker();

        Assert.True(tracker.ShouldAnnounce(true, "Using cached usage."));
        Assert.False(tracker.ShouldAnnounce(true, "Using cached usage."));
        Assert.True(tracker.ShouldAnnounce(true, "The live source is unavailable."));
    }

    [Theory]
    [InlineData(false, "Using cached usage.")]
    [InlineData(true, "")]
    [InlineData(true, "   ")]
    [InlineData(true, null)]
    public void ShouldAnnounce_HiddenOrBlankStateResetsDuplicateSuppression(bool isVisible, string? resetMessage)
    {
        var tracker = new StatusMessageAnnouncementTracker();
        Assert.True(tracker.ShouldAnnounce(true, "Using cached usage."));

        Assert.False(tracker.ShouldAnnounce(isVisible, resetMessage));

        Assert.True(tracker.ShouldAnnounce(true, "Using cached usage."));
    }
}
