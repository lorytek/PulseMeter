# Changelog

## 0.3.0

- Added confirmation of suspicious live quota regressions and last-confirmed-value fallback when readings disagree.
- Reused the local Codex app-server connection during normal refreshes to reduce unnecessary process churn.
- Improved expanded navigation with direct section jumps, top-aligned destinations, a persistent Customize menu, and keyboard access.
- Improved window sizing and placement across multiple monitors and changing work areas.
- Grouped repeated token-count updates into clearer Burn Analysis moments and removed duplicate cumulative events.
- Added an adaptive weekly-only compact HUD while preserving the normal two-limit layout.
- Updated the dashboard and compact HUD screenshots for the current interface.

## 0.2.1

- Redesigned the compact tray to show each limit and its reset time in a clear two-line group.
- Shows local reset time for 5-hour limits and weekday plus time for weekly limits.
- Keeps tray action buttons visible with a dedicated action column at all compact statuses.
- Prevents compact quota text from clipping when usage or sync labels are longer.

## 0.2.0

- Added Burn Analysis for top local chats by estimated token burn and largest token-burn events.
- Added Needs Attention signals for local usage, rate-limit risk, and sync issues.
- Added Limit Runway and Idle Drain diagnostics to make usage risk clearer.
- Expanded Mock Mode showcase data so demo mode includes representative alerts and attention states.
- Improved Burn Analysis layout and labels, including responsive stacking at narrow widths.
- Clarified privacy notes for local token attribution: estimated, local, and never rendering prompt/message bodies.

## 0.1.1

- Re-licensed PulseMeter under the Apache License 2.0.
- Added public contribution, issue, pull request, security, Code of Conduct, and agent-index docs.
- Added supply-chain guardrails for dependency updates, CodeQL, and secret scanning.
- Prepared the repository for a clean open-source source drop.

## 0.1.0

- Initial public-prep build of PulseMeter.
- Light floating HUD with compact and expanded usage views.
- Tray icon with show, hide, refresh, mock mode, and exit controls.
- Live Codex CLI/app-server usage sync when available.
- Reset-credit display with expiry dates when available.
- Estimated project and daily usage sections.
- Unsigned portable Windows release package.
