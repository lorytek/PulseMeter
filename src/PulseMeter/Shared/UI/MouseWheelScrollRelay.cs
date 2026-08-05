using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PulseMeter.Shared.UI;

public static class MouseWheelScrollRelay
{
    public static readonly DependencyProperty RelayToParentProperty = DependencyProperty.RegisterAttached(
        "RelayToParent",
        typeof(bool),
        typeof(MouseWheelScrollRelay),
        new PropertyMetadata(false, OnRelayToParentChanged));

    public static bool GetRelayToParent(DependencyObject element)
    {
        return (bool)element.GetValue(RelayToParentProperty);
    }

    public static void SetRelayToParent(DependencyObject element, bool value)
    {
        element.SetValue(RelayToParentProperty, value);
    }

    private static void OnRelayToParentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not UIElement element)
        {
            return;
        }

        if ((bool)eventArgs.OldValue)
        {
            element.PreviewMouseWheel -= RelayMouseWheelToParent;
        }

        if ((bool)eventArgs.NewValue)
        {
            element.PreviewMouseWheel += RelayMouseWheelToParent;
        }
    }

    private static void RelayMouseWheelToParent(object sender, MouseWheelEventArgs eventArgs)
    {
        if (sender is not UIElement element || eventArgs.Delta == 0)
        {
            return;
        }

        eventArgs.Handled = TryRelayMouseWheelToParent(
            element,
            eventArgs.Delta,
            SystemParameters.WheelScrollLines);
    }

    internal static bool TryRelayMouseWheelToParent(
        UIElement element,
        int wheelDelta,
        int wheelScrollLines)
    {
        if (wheelDelta == 0)
        {
            return false;
        }

        var parentScrollViewer = FindParentScrollViewer(element);
        if (parentScrollViewer is null)
        {
            return false;
        }

        var scrollDistance = CalculateScrollDistance(
            wheelScrollLines,
            parentScrollViewer.ViewportHeight);
        var targetOffset = CalculateTargetOffset(
            parentScrollViewer.VerticalOffset,
            parentScrollViewer.ScrollableHeight,
            wheelDelta,
            scrollDistance);

        if (Math.Abs(targetOffset - parentScrollViewer.VerticalOffset) < 0.01)
        {
            return false;
        }

        parentScrollViewer.ScrollToVerticalOffset(targetOffset);
        return true;
    }

    internal static double CalculateScrollDistance(int wheelScrollLines, double viewportHeight)
    {
        if (wheelScrollLines == 0)
        {
            return 0;
        }

        if (wheelScrollLines < 0)
        {
            return double.IsFinite(viewportHeight) ? Math.Max(0, viewportHeight) : 0;
        }

        return wheelScrollLines * 16d;
    }

    internal static double CalculateTargetOffset(
        double currentOffset,
        double scrollableHeight,
        int wheelDelta,
        double scrollDistance)
    {
        var maximumOffset = double.IsFinite(scrollableHeight)
            ? Math.Max(0, scrollableHeight)
            : 0;
        var normalizedCurrentOffset = double.IsFinite(currentOffset)
            ? Math.Clamp(currentOffset, 0, maximumOffset)
            : 0;

        if (wheelDelta == 0 || !double.IsFinite(scrollDistance) || scrollDistance <= 0)
        {
            return normalizedCurrentOffset;
        }

        var wheelSteps = Math.Abs((double)wheelDelta) / Mouse.MouseWheelDeltaForOneLine;
        var requestedOffset = normalizedCurrentOffset
            - Math.Sign(wheelDelta) * scrollDistance * wheelSteps;
        return Math.Clamp(requestedOffset, 0, maximumOffset);
    }

    private static ScrollViewer? FindParentScrollViewer(DependencyObject element)
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
        }

        return null;
    }
}
