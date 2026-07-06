using WpfPoint = System.Windows.Point;

namespace PulseMeter.Slices.PulseMeterWindow;

internal static class WindowResizeHitTester
{
    internal const int HtLeft = 10;
    internal const int HtRight = 11;
    internal const int HtTop = 12;
    internal const int HtTopLeft = 13;
    internal const int HtTopRight = 14;
    internal const int HtBottom = 15;
    internal const int HtBottomLeft = 16;
    internal const int HtBottomRight = 17;

    private const double ResizeBorderThickness = 8;

    public static int? GetResizeHitTest(WpfPoint point, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var isLeft = point.X >= 0 && point.X < ResizeBorderThickness;
        var isRight = point.X <= width && point.X > width - ResizeBorderThickness;
        var isTop = point.Y >= 0 && point.Y < ResizeBorderThickness;
        var isBottom = point.Y <= height && point.Y > height - ResizeBorderThickness;

        if (isTop && isLeft)
        {
            return HtTopLeft;
        }

        if (isTop && isRight)
        {
            return HtTopRight;
        }

        if (isBottom && isLeft)
        {
            return HtBottomLeft;
        }

        if (isBottom && isRight)
        {
            return HtBottomRight;
        }

        if (isLeft)
        {
            return HtLeft;
        }

        if (isRight)
        {
            return HtRight;
        }

        if (isTop)
        {
            return HtTop;
        }

        return isBottom ? HtBottom : null;
    }
}
