# PulseMeter 0.6.0

PulseMeter 0.6.0 turns Coding Runway into a more practical planning tool and strengthens the app's behavior across refreshes, restarts, and shutdown.

## Download

Download `PulseMeter-0.6.0-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

The release also includes `PulseMeter-0.6.0-win-x64-portable.zip.sha256` for integrity verification.

On the GitHub release page, use the portable ZIP above for the app. GitHub's automatic `Source code (zip)` and `Source code (tar.gz)` downloads are source archives for developers, not the Windows app.

## New In 0.6.0

- Block Planner checks whether a selected coding block is likely to fit the current 5-hour or 7-day limit at the measured pace.
- Window-appropriate options cover 15 minutes through 2 hours for the 5-hour limit and 1 hour through 1 day (8 hours) for the weekly limit.
- Recovery watching can notify when a risky block becomes likely to fit or when the watched quota resets.
- Usage Momentum stays neutral while learning, states exactly how much baseline data remains, and offers an accessible preview of the completed gauge.
- Coding Runway keeps keyboard point selection through live refreshes, advances forecasts after idle time, retains recent samples across restarts, and uses an exact 24-hour weekly baseline.
- Daily Usage now labels absent samples as `Not recorded` with neutral markers instead of turning missing data into zero usage.
- Expired reset credits are removed from the available count immediately and the corrected state is retained across restarts.

## Reliability And Privacy

- Application startup and shutdown now serialize concurrent lifecycle work and wait for asynchronous service disposal without double-stopping resources.
- Single-instance startup, local state recovery, sync cancellation, and Windows UI service boundaries have broader failure and concurrency coverage.
- Diagnostics remain privacy-safe and do not expose local paths, account data, credentials, or Codex message content in user-facing errors.
- PulseMeter has no telemetry and does not parse or display Codex prompt or message text.

## Minimum Requirements

- Windows 10 or Windows 11, 64-bit.
- No .NET install required for the portable release ZIP.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

## Unsigned App Notice

This is an unsigned alpha build. Windows may show an unknown-publisher or SmartScreen warning. Only run a release downloaded from a PulseMeter release page you trust.

## License

PulseMeter is open source under the Apache License 2.0. See [LICENSE](LICENSE).
