$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$artifactRoot = Join-Path $env:LOCALAPPDATA "PulseMeter\TestArtifacts\$timestamp"

$env:PULSEMETER_REPO_ROOT = $repoRoot

dotnet test (Join-Path $repoRoot "PulseMeter.slnx") `
    -c Release `
    --artifacts-path $artifactRoot
