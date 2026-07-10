using PulseMeter.Platform.Persistence;

namespace PulseMeter.Slices.PulseMeterWindow.Business;

internal static class PulseMeterWindowLayoutCalculator
{
    // The compact data bar is 382px wide. Add the window surface's 26px horizontal
    // padding and 2px border so both reset labels and the fixed controls remain readable.
    public const double CompactWindowWidth = 410;
    public const double CompactWindowMinWidth = 410;
    public const double CompactWindowHeight = 66;
    public const double CompactWindowMinHeight = 66;
    public const double DefaultExpandedWindowWidth = 1024;
    public const double DefaultExpandedWindowHeight = 712;
    public const double NormalExpandedWindowWidth = 1042;
    public const double NormalExpandedWindowHeight = 712;
    public const double ExpandedWindowMinWidth = 720;
    public const double ExpandedWindowMinHeight = 460;
    public const double ExpandedWindowMaxWidth = 1_300;
    public const double ExpandedWindowMaxHeight = 900;

    private const double ExpandedLayoutMinScale = 0.72;
    private const double ExpandedLayoutMaxScale = 1.0;
    private const double LegacyCompressedReferenceHeight = 1_040;

    public static double WindowMinWidth(bool isExpanded)
    {
        return isExpanded ? ExpandedWindowMinWidth : CompactWindowMinWidth;
    }

    public static double WindowWidth(bool isExpanded, double expandedWindowWidth)
    {
        return isExpanded ? expandedWindowWidth : CompactWindowWidth;
    }

    public static double WindowMinHeight(bool isExpanded)
    {
        return isExpanded ? ExpandedWindowMinHeight : CompactWindowMinHeight;
    }

    public static double WindowHeight(bool isExpanded, double expandedWindowHeight)
    {
        return isExpanded ? expandedWindowHeight : CompactWindowHeight;
    }

    public static double SanitizeExpandedWindowWidth(double width)
    {
        return IsFinitePositive(width)
            ? Math.Clamp(width, ExpandedWindowMinWidth, ExpandedWindowMaxWidth)
            : DefaultExpandedWindowWidth;
    }

    public static double SanitizeExpandedWindowHeight(double height)
    {
        return IsFinitePositive(height)
            ? Math.Clamp(height, ExpandedWindowMinHeight, ExpandedWindowMaxHeight)
            : DefaultExpandedWindowHeight;
    }

    public static bool ShouldUpgradeLegacyReferenceHeight(PulseMeterWindowState state)
    {
        return state.IsExpanded
            && Math.Abs(state.Width - DefaultExpandedWindowWidth) < 0.5
            && Math.Abs(state.Height - LegacyCompressedReferenceHeight) < 0.5;
    }

    public static double CalculateExpandedLayoutScale(bool isExpanded, double width, double height)
    {
        if (!isExpanded || !IsFinitePositive(width) || !IsFinitePositive(height))
        {
            return 1.0;
        }

        var widthScale = width / NormalExpandedWindowWidth;
        var heightScale = height / NormalExpandedWindowHeight;
        var scale = Math.Clamp(
            Math.Min(widthScale, heightScale),
            ExpandedLayoutMinScale,
            ExpandedLayoutMaxScale);

        return Math.Round(scale, 3);
    }

    public static bool IsFinitePositive(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
