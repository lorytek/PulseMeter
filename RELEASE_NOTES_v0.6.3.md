# PulseMeter 0.6.3

PulseMeter 0.6.3 makes Coding Runway confidence more honest, clarifies the chart, and gives the floating window a proper minimize action without sacrificing live taskbar visibility.

## Download

Download `PulseMeter-0.6.3-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

The release also includes `PulseMeter-0.6.3-win-x64-portable.zip.sha256` for integrity verification.

On the GitHub release page, use the portable ZIP above for the app. GitHub's automatic `Source code (zip)` and `Source code (tar.gz)` downloads are source archives for developers, not the Windows app.

## New In 0.6.3

- Coding Runway now reports runway confidence separately from activity-qualified baseline maturity. Medium-confidence forecasts use their modeled timing range instead of presenting one precise point as established fact.
- Usage Momentum now describes differences as percentage points per hour, avoiding confusion with relative percentage changes and the separate wall-clock runway pace.
- The chart legend now mirrors the rendered recorded usage, unmeasured gaps, forecast, sustainable pace, 100% limit, and reset time.
- Compact and expanded headers include a dedicated Minimize action. PulseMeter respects a window intentionally minimized by the user instead of reopening it during foreground monitoring.
- The Windows taskbar retains the live weekly usage badge. The notification-area icon remains the recognizable PulseMeter icon and exposes the current weekly percentage in its tooltip.

## Reliability And Privacy

- The forecast still uses local rate-limit observations, measurement-gap handling, recency weighting, and statistical uncertainty; this release improves how that evidence is communicated rather than claiming additional account knowledge.
- No new account identity, plan, prompt, response, command, or project-content data is collected.
- PulseMeter remains local-only and has no telemetry.

## Minimum Requirements

- Windows 10 or Windows 11, 64-bit.
- No .NET install required for the portable release ZIP.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

## Unsigned App Notice

This is an unsigned alpha build. Windows may show an unknown-publisher or SmartScreen warning. Only run a release downloaded from a PulseMeter release page you trust.

## License

PulseMeter is open source under the Apache License 2.0. See [LICENSE](LICENSE).
