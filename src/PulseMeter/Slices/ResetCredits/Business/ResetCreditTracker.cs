using System.Globalization;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.ResetCredits.Business;

public sealed class ResetCreditTracker
{
    private static readonly TimeSpan DefaultCreditLifetime = TimeSpan.FromDays(30);
    private readonly List<TrackedResetCredit> _credits = new();
    private bool _hasObservedAvailableCount;
    private int _nextCreditNumber = 1;

    public ResetCreditTracker(ResetCreditTrackerState? state = null)
    {
        if (state is null)
        {
            return;
        }

        _hasObservedAvailableCount = state.HasObservedAvailableCount;
        _nextCreditNumber = Math.Max(1, state.NextCreditNumber);

        foreach (var credit in state.Credits)
        {
            if (credit.Number <= 0)
            {
                continue;
            }

            _credits.Add(new TrackedResetCredit(credit.Number, credit.ExpiresAtUtc, credit.HasExactExpiry));
            _nextCreditNumber = Math.Max(_nextCreditNumber, credit.Number + 1);
        }
    }

    public IReadOnlyList<ResetCreditListItem> Update(
        int? availableCount,
        DateTimeOffset? appServerExpiryUtc,
        DateTimeOffset nowUtc)
    {
        return Update(availableCount, appServerExpiryUtc, Array.Empty<ResetCreditSnapshot>(), nowUtc);
    }

