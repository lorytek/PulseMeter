using PulseMeter.Shared.UI;
using System.Runtime.ExceptionServices;

namespace PulseMeter.Tests;

public sealed class MouseWheelScrollRelayTests
{
    [Fact]
    public void TryRelayMouseWheelToParent_MovesTheRealParentAndLeavesNoOpEventsAvailable()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            System.Windows.Window? window = null;
            try
            {
                var relayTarget = new System.Windows.Controls.Border
                {
                    Width = 240,
                    Height = 1_000
                };
                var scrollViewer = new System.Windows.Controls.ScrollViewer
                {
                    Width = 260,
                    Height = 200,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden,
                    Content = relayTarget
                };
                window = new System.Windows.Window
                {
                    Content = scrollViewer,
                    Width = 300,
                    Height = 240,
                    Left = -20_000,
                    Top = -20_000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };
                window.Show();
                window.UpdateLayout();
                scrollViewer.ScrollToVerticalOffset(100);
                scrollViewer.UpdateLayout();

                Assert.True(MouseWheelScrollRelay.TryRelayMouseWheelToParent(relayTarget, 30, 3));
                scrollViewer.UpdateLayout();
                Assert.Equal(88, scrollViewer.VerticalOffset, precision: 8);

                scrollViewer.ScrollToTop();
                scrollViewer.UpdateLayout();
                Assert.False(MouseWheelScrollRelay.TryRelayMouseWheelToParent(relayTarget, 120, 3));
                Assert.Equal(0, scrollViewer.VerticalOffset);

                scrollViewer.ScrollToVerticalOffset(100);
                scrollViewer.UpdateLayout();
                Assert.False(MouseWheelScrollRelay.TryRelayMouseWheelToParent(relayTarget, -120, 0));
                Assert.Equal(100, scrollViewer.VerticalOffset, precision: 8);

                Assert.False(MouseWheelScrollRelay.TryRelayMouseWheelToParent(
                    new System.Windows.Controls.Border(),
                    -120,
                    3));
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The mouse-wheel relay test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Theory]
    [InlineData(0, 480, 0)]
    [InlineData(-1, 480, 480)]
    [InlineData(1, 480, 16)]
    [InlineData(3, 480, 48)]
    public void CalculateScrollDistance_RespectsTheWindowsWheelPreference(
        int wheelScrollLines,
        double viewportHeight,
        double expected)
    {
        Assert.Equal(
            expected,
            MouseWheelScrollRelay.CalculateScrollDistance(wheelScrollLines, viewportHeight));
    }

    [Fact]
    public void CalculateScrollDistance_RejectsAnInvalidPageHeight()
    {
        Assert.Equal(0, MouseWheelScrollRelay.CalculateScrollDistance(-1, double.NaN));
        Assert.Equal(0, MouseWheelScrollRelay.CalculateScrollDistance(-1, double.PositiveInfinity));
        Assert.Equal(0, MouseWheelScrollRelay.CalculateScrollDistance(-1, -10));
    }

    [Theory]
    [InlineData(100, 500, 120, 48, 52)]
    [InlineData(100, 500, -120, 48, 148)]
    [InlineData(100, 500, 30, 48, 88)]
    [InlineData(0, 500, 120, 48, 0)]
    [InlineData(500, 500, -120, 48, 500)]
    [InlineData(100, 500, 0, 48, 100)]
    [InlineData(100, 500, 120, 0, 100)]
    public void CalculateTargetOffset_ScalesPrecisionInputAndClampsAtBoundaries(
        double currentOffset,
        double scrollableHeight,
        int wheelDelta,
        double scrollDistance,
        double expected)
    {
        Assert.Equal(
            expected,
            MouseWheelScrollRelay.CalculateTargetOffset(
                currentOffset,
                scrollableHeight,
                wheelDelta,
                scrollDistance),
            precision: 8);
    }

    [Fact]
    public void CalculateTargetOffset_NormalizesInvalidScrollViewerMeasurements()
    {
        Assert.Equal(
            0,
            MouseWheelScrollRelay.CalculateTargetOffset(
                double.NaN,
                double.PositiveInfinity,
                120,
                48));
    }
}
