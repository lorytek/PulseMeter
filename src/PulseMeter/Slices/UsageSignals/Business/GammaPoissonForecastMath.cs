namespace PulseMeter.Slices.UsageSignals.Business;

internal static class GammaPoissonForecastMath
{
    internal const double AtRiskProbability = 0.50;
    internal const double ActionProbability = 0.90;
    private const double MaximumExhaustionMinutes = 365 * 24 * 60;

    internal static bool IsAtRisk(double exhaustionProbability) =>
        exhaustionProbability >= AtRiskProbability;

    internal static bool HasActionableProbability(double exhaustionProbability) =>
        exhaustionProbability >= ActionProbability;

    internal static int CountQuantile(
        double quantile,
        int cappedCount,
        double posteriorShape,
        double posteriorExposureMinutes,
        double futureMinutes)
    {
        if (futureMinutes <= 0 || cappedCount <= 0)
        {
            return 0;
        }

        var successProbability = posteriorExposureMinutes / (posteriorExposureMinutes + futureMinutes);
        var failureProbability = 1 - successProbability;
        var probability = Math.Exp(posteriorShape * Math.Log(successProbability));
        var cumulative = probability;
        if (cumulative >= quantile)
        {
            return 0;
        }

        for (var count = 1; count < cappedCount; count++)
        {
            probability *= ((posteriorShape + count - 1) / count) * failureProbability;
            cumulative += probability;
            if (cumulative >= quantile)
            {
                return count;
            }
        }

        return cappedCount;
    }

    internal static double ExhaustionProbability(
        int remainingPoints,
        double posteriorShape,
        double posteriorExposureMinutes,
        double futureMinutes)
    {
        if (futureMinutes <= 0)
        {
            return 0;
        }

        var successProbability = posteriorExposureMinutes / (posteriorExposureMinutes + futureMinutes);
        var failureProbability = 1 - successProbability;
        var probability = Math.Exp(posteriorShape * Math.Log(successProbability));
        var cumulative = probability;
        for (var count = 1; count < remainingPoints; count++)
        {
            probability *= ((posteriorShape + count - 1) / count) * failureProbability;
            cumulative += probability;
        }

        return Math.Clamp(1 - cumulative, 0, 1);
    }

    internal static double? ExhaustionQuantileMinutes(
        double quantile,
        int remainingPoints,
        double posteriorShape,
        double posteriorExposureMinutes)
    {
        var upper = Math.Max(1, posteriorExposureMinutes);
        while (upper < MaximumExhaustionMinutes
               && ExhaustionProbability(
                   remainingPoints,
                   posteriorShape,
                   posteriorExposureMinutes,
                   upper) < quantile)
        {
            upper = Math.Min(MaximumExhaustionMinutes, upper * 2);
        }

        if (ExhaustionProbability(
                remainingPoints,
                posteriorShape,
                posteriorExposureMinutes,
                upper) < quantile)
        {
            return null;
        }

        var lower = 0d;
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var midpoint = (lower + upper) / 2;
            if (ExhaustionProbability(
                    remainingPoints,
                    posteriorShape,
                    posteriorExposureMinutes,
                    midpoint) >= quantile)
            {
                upper = midpoint;
            }
            else
            {
                lower = midpoint;
            }
        }

        return upper;
    }
}
