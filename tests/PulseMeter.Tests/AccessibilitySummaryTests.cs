using PulseMeter.Slices.DailyUsage.Models;
using PulseMeter.Slices.NeedsAttention.Models;
using PulseMeter.Slices.ProjectUsage.Models;
using PulseMeter.Slices.RateLimitsDaily.Models;
using PulseMeter.Slices.UsageAttribution.Models;

namespace PulseMeter.Tests;

public sealed class AccessibilitySummaryTests
{
    [Fact]
    public void RowSummaries_UseReadableMetricsWithoutPrivatePathsOrImplementationFields()
    {
        var project = new ProjectUsageDisplayRow(
            "My project", @"C:\Users\Private\Project", "1.2B", "42%", "12", 42,
            "300M", "+50M", "#D97706", 50_000_000, 300_000_000,
            "5 active days", "2 spike days", "Top chat", "Largest moment");
        var attribution = new UsageAttributionProjectDisplayRow(
            "My project", @"C:\Users\Private\Project", "1.2B", "42%", "5 active days",
            @"My project C:\Users\Private\Project Estimated 30-day token burn: 1.2B");
        var attention = new NeedsAttentionItem(
            "RUNWAY", "Limit may be reached early", "Reduce pace to last until reset.", "#F97316");
        var daily = new DailyUsageDisplayRow("Today", "12M", "-20% vs median", true, 30, 12);
        var weekly = new DailyRateLimitDisplayRow("Today", "#1F73FF", "65%", 65, "#1F73FF", "M 56 13");

        var summaries = new[]
        {
            project.AccessibleSummary,
            attribution.AccessibleSummary,
            attention.AccessibleSummary,
            daily.AccessibleSummary,
            weekly.AccessibleSummary
        };

        Assert.All(summaries, summary =>
        {
            Assert.DoesNotContain("C:\\Users", summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FullPath", summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DisplayRow", summary, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains("42% of 30-day usage", project.AccessibleSummary);
        Assert.Contains("Estimated 1.2B tokens", attribution.AccessibleSummary);
        Assert.Equal("RUNWAY. Limit may be reached early. Reduce pace to last until reset.", attention.AccessibleSummary);
        Assert.Equal("Today. 12M tokens. -20% vs median.", daily.AccessibleSummary);
        Assert.Equal("Today. 65% remaining.", weekly.AccessibleSummary);
    }
}
