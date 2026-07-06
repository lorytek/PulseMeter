using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.RateLimits.Business;

public interface IRateLimitsPresenter
{
    IReadOnlyList<RateLimitOption> BuildOptions(IEnumerable<RateLimitBucket> buckets);

    RateLimitOption? SelectOption(IReadOnlyList<RateLimitOption> options, string? selectedKey);

    IReadOnlyList<RateLimitBucket> SelectBuckets(IEnumerable<RateLimitBucket> buckets, RateLimitOption? selectedOption);

    IReadOnlyList<QuotaDisplayRow> BuildQuotaRows(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now);

    IReadOnlyList<QuotaDisplayRow> BuildCompactRows(IEnumerable<QuotaDisplayRow> selectedRows);

    string BuildCompactTitle(
        IEnumerable<RateLimitBucket> selectedBuckets,
        IEnumerable<RateLimitBucket> allBuckets,
        RateLimitOption? selectedOption);

    string BuildCompactQuotaSummary(IEnumerable<QuotaDisplayRow> compactRows);

    string BuildExpandedQuotaSummary(IEnumerable<QuotaDisplayRow> compactRows);
}

public sealed class RateLimitsPresenter : IRateLimitsPresenter
{
    public IReadOnlyList<RateLimitOption> BuildOptions(IEnumerable<RateLimitBucket> buckets)
    {
        return buckets
            .GroupBy(QuotaDisplayBuilder.LimitKey)
            .Select(group => new RateLimitOption(group.Key, QuotaDisplayBuilder.SanitizeDisplayLabel(group.First().GroupLabel)))
            .OrderBy(QuotaDisplayBuilder.LimitOptionSortPriority)
            .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public RateLimitOption? SelectOption(IReadOnlyList<RateLimitOption> options, string? selectedKey)
    {
        return options.FirstOrDefault(option => option.Key == selectedKey)
            ?? options.FirstOrDefault(option =>
                option.Key.Equals("codex", StringComparison.OrdinalIgnoreCase)
                || option.DisplayName.Equals("General", StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();
    }

    public IReadOnlyList<RateLimitBucket> SelectBuckets(IEnumerable<RateLimitBucket> buckets, RateLimitOption? selectedOption)
    {
        var selectedKey = selectedOption?.Key;
        var selected = string.IsNullOrWhiteSpace(selectedKey)
            ? buckets
            : buckets.Where(bucket => QuotaDisplayBuilder.LimitKey(bucket) == selectedKey);

        return selected.ToList();
    }

    public IReadOnlyList<QuotaDisplayRow> BuildQuotaRows(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now)
    {
        return QuotaDisplayBuilder.BuildQuotaRows(selectedBuckets, now);
    }

    public IReadOnlyList<QuotaDisplayRow> BuildCompactRows(IEnumerable<QuotaDisplayRow> selectedRows)
    {
        return QuotaDisplayBuilder.BuildCompactRows(selectedRows);
    }

    public string BuildCompactTitle(
        IEnumerable<RateLimitBucket> selectedBuckets,
        IEnumerable<RateLimitBucket> allBuckets,
        RateLimitOption? selectedOption)
    {
        var preferredBuckets = selectedBuckets.Any() ? selectedBuckets : allBuckets;
        var selectedLabel = selectedOption?.DisplayName
            ?? QuotaDisplayBuilder.SanitizeDisplayLabel(preferredBuckets.FirstOrDefault()?.GroupLabel);

        return $"PulseMeter - {selectedLabel}";
    }

    public string BuildCompactQuotaSummary(IEnumerable<QuotaDisplayRow> compactRows)
    {
        var parts = compactRows.Select(row => $"{row.Label} \u2022 {row.CompactRemainingPercentText}");
        var bucketSummary = string.Join(" | ", parts);

        return string.IsNullOrWhiteSpace(bucketSummary) ? "usage unavailable" : bucketSummary;
    }

    public string BuildExpandedQuotaSummary(IEnumerable<QuotaDisplayRow> compactRows)
    {
        var parts = compactRows.Select(row =>
            $"{row.Label}: {row.RemainingPercentText} & resets {row.ResetDisplayText}");
        var bucketSummary = string.Join("  |  ", parts);

        return string.IsNullOrWhiteSpace(bucketSummary) ? "Usage unavailable" : bucketSummary;
    }
}