    public IReadOnlyList<ResetCreditListItem> Update(
        int? availableCount,
        DateTimeOffset? appServerExpiryUtc,
        IReadOnlyList<ResetCreditSnapshot> exactCredits,
        DateTimeOffset nowUtc)
    {
        if (availableCount is null)
        {
            _credits.Clear();
            _hasObservedAvailableCount = false;
            _nextCreditNumber = 1;
            return Array.Empty<ResetCreditListItem>();
        }

        var targetCount = Math.Max(0, availableCount.Value);
        if (exactCredits.Count > 0)
        {
            _credits.Clear();
            var sortedCredits = exactCredits
                .OrderBy(credit => credit.ExpiresAtUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(credit => credit.GrantedAtUtc ?? DateTimeOffset.MaxValue)
                .ToList();

            for (var index = 0; index < sortedCredits.Count; index++)
            {
                _credits.Add(new TrackedResetCredit(index + 1, sortedCredits[index].ExpiresAtUtc, true));
            }

            _nextCreditNumber = _credits.Count + 1;
            _hasObservedAvailableCount = true;
            return Refresh(nowUtc);
        }

        if (targetCount < _credits.Count)
        {
            _credits.RemoveRange(targetCount, _credits.Count - targetCount);
        }

        if (appServerExpiryUtc is DateTimeOffset providedExpiry)
        {
            for (var index = 0; index < _credits.Count; index++)
            {
                _credits[index] = _credits[index] with { ExpiresAtUtc = providedExpiry };
            }
        }

        var isInitialObservation = !_hasObservedAvailableCount;
        while (_credits.Count < targetCount)
        {
            _credits.Add(new TrackedResetCredit(
                _nextCreditNumber++,
                appServerExpiryUtc ?? (isInitialObservation ? null : nowUtc.AddDays(30)),
                false));
        }

        _hasObservedAvailableCount = true;
        return Refresh(nowUtc);
    }

    public ResetCreditTrackerState CaptureState()
    {
        return new ResetCreditTrackerState(
            _hasObservedAvailableCount,
            _nextCreditNumber,
            _credits
                .Select(credit => new ResetCreditState(credit.Number, credit.ExpiresAtUtc, credit.HasExactExpiry))
                .ToList());
    }

    public IReadOnlyList<ResetCreditListItem> Refresh(DateTimeOffset nowUtc)
    {
        return _credits
            .Select(credit => new ResetCreditListItem(
                credit.Number,
                FormatExpiry(credit.ExpiresAtUtc, nowUtc, credit.HasExactExpiry),
                CalculateExpiryProgressValue(credit.ExpiresAtUtc, nowUtc),
                GetExpiryProgressBrush(credit.ExpiresAtUtc, nowUtc)))
            .ToList();
    }

    public static string FormatExpiry(DateTimeOffset? expiresAtUtc, DateTimeOffset nowUtc, bool includeDate = false)
    {
        if (expiresAtUtc is not DateTimeOffset expiry)
        {
            return "expiry unavailable";
        }

        var remaining = expiry - nowUtc;
        if (remaining <= TimeSpan.Zero)
        {
            return "expired";
        }

        if (includeDate)
        {
            var localExpiry = expiry.ToLocalTime();
            return $"expires {localExpiry.ToString("MMM d HH:mm", CultureInfo.InvariantCulture)} ({FormatRemaining(remaining)})";
        }

        return FormatRemaining(remaining, includeExpiresPrefix: true);
    }

    private static string FormatRemaining(TimeSpan remaining, bool includeExpiresPrefix = false)
    {
        var prefix = includeExpiresPrefix ? "expires " : string.Empty;

        if (remaining.TotalDays >= 1)
        {
            var days = Math.Max(1, (int)Math.Ceiling(remaining.TotalDays));
            return days == 1 ? $"{prefix}in 1 day" : $"{prefix}in {days} days";
        }

        if (remaining.TotalHours >= 1)
        {
            var hours = Math.Max(1, (int)Math.Ceiling(remaining.TotalHours));
            return hours == 1 ? $"{prefix}in 1 hour" : $"{prefix}in {hours} hours";
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return minutes == 1 ? $"{prefix}in 1 minute" : $"{prefix}in {minutes} minutes";
    }

    private static double CalculateExpiryProgressValue(DateTimeOffset? expiresAtUtc, DateTimeOffset nowUtc)
    {
        if (expiresAtUtc is not DateTimeOffset expiry)
        {
            return 0;
        }

        var remaining = expiry - nowUtc;
        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }

        return Math.Clamp(remaining.TotalMilliseconds / DefaultCreditLifetime.TotalMilliseconds * 100, 0, 100);
    }

    private static string GetExpiryProgressBrush(DateTimeOffset? expiresAtUtc, DateTimeOffset nowUtc)
    {
        if (expiresAtUtc is not DateTimeOffset expiry)
        {
            return "#9CA3AF";
        }

        var remaining = expiry - nowUtc;
        if (remaining <= TimeSpan.Zero)
        {
            return "#EF4444";
        }

        var remainingRatio = Math.Clamp(
            remaining.TotalMilliseconds / DefaultCreditLifetime.TotalMilliseconds,
            0,
            1);

        return remainingRatio >= 0.5
            ? InterpolateHexColor("#F59E0B", "#22C55E", (remainingRatio - 0.5) * 2)
            : InterpolateHexColor("#EF4444", "#F59E0B", remainingRatio * 2);
    }

    private static string InterpolateHexColor(string startHex, string endHex, double amount)
    {
        var normalizedAmount = Math.Clamp(amount, 0, 1);
        var startR = Convert.ToInt32(startHex.Substring(1, 2), 16);
        var startG = Convert.ToInt32(startHex.Substring(3, 2), 16);
        var startB = Convert.ToInt32(startHex.Substring(5, 2), 16);
        var endR = Convert.ToInt32(endHex.Substring(1, 2), 16);
        var endG = Convert.ToInt32(endHex.Substring(3, 2), 16);
        var endB = Convert.ToInt32(endHex.Substring(5, 2), 16);

        var r = (int)Math.Round(startR + ((endR - startR) * normalizedAmount));
        var g = (int)Math.Round(startG + ((endG - startG) * normalizedAmount));
        var b = (int)Math.Round(startB + ((endB - startB) * normalizedAmount));

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private sealed record TrackedResetCredit(int Number, DateTimeOffset? ExpiresAtUtc, bool HasExactExpiry);
}
