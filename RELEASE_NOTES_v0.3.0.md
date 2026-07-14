# PulseMeter 0.3.0

PulseMeter 0.3.0 makes live quota readings more trustworthy, improves navigation and multi-monitor behavior, and makes local token-burn evidence easier to understand.

## Download

Download `PulseMeter-0.3.0-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

The release also includes `PulseMeter-0.3.0-win-x64-portable.zip.sha256` for integrity verification.

On the GitHub release page, use the portable ZIP above for the app. GitHub also shows `Source code (zip)` and `Source code (tar.gz)` automatically for every release tag; those are source archives for developers, not the Windows app download.

## New In 0.3.0

### More Trustworthy Live Usage

- PulseMeter confirms suspicious live quota readings before replacing the last trusted values.
- If two readings still disagree, the app shows the last confirmed values with a stale status instead of presenting a likely regression as live data.
- Normal refreshes reuse the local Codex app-server connection, avoiding unnecessary process churn while keeping reconnects available for errors and confirmation reads.

### Better Navigation And Window Behavior

- Expanded navigation now provides direct section jumps and top-aligned destinations in the content area.
- Needs Attention is easier to reach, Rate Limits Daily is presented as Weekly Pace, and Customize has a dedicated persistent menu entry.
- Keyboard navigation and focus behavior are more predictable.
- Window sizing and placement remain usable when moving between monitors or when the available work area changes.

### Clearer Burn Analysis

- Repeated low-level token updates are grouped into per-minute burn moments so one chat does not fill the table with near-identical rows.
- Duplicate cumulative token records are ignored before local attribution is calculated.
- Burn Analysis remains local and estimated; PulseMeter does not parse or render Codex prompt/message bodies.

### Adaptive Compact HUD

- When Codex returns only the weekly quota, the weekly-only compact layout keeps its remaining value and reset time on one aligned row.
- When both the 5-hour and weekly quotas are available, the established two-limit layout is preserved.
- Weekly pacing uses weekday names, compact quota accents remain visible, and status/actions keep their reserved space.

### Documentation

- README screenshots now show the current overview, Burn Analysis, project attribution, and compact HUD.
- Download links, package commands, and release metadata now point to 0.3.0.

## Minimum Requirements

- Windows 10 or Windows 11, 64-bit.
- No .NET install required for the portable release ZIP.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

## Unsigned App Notice

This is an unsigned alpha build. Windows may show an unknown-publisher or SmartScreen warning. If you trust this release ZIP, choose `More info`, then `Run anyway`.

## Privacy And Accuracy

PulseMeter is local-only and has no telemetry. Project usage, Burn Analysis, automatic alert signals, Limit Runway, and Idle Drain Detector are local estimates and diagnostics, not billing-exact claims.

## License

PulseMeter is open source under the Apache License 2.0. See [LICENSE](LICENSE).
