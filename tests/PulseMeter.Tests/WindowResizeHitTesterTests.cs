using PulseMeter.Slices.PulseMeterWindow;
using WpfPoint = System.Windows.Point;

namespace PulseMeter.Tests;

public sealed class WindowResizeHitTesterTests
{
    [Theory]
    [InlineData(0, 0, WindowResizeHitTester.HtTopLeft)]
    [InlineData(7.999, 7.999, WindowResizeHitTester.HtTopLeft)]
    [InlineData(92, 0, WindowResizeHitTester.HtTopRight)]
    [InlineData(100, 0, WindowResizeHitTester.HtTopRight)]
    [InlineData(0, 92, WindowResizeHitTester.HtBottomLeft)]
    [InlineData(92, 92, WindowResizeHitTester.HtBottomRight)]
    [InlineData(100, 100, WindowResizeHitTester.HtBottomRight)]
    [InlineData(0, 50, WindowResizeHitTester.HtLeft)]
    [InlineData(92, 50, WindowResizeHitTester.HtRight)]
    [InlineData(50, 0, WindowResizeHitTester.HtTop)]
    [InlineData(50, 92, WindowResizeHitTester.HtBottom)]
    public void GetResizeHitTest_ReturnsTheExpectedEightPixelEdge(
        double x,
        double y,
        int expected)
    {
        Assert.Equal(
            expected,
            WindowResizeHitTester.GetResizeHitTest(new WpfPoint(x, y), width: 100, height: 100));
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(50, 50)]
    [InlineData(-0.001, 4)]
    [InlineData(4, -0.001)]
    [InlineData(100.001, 4)]
    [InlineData(4, 100.001)]
    [InlineData(double.NaN, 4)]
    [InlineData(4, double.NaN)]
    public void GetResizeHitTest_RejectsInteriorAndOutOfBoundsPoints(double x, double y)
    {
        Assert.Null(WindowResizeHitTester.GetResizeHitTest(new WpfPoint(x, y), width: 100, height: 100));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    [InlineData(double.NaN, 100)]
    [InlineData(100, double.PositiveInfinity)]
    public void GetResizeHitTest_RejectsInvalidWindowDimensions(double width, double height)
    {
        Assert.Null(WindowResizeHitTester.GetResizeHitTest(new WpfPoint(0, 0), width, height));
    }
}
