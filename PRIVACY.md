# Privacy

PulseMeter is a local Windows companion app. It does not run a local web server, collect telemetry, or send usage analytics to the maintainer.

PulseMeter is unofficial and is not affiliated with OpenAI.

## Local Data Read

In live mode, PulseMeter may read:

- Codex CLI/app-server output over stdio.
- `%USERPROFILE%\.codex\auth.json` only to request reset-credit expiry metadata from OpenAI.
- `%USERPROFILE%\.codex\state_5.sqlite` and `%USERPROFILE%\.codex\sessions` to estimate local project usage shares.

PulseMeter does not parse or display Codex message text for project usage estimates.

## Network Requests

PulseMeter relies on local Codex app-server responses for usage data. For reset-credit expiry dates, it may call OpenAI's ChatGPT backend endpoint with the local Codex auth bearer token.

PulseMeter does not send usage analytics, crash reports, or maintainer-owned tracking requests.

## Local Data Written

PulseMeter stores its own settings under `%LOCALAPPDATA%\PulseMeter`, including window state, sync settings, and inferred reset-credit expiry timestamps.

## Credentials

PulseMeter does not ask for passwords, API keys, or tokens. It does not display, log, or store Codex access tokens, account IDs, or server credit IDs.

## Uninstalling Local Data

To remove PulseMeter's local settings, exit the app from the tray menu and delete `%LOCALAPPDATA%\PulseMeter`.
