# PulseMeter 0.3.1

PulseMeter 0.3.1 fixes live synchronization after a Codex reset credit is used.

## Download

Download `PulseMeter-0.3.1-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

The release also includes `PulseMeter-0.3.1-win-x64-portable.zip.sha256` for integrity verification.

On the GitHub release page, use the portable ZIP above for the app. GitHub's automatic `Source code (zip)` and `Source code (tar.gz)` downloads are source archives for developers, not the Windows app.

## Fixed In 0.3.1

- Using a reset credit no longer leaves PulseMeter stuck on stale pre-reset quota values.
- PulseMeter now reads the available reset-credit count before judging a sudden quota change.
- A confirmed consumed credit may clear the short rolling limit while the weekly limit remains live.
- Suspicious weekly drops, missing limits without a consumed credit, and empty responses still require confirmation and retain the last trusted values when necessary.

## Minimum Requirements

- Windows 10 or Windows 11, 64-bit.
- No .NET install required for the portable release ZIP.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

## Unsigned App Notice

This is an unsigned alpha build. Windows may show an unknown-publisher or SmartScreen warning. Only run a release downloaded from a PulseMeter release page you trust.

## Privacy And Accuracy

PulseMeter is local-only and has no telemetry. Project usage, Burn Analysis, automatic alert signals, Limit Runway, and Idle Drain Detector are local estimates and diagnostics, not billing-exact claims.

## License

PulseMeter is open source under the Apache License 2.0. See [LICENSE](LICENSE).
