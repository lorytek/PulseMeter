using System.Windows;
using System.Windows.Controls;

namespace PulseMeter.Slices.UsageAttribution.UI;

public partial class UsageAttributionSection : System.Windows.Controls.UserControl
{
    private const double BurnAnalysisStackedLayoutThreshold = 900;
    private static readonly GridLength SideBySideChatsWidth = new(6, GridUnitType.Star);
    private static readonly GridLength SideBySideGapWidth = new(28);
    private static readonly GridLength SideBySideEventsWidth = new(5, GridUnitType.Star);
    private static readonly GridLength StackedGapHeight = new(18);

    private bool _isBurnAnalysisStacked;

    public UsageAttributionSection()
    {
        InitializeComponent();
    }

    private void BurnAnalysisTablesGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyBurnAnalysisLayout(e.NewSize.Width);
    }

    private void ApplyBurnAnalysisLayout(double availableWidth)
    {
        var shouldStack = availableWidth > 0 && availableWidth < BurnAnalysisStackedLayoutThreshold;
        if (shouldStack == _isBurnAnalysisStacked)
        {
            return;
        }

        _isBurnAnalysisStacked = shouldStack;
        if (shouldStack)
        {
            BurnAnalysisChatsColumn.Width = new GridLength(1, GridUnitType.Star);
            BurnAnalysisChatsColumn.MinWidth = 0;
            BurnAnalysisGapColumn.Width = new GridLength(0);
            BurnAnalysisEventsColumn.Width = new GridLength(0);
            BurnAnalysisEventsColumn.MinWidth = 0;
            BurnAnalysisStackedGapRow.Height = StackedGapHeight;
            Grid.SetRow(BurnAnalysisEventsPanel, 2);
            Grid.SetColumn(BurnAnalysisEventsPanel, 0);
            Grid.SetColumnSpan(BurnAnalysisEventsPanel, 3);
            return;
        }

        BurnAnalysisChatsColumn.Width = SideBySideChatsWidth;
        BurnAnalysisChatsColumn.MinWidth = 440;
        BurnAnalysisGapColumn.Width = SideBySideGapWidth;
        BurnAnalysisEventsColumn.Width = SideBySideEventsWidth;
        BurnAnalysisEventsColumn.MinWidth = 400;
        BurnAnalysisStackedGapRow.Height = new GridLength(0);
        Grid.SetRow(BurnAnalysisEventsPanel, 0);
        Grid.SetColumn(BurnAnalysisEventsPanel, 2);
        Grid.SetColumnSpan(BurnAnalysisEventsPanel, 1);
    }
}
