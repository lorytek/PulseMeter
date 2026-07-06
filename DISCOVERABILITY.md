# PulseMeter Discoverability Notes

PulseMeter is currently optimized for real users and feedback, not vanity metrics. The main conversion path is GitHub-first: README, release ZIP, issues, and targeted communities.

## Baseline

- Primary goal: more real Windows users downloading the portable ZIP and giving feedback.
- Current app download: `PulseMeter-0.1.1-win-x64-portable.zip`.
- Feedback hub: https://github.com/lorytek/PulseMeter/issues/4
- First external listing request: https://github.com/RoggeOhta/awesome-codex-cli/issues/131

## Share Copy

Title:

```text
I made a local Windows tray app to see OpenAI Codex rate limits and reset credits
```

Short body:

```text
PulseMeter is a free, local-first Windows tray app for OpenAI Codex usage limits.

It shows:
- 5-hour and weekly rate-limit remaining
- reset-credit expiry dates when available
- account usage and estimated local project usage

It has no telemetry, does not scrape the Codex UI, and does not store credentials.

Download: https://github.com/lorytek/PulseMeter/releases/latest
Feedback: https://github.com/lorytek/PulseMeter/issues/4
```

## Where To Share

- Narrow Reddit posts for Codex, Windows developer tools, and local-first tooling communities.
- Hacker News `Show HN` once the post can emphasize the technical build and privacy boundaries.
- Codex-related awesome lists and curated tool directories that explicitly accept submissions.

Avoid reposting the same generic pitch. Lead with the concrete user problem: seeing Codex limits and reset credits from Windows without telemetry.

## Submission Tracker

| Date | Channel | Status | Link |
| --- | --- | --- | --- |
| 2026-07-06 | GitHub topics and repo metadata | Done | https://github.com/lorytek/PulseMeter |
| 2026-07-06 | Pinned PulseMeter feedback issue | Done | https://github.com/lorytek/PulseMeter/issues/4 |
| 2026-07-06 | Awesome Codex CLI listing request | Submitted | https://github.com/RoggeOhta/awesome-codex-cli/issues/131 |

## Weekly Check

Track these every week for the first month:

- release ZIP downloads
- GitHub traffic referrers
- stars
- feedback issue comments
- new issues or discussions elsewhere
- external listings submitted or merged

Targets:

- Week 1: 25 total `v0.1.1` downloads and 3 useful feedback signals.
- Week 2: 50 downloads, 10 stars, and 1 external listing submitted or merged.
- Week 4: decide whether a tiny website is worth maintaining.
