using System.Globalization;
using System.Windows.Data;
using PulseMeter.Slices.NavigationRail.Models;

namespace PulseMeter.Slices.NavigationRail.UI;

public sealed class NavigationSectionSelectedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is NavigationSection selectedSection &&
               parameter is NavigationSection candidateSection &&
               selectedSection == candidateSection
            ? "Current"
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
