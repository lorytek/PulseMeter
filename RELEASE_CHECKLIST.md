# Release Checklist

Use this before publishing PulseMeter publicly.

- [ ] Confirm `LICENSE` is Apache License 2.0 and README says PulseMeter is open source under Apache-2.0.
- [ ] Confirm the repository does not include local shortcuts, `artifacts`, `bin`, `obj`, `.agents`, `.codex`, `pulsemeter_build_brief.md`, or `docs/superpowers`.
- [ ] Confirm community files are present: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `.github/CODEOWNERS`, issue templates, pull request template, `llms.txt`, `.gitleaks.toml`, Dependabot, and security workflow.
- [ ] Run `dotnet test PulseMeter.slnx -c Release`.
- [ ] Run `dotnet build PulseMeter.slnx -c Release`.
- [ ] Run `powershell -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -Version 0.4.0`.
- [ ] Smoke-test the zip from `artifacts\release` on a clean Windows account or VM.
- [ ] Confirm README, privacy, and security docs match the shipped behavior.
- [ ] Confirm release notes clearly mark this as an unsigned alpha build.
- [ ] Confirm GitHub private vulnerability reporting is enabled if available for the repository.
- [ ] Attach `PulseMeter-0.4.0-win-x64-portable.zip` and its `.sha256` checksum file to the GitHub release.

## Current Public-Release Caveats

- PulseMeter is not code-signed yet.
- Exact current Codex Desktop thread detection is not implemented.
- Project usage and Burn Analysis are estimates from local Codex metadata, not billing-exact splits.
- Reset-credit expiry relies on an observed ChatGPT backend endpoint and may change.
