# PulseMeter 0.6.2

PulseMeter 0.6.2 makes handoff notes durable, keeps live weekly usage visible in Windows, and makes Coding Runway and Block Planner decisions easier to understand after startup or idle periods.

## Download

Download `PulseMeter-0.6.2-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

The release also includes `PulseMeter-0.6.2-win-x64-portable.zip.sha256` for integrity verification.

On the GitHub release page, use the portable ZIP above for the app. GitHub's automatic `Source code (zip)` and `Source code (tar.gz)` downloads are source archives for developers, not the Windows app.

## New In 0.6.2

- Persistent Return Notes now support multiple handoff notes across app restarts. Note payloads are protected with Windows Data Protection for the current Windows user and remain local.
- Coding Runway shows progressive activity-qualified evidence, clearer sustainable-pace colors, improved chart labels, and a smoother live momentum gauge.
- Block Planner now uses an established activity-qualified baseline when a quiet restart makes the recent forecast temporarily low-confidence, while genuinely immature or risky forecasts remain conservative.
- The running Windows taskbar button and notification-area icon can show the live weekly percentage using consistent green, blue, orange, and red status bands.
- Project location actions now include Open in Codex alongside Explorer and Windows PowerShell after explicit folder confirmation.
- Header usage visibility, selected-project actions, block-duration controls, keyboard navigation, startup guidance, and local shortcut publishing were refined.

## Reliability And Privacy

- Return Notes are validated, bounded, stored atomically, and never uploaded. The app continues to advise against entering secrets or customer data.
- Stale, unavailable, syncing, and mock usage states fall back to the normal PulseMeter taskbar and tray presentation rather than showing an outdated percentage.
- Project launch actions revalidate the selected path and use bounded Windows launch surfaces without storing arbitrary commands.
- PulseMeter remains local-only, has no telemetry, and does not upload prompts, responses, paths, notes, or diagnostic snapshots.

## Minimum Requirements

- Windows 10 or Windows 11, 64-bit.
- No .NET install required for the portable release ZIP.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

## Unsigned App Notice

This is an unsigned alpha build. Windows may show an unknown-publisher or SmartScreen warning. Only run a release downloaded from a PulseMeter release page you trust.

## License

PulseMeter is open source under the Apache License 2.0. See [LICENSE](LICENSE).
