namespace PulseMeter.Tests;

public sealed class GammaPoissonForecastMathTests
{
    [Fact]
    public void ExhaustionProbability_MatchesGeometricReferenceCase()
    {
        var probability = GammaPoissonForecastMath.ExhaustionProbability(
            remainingPoints: 1,
            posteriorShape: 1,
            posteriorExposureMinutes: 1,
            futureMinutes: 1);

        Assert.Equal(0.5, probability, precision: 12);
    }

    [Fact]
    public void CountQuantiles_MatchGeometricReferenceCase()
    {
        var p10 = GammaPoissonForecastMath.CountQuantile(0.10, 100, 1, 1, 1);
        var p90 = GammaPoissonForecastMath.CountQuantile(0.90, 100, 1, 1, 1);

        Assert.Equal(0, p10);
        Assert.Equal(3, p90);
    }

    [Theory]
    [InlineData(0.10, 1d / 9d)]
    [InlineData(0.50, 1)]
    [InlineData(0.90, 9)]
    public void ExhaustionQuantiles_MatchBetaPrimeReferenceCase(double quantile, double expectedMinutes)
    {
        var minutes = GammaPoissonForecastMath.ExhaustionQuantileMinutes(
            quantile,
            remainingPoints: 1,
            posteriorShape: 1,
            posteriorExposureMinutes: 1);

        Assert.Equal(expectedMinutes, Assert.IsType<double>(minutes), precision: 9);
    }

    [Fact]
    public void ZeroHorizonAndCountCapHaveDeterministicBoundaries()
    {
        Assert.Equal(0, GammaPoissonForecastMath.ExhaustionProbability(1, 2, 5, 0));
        Assert.Equal(0, GammaPoissonForecastMath.CountQuantile(0.90, 10, 2, 5, 0));
        Assert.Equal(3, GammaPoissonForecastMath.CountQuantile(0.999, 3, 1, 1, 1));
    }

    [Fact]
    public void ExhaustionProbabilityIsMonotoneInHorizonRemainingCountAndExposure()
    {
        var baseline = GammaPoissonForecastMath.ExhaustionProbability(10, 4, 20, 60);
        var longerHorizon = GammaPoissonForecastMath.ExhaustionProbability(10, 4, 20, 120);
        var moreRemaining = GammaPoissonForecastMath.ExhaustionProbability(20, 4, 20, 60);
        var moreExposure = GammaPoissonForecastMath.ExhaustionProbability(10, 4, 40, 60);

        Assert.True(longerHorizon > baseline);
        Assert.True(moreRemaining < baseline);
        Assert.True(moreExposure < baseline);
    }

    [Fact]
    public void DecisionThresholdsAreInclusiveAtFiftyAndNinetyPercent()
    {
        Assert.False(GammaPoissonForecastMath.IsAtRisk(0.499999));
        Assert.True(GammaPoissonForecastMath.IsAtRisk(0.50));
        Assert.False(GammaPoissonForecastMath.HasActionableProbability(0.899999));
        Assert.True(GammaPoissonForecastMath.HasActionableProbability(0.90));
    }
}
