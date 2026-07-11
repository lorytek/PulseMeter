using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace PulseMeter.Slices.PulseMeterWindow.Business;

internal static class PulseMeterWindowPlacementCalculator
{
    public static Size FitSize(double width, double height, Rect workArea, double edgePadding)
    {
        var padding = NormalizePadding(edgePadding);
        var maxWidth = Math.Max(1, workArea.Width - padding * 2);
        var maxHeight = Math.Max(1, workArea.Height - padding * 2);

        return new Size(
            Math.Clamp(NormalizeDimension(width), 1, maxWidth),
            Math.Clamp(NormalizeDimension(height), 1, maxHeight));
    }

    public static Rect Clamp(double left, double top, double width, double height, Rect workArea, double edgePadding)
    {
        var padding = NormalizePadding(edgePadding);
        var size = FitSize(width, height, workArea, padding);

        return new Rect(
            ClampCoordinate(left, workArea.Left, workArea.Right - padding - size.Width),
            ClampCoordinate(top, workArea.Top, workArea.Bottom - padding - size.Height),
            size.Width,
            size.Height);
    }

    private static double ClampCoordinate(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Clamp(IsFinite(value) ? value : min, min, max);
    }

    private static double NormalizeDimension(double value)
    {
        return IsFinite(value) && value > 0 ? value : 1;
    }

    private static double NormalizePadding(double value)
    {
        return IsFinite(value) && value > 0 ? value : 0;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
