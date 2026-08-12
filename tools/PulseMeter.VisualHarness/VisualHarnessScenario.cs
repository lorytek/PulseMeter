namespace PulseMeter.VisualHarness;

public enum VisualHarnessScenario
{
    Healthy,
    Unavailable,
    Stale
}

public enum DesktopProcessSnapshotVisualScenario
{
    None,
    Idle,
    Complete,
    Partial
}

public enum ProjectLocationVisualScenario
{
    None,
    Success,
    InvalidLocation,
    LaunchFailed
}

public static class VisualHarnessScenarioParser
{
    public static bool ShouldOpenSupportSnapshot(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Any(argument =>
            argument.Equals("--support-snapshot", StringComparison.OrdinalIgnoreCase));
    }

    public static DesktopProcessSnapshotVisualScenario ParseDesktopProcessSnapshot(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.Equals("--desktop-process-snapshot", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return ParseDesktopProcessSnapshotValue(args[index + 1]);
                }

                return DesktopProcessSnapshotVisualScenario.Idle;
            }

            if (argument.StartsWith("--desktop-process-snapshot=", StringComparison.OrdinalIgnoreCase))
            {
                return ParseDesktopProcessSnapshotValue(argument["--desktop-process-snapshot=".Length..]);
            }
        }

        return DesktopProcessSnapshotVisualScenario.None;
    }

    public static ProjectLocationVisualScenario ParseProjectLocation(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.Equals("--project-location", StringComparison.OrdinalIgnoreCase))
            {
                return index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                    ? ParseProjectLocationValue(args[index + 1])
                    : ProjectLocationVisualScenario.Success;
            }

            if (argument.StartsWith("--project-location=", StringComparison.OrdinalIgnoreCase))
            {
                return ParseProjectLocationValue(argument["--project-location=".Length..]);
            }
        }

        return ProjectLocationVisualScenario.None;
    }

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

    private static DesktopProcessSnapshotVisualScenario ParseDesktopProcessSnapshotValue(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "idle" => DesktopProcessSnapshotVisualScenario.Idle,
            "complete" => DesktopProcessSnapshotVisualScenario.Complete,
            "partial" => DesktopProcessSnapshotVisualScenario.Partial,
            _ => throw new ArgumentException(
                $"Unknown desktop process snapshot scenario '{value}'. Use idle, complete, or partial.",
                nameof(value))
        };
    }

    private static ProjectLocationVisualScenario ParseProjectLocationValue(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "success" => ProjectLocationVisualScenario.Success,
            "invalid" => ProjectLocationVisualScenario.InvalidLocation,
            "failed" => ProjectLocationVisualScenario.LaunchFailed,
            _ => throw new ArgumentException("Unknown project location scenario. Use success, invalid, or failed.", nameof(value))
        };
    }
}
