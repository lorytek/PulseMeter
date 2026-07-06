# Security

PulseMeter is an unofficial companion app for Codex usage visibility. It is not affiliated with OpenAI.

## Unsigned Releases

PulseMeter releases are currently unsigned. Windows may show an unknown-publisher or SmartScreen warning until the project uses a code-signing certificate.

Only run release packages you downloaded from a PulseMeter release page you trust.

## Supported Versions

Security fixes target the latest public release only until the project has a stable release cadence.

## Reporting A Vulnerability

Do not include access tokens, auth files, account IDs, local file paths, or private Codex session contents in public issues.

If GitHub private vulnerability reporting is enabled for the repository, use it. Otherwise, open a minimal public issue that describes the affected component without secrets, and the maintainer can coordinate privately through the contact options on the `@lorytek` GitHub profile.

Include:

- The affected PulseMeter version or commit.
- The affected source file or feature, if known.
- Steps to reproduce with secrets redacted.
- Impact and whether local Codex auth/session data may be exposed.

## Sensitive Areas

Extra care is required around:

- Reading `%USERPROFILE%\.codex\auth.json`.
- Sending reset-credit requests with a bearer token.
- Reading local Codex session metadata for project usage estimates.
- Logging or exception messages that could accidentally include paths or credentials.

PulseMeter should never print bearer tokens, account IDs, reset-credit IDs, or Codex message text.
