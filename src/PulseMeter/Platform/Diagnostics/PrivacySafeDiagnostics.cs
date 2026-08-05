using System.Diagnostics;

namespace PulseMeter.Platform.Diagnostics;

internal static class PrivacySafeDiagnostics
{
    [Conditional("DEBUG")]
    public static void WriteInfo(string message)
    {
        Debug.WriteLine("[pulsemeter] " + message);
    }

    [Conditional("DEBUG")]
    public static void WriteFailure(string operation, Exception? exception)
    {
        var failureType = exception?.GetBaseException().GetType().Name ?? "UnknownError";
        Debug.WriteLine($"[pulsemeter] {operation}: {failureType}");
    }
}
