# Privacy

PulseMeter is a local Windows companion app. It does not run a local web server, collect telemetry, or send usage analytics to the maintainer.

PulseMeter is unofficial and is not affiliated with OpenAI.

## Local Data Read

In live mode, PulseMeter may read:

- Codex CLI/app-server output over stdio.
- `%USERPROFILE%\.codex\auth.json` only to request reset-credit expiry metadata from OpenAI.
- `%USERPROFILE%\.codex\state_5.sqlite`, `%USERPROFILE%\.codex\sessions`, and local rollout `token_count` records to estimate local project usage and Burn Analysis shares.

PulseMeter does not parse or display Codex message text for project usage or Burn Analysis estimates. Burn Analysis groups local session token metadata by project and does not display chat titles or prompt text. Project paths and thread IDs remain local attribution metadata. Historical attribution is labeled local and estimated, and is scaled against account-level daily usage rather than treated as billing-exact.

Automatic alert signals use local usage and rate-limit numbers. They do not require prompt text or Codex message content.

## Network Requests

PulseMeter relies on local Codex app-server responses for usage data. For reset-credit expiry dates, it may call OpenAI's ChatGPT backend endpoint with the local Codex auth bearer token.

PulseMeter does not send usage analytics, crash reports, or maintainer-owned tracking requests.

## Local Data Written

PulseMeter stores its own settings under `%LOCALAPPDATA%\PulseMeter`, including window state, sync settings, and inferred reset-credit expiry timestamps.

## Credentials

PulseMeter does not ask for passwords, API keys, or tokens. It does not display, log, or store Codex access tokens, account IDs, or server credit IDs.

## Uninstalling Local Data

To remove PulseMeter's local settings, exit the app from the tray menu and delete `%LOCALAPPDATA%\PulseMeter`.
