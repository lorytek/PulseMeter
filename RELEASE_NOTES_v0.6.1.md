# PulseMeter 0.6.1

PulseMeter 0.6.1 makes its momentum baseline more representative of real Codex activity and adds practical local tools for resuming work, diagnosing the app, and opening observed project locations safely.

## Download

Download `PulseMeter-0.6.1-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

The release also includes `PulseMeter-0.6.1-win-x64-portable.zip.sha256` for integrity verification.

On the GitHub release page, use the portable ZIP above for the app. GitHub's automatic `Source code (zip)` and `Source code (tar.gz)` downloads are source archives for developers, not the Windows app.

## New In 0.6.1

- Activity-qualified Usage Momentum learns from completed hours with locally observed Codex activity or at least 0.1 percentage point of quota movement. Inactive zero-use hours remain available to Coding Runway but no longer dilute momentum.
- Momentum evidence now persists in a versioned schema with safe migration, bounded retention, per-window reset cutoffs, and protection against overwriting newer state formats.
- A memory-only Return Note keeps one project or task and its next step visible for the current PulseMeter run.
- Optional quick access, tray confidence indicators, first-hide guidance, and improved keyboard navigation make the app easier to reach and operate.
- Preview-first PulseMeter support snapshots and Desktop process snapshots provide privacy-bounded local diagnostics without uploading logs or message content.
- Project location actions validate a user-confirmed folder before opening Explorer or starting Windows PowerShell in that location.

## Reliability And Privacy

- Stale or unavailable cached data no longer produces actionable-looking non-sync Needs Attention signals.
- Failed baseline resets are not reported as successful, future schema files are preserved, and malformed activity events cannot create false local activity.
- Activity evidence stores only UTC hour markers and coverage state. It does not retain prompts, responses, commands, paths, thread identifiers, token amounts, account identity, balances, or authentication data in the momentum model.
- Support snapshots are previewed before copying and exclude raw Codex logs, prompts, command output, paths, session identifiers, and authentication material.
- PulseMeter has no telemetry and does not upload local diagnostic measurements.

## Minimum Requirements

- Windows 10 or Windows 11, 64-bit.
- No .NET install required for the portable release ZIP.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

## Unsigned App Notice

This is an unsigned alpha build. Windows may show an unknown-publisher or SmartScreen warning. Only run a release downloaded from a PulseMeter release page you trust.

## License

PulseMeter is open source under the Apache License 2.0. See [LICENSE](LICENSE).
