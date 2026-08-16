using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.UsageCollection.Business;
using PulseMeter.Slices.UsageCollection.Models;

namespace PulseMeter.Tests;

[Collection(UsageTrendWpfCollection.Name)]
public sealed class TaskbarUsageIconControllerTests
{
    [Fact]
    public void Renderer_CreatesFrozenSquareBadgeForEverySupportedPercentageWidth()
    {
        foreach (var remainingPercent in new[] { 0, 7, 72, 100 })
        {
            var badge = Assert.IsType<RenderTargetBitmap>(
                TaskbarUsageBadgeRenderer.Create(remainingPercent));
            Assert.Equal(64, badge.PixelWidth);
            Assert.Equal(64, badge.PixelHeight);
            Assert.True(badge.IsFrozen);
        }
    }

    [Fact]
    public void Controller_UsesLiveWeeklyNumberAndRestoresBrandIconForStaleData()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            TaskbarUsageIconController? controller = null;
            try
            {
                var defaultIcon = CreateDefaultIcon();
                var window = new Window { Icon = defaultIcon };
                var viewModel = new PulseMeterWindowViewModel(new NoOpUsageService());
                controller = new TaskbarUsageIconController(window);
                controller.Bind(viewModel);

                Assert.Same(defaultIcon, window.Icon);
                Assert.Equal("PulseMeter — Starting", window.TaskbarItemInfo?.Description);

                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 28));
                var badge72 = window.Icon;
                Assert.NotSame(defaultIcon, badge72);
                Assert.Equal("PulseMeter — Weekly 72% left", window.TaskbarItemInfo?.Description);

                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 28.4));
                Assert.Same(badge72, window.Icon);
                Assert.Equal("PulseMeter — Weekly 72% left", window.TaskbarItemInfo?.Description);

                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 29));
                Assert.NotSame(badge72, window.Icon);
                Assert.Equal("PulseMeter — Weekly 71% left", window.TaskbarItemInfo?.Description);

                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 29, SyncStatus.Stale));
                Assert.Same(defaultIcon, window.Icon);
                Assert.Equal("PulseMeter — Stale", window.TaskbarItemInfo?.Description);

                controller.Dispose();
                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 30));
                Assert.Same(defaultIcon, window.Icon);
                Assert.Equal("PulseMeter — Stale", window.TaskbarItemInfo?.Description);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                controller?.Dispose();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TestTimeouts.UiThread), "The taskbar usage icon test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    private static ImageSource CreateDefaultIcon()
    {
        var drawing = new GeometryDrawing(
            Brushes.SteelBlue,
            null,
            new RectangleGeometry(new Rect(0, 0, 16, 16)));
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }

    private static UsageSnapshot LiveWeeklySnapshot(
        double usedPercent,
        SyncStatus syncStatus = SyncStatus.Live) =>
        new()
        {
            SyncStatus = syncStatus,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Buckets =
            [
                new RateLimitBucket
                {
                    LimitId = "codex",
                    LimitName = "General",
                    WindowDurationMins = 10_080,
                    WindowLabel = "7d",
                    UsedPercent = usedPercent
                }
            ]
        };

    private sealed class NoOpUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated { add { } remove { } }

        public bool UseMockMode { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new UsageSnapshot());
    }
}
