# Contributing

Thanks for helping improve PulseMeter.

By participating, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## What To Open

Small bug fixes and documentation fixes can open a pull request directly.

For new features, architecture changes, dependency additions, and refactor-only work, please open an issue first and wait for maintainer agreement before implementing. This keeps the app small, privacy-focused, and maintainable.

## Bug Fixes

Every bug-fix pull request should include:

- A reproduction: minimal steps, a failing test, or a short explanation of the bad behavior.
- A test that fails before the fix and passes after, when practical.
- Manual verification details when an automated test is not practical.

## Real Behavior Proof

Every pull request should include a `Real Behavior Proof` section with:

- Environment tested on, including Windows version.
- Exact commands or manual steps run after the patch.
- Observed result.
- What was not tested.

For UI changes, include a screenshot or short recording when possible.

## Dependency Changes

Dependency changes need written justification:

- Why this package or version is needed.
- Whether it changes install/runtime surface.
- Whether it adds native code or network behavior.
- How the dependency is maintained.

Cosmetic dependency churn may be declined.

## Development Checks

Run the narrowest useful checks for your change. For most source changes:

```powershell
dotnet test PulseMeter.slnx -c Release
dotnet build PulseMeter.slnx -c Release
```

For release/package changes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -Version 0.5.0
```

## Security

Do not include access tokens, auth files, account IDs, local file paths, or private Codex session contents in public issues or pull requests. Follow [SECURITY.md](SECURITY.md) for vulnerability reports.
