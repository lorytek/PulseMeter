namespace PulseMeter.VisualHarness;

public enum VisualHarnessScenario
{
    Healthy,
    Unavailable,
    Stale
}

public static class VisualHarnessScenarioParser
{
    public static VisualHarnessScenario Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--scenario=", StringComparison.OrdinalIgnoreCase))
            {
                return ParseValue(argument["--scenario=".Length..]);
            }

            if (argument.Equals("--scenario", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Count)
                {
                    throw new ArgumentException("The visual harness --scenario option requires a value.", nameof(args));
                }

                return ParseValue(args[index + 1]);
            }
        }

        return VisualHarnessScenario.Healthy;
    }

    private static VisualHarnessScenario ParseValue(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "healthy" or "mock" => VisualHarnessScenario.Healthy,
            "unavailable" => VisualHarnessScenario.Unavailable,
            "stale" => VisualHarnessScenario.Stale,
            _ => throw new ArgumentException(
                $"Unknown visual harness scenario '{value}'. Use healthy, unavailable, or stale.",
                nameof(value))
        };
    }
}
