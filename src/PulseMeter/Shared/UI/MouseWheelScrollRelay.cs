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

        var parentScrollViewer = FindParentScrollViewer(element);
        if (parentScrollViewer is null)
        {
            return;
        }

        var scrollDistance = SystemParameters.WheelScrollLines == -1
            ? parentScrollViewer.ViewportHeight
            : Math.Max(1, SystemParameters.WheelScrollLines) * 16;
        var wheelSteps = Math.Max(1, Math.Abs(eventArgs.Delta) / Mouse.MouseWheelDeltaForOneLine);
        var targetOffset = parentScrollViewer.VerticalOffset - Math.Sign(eventArgs.Delta) * scrollDistance * wheelSteps;

        parentScrollViewer.ScrollToVerticalOffset(targetOffset);
        eventArgs.Handled = true;
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
