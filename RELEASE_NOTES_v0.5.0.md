# PulseMeter 0.5.0

PulseMeter 0.5.0 adds a graph-first Coding Runway that turns live Codex limits into a clearer, persistent analytical view.

## Download

Download `PulseMeter-0.5.0-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

The release also includes `PulseMeter-0.5.0-win-x64-portable.zip.sha256` for integrity verification.

On the GitHub release page, use the portable ZIP above for the app. GitHub's automatic `Source code (zip)` and `Source code (tar.gz)` downloads are source archives for developers, not the Windows app.

## New In 0.5.0

- Coding Runway shows actual usage, current pace, sustainable pace, estimated reach-limit timing, and reset timing on one graph.
- The selector switches truthfully between the complete 5-hour and 7-day limit windows while keeping the other chart state hidden.
- Usage momentum compares the current pace with the relevant baseline, using the 5-hour window median or median day for the weekly view.
- Rate-limit history survives app restarts, restores recent local samples, and marks periods that were not measured instead of inventing data.
- Statistical runway estimates use evidence-aware language and report either capacity remaining at reset or that the limit may be reached before reset.
- Weekly Pace now sits above Coding Runway, duplicate alerts and redundant forecast panels are removed, and navigation follows the visible dashboard order.
- The interface keeps a restrained Mac-inspired visual language with improved labels, hover details, accessibility descriptions, settings validation, and visible version information.

## Reliability And Privacy

- Local settings and runway observations use atomic persistence so interrupted writes do not silently discard the last good state.
- Recent rate-limit history can be recovered from local Codex rollout metadata and is bounded to the windows needed by the app.
- Coding Runway observations contain usage percentages, reset and observation times, rate-limit labels, and measurement gaps only.
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
