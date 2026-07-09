# PulseMeter 0.2.0

PulseMeter 0.2.0 adds local token-burn attribution and stronger attention signals while keeping the app local-first and unsigned-alpha clear.

## Download

Download `PulseMeter-0.2.0-win-x64-portable.zip`, extract it, and run `PulseMeter.exe`.

On the GitHub release page, use the portable ZIP above for the app. GitHub also shows `Source code (zip)` and `Source code (tar.gz)` automatically for every release tag; those are source archives for developers, not the Windows app download.

## New In 0.2.0

- Burn Analysis shows top local chats by estimated token burn for the last 30 days.
- Largest burn events highlights individual local token-count events that drove usage.
- Needs Attention groups local warning signals in one dashboard section.
- Limit Runway estimates when 5-hour or weekly usage may run out before reset.
- Idle Drain Detector flags usage movement while Windows reports you were idle.
- Mock Mode now shows representative alerts and attention states for demos.

## Privacy Notes

- Burn Analysis is local and estimated, scaled against account-level usage.
- It reads local Codex metadata and rollout `token_count` records only for attribution.
- It displays project paths, thread titles/IDs, timestamps, and token counts.
- It does not parse or render Codex prompt/message bodies.

## Minimum Requirements

- Windows 10 or Windows 11, 64-bit.
- No .NET install required.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

## Unsigned App Notice

PulseMeter is currently unsigned. Windows may show an unknown-publisher or SmartScreen warning. If you trust this release zip, choose `More info`, then `Run anyway`.

## License

PulseMeter is open source under the Apache License 2.0. See [LICENSE](LICENSE).
