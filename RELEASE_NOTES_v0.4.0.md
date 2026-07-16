# PulseMeter 0.4.0

PulseMeter 0.4.0 turns the local usage monitor into a clearer analytical dashboard while keeping estimates local and explicitly evidence-labelled.

## Download

Download `PulseMeter-0.4.0-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

The release also includes `PulseMeter-0.4.0-win-x64-portable.zip.sha256` for integrity verification.

On the GitHub release page, use the portable ZIP above for the app. GitHub's automatic `Source code (zip)` and `Source code (tar.gz)` downloads are source archives for developers, not the Windows app.

## New In 0.4.0

- Project Health compares each local project's last 7 days with the prior 7 days and shows its attributed 30-day share.
- Runway Forecast projects whether the selected rate-limit track may run out before reset from recent confirmed samples.
- Rate Limits and Weekly Pace now present remaining capacity, reset timing, and pace guidance more clearly.
- Project-level Burn Analysis replaces the retired chat-level and burn-moment tables.
- Needs Attention is first in the dashboard and consolidates the signals that need action.
- Navigation jumps sections to the top, supports customization, and behaves more reliably across window sizes and monitors.
- Compact mode adapts to the available quota tracks and collapses when Codex becomes the foreground app on the same monitor.

## Reliability And Privacy

- Live sync confirmation handles reset-credit changes and suspicious quota regressions without trusting malformed or contradictory samples.
- Burn Analysis groups local session token metadata by project and does not display chat titles or prompt text.
- Project usage, Project Health, Burn Analysis, and Runway Forecast are local estimates and diagnostics, not billing-exact claims.
- PulseMeter has no telemetry and does not parse or render Codex prompt/message bodies.

## Minimum Requirements

- Windows 10 or Windows 11, 64-bit.
- No .NET install required for the portable release ZIP.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

## Unsigned App Notice

This is an unsigned alpha build. Windows may show an unknown-publisher or SmartScreen warning. Only run a release downloaded from a PulseMeter release page you trust.

## License

PulseMeter is open source under the Apache License 2.0. See [LICENSE](LICENSE).
