param(
    [string]$Version = "0.3.1",
    [switch]$SkipTests,
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "PulseMeter.slnx"
$project = Join-Path $root "src\PulseMeter\PulseMeter.csproj"
$releaseRoot = Join-Path $root "artifacts\release"
$output = Join-Path $releaseRoot "PulseMeter-win-x64-portable"
$version = $Version.TrimStart("v")
$zipPath = Join-Path $releaseRoot "PulseMeter-$version-win-x64-portable.zip"
$checksumPath = "$zipPath.sha256"
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

if (-not $SkipTests) {
    dotnet test $solution -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE"
    }
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained $selfContained `
    -o $output `
    /p:UseAppHost=true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:PublishReadyToRun=false `
    /p:DebugType=embedded `
    /p:DebugSymbols=false `
    /p:Version=$version `
    /p:FileVersion=$version `
    /p:InformationalVersion=$version

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $output "PulseMeter.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Published executable was not created: $exe"
}

$installText = @"
PulseMeter $version

License:
- PulseMeter is open source under the Apache License 2.0.
- You may use, modify, and redistribute PulseMeter under Apache-2.0 terms.
- See LICENSE in this folder for the full Apache-2.0 terms.

Minimum requirements:
- Windows 10 or Windows 11, 64-bit.
- No .NET install required for this self-contained portable build.
- Codex CLI installed and signed in for live usage sync.
- Internet access for Codex/OpenAI usage data.

Unsigned app notice:
- PulseMeter is currently unsigned.
- Windows may show an unknown-publisher or SmartScreen warning.
- If you trust the release zip you downloaded, choose More info, then Run anyway.

Install:
1. Extract the zip to a folder.
2. Run PulseMeter.exe.

Mock Mode works without Codex CLI and shows full showcase demo data, including alert states.

Uninstall:
1. Exit PulseMeter from the tray menu.
2. Delete the extracted folder.
3. Optional: delete %LOCALAPPDATA%\PulseMeter to remove local settings.
"@
Set-Content -LiteralPath (Join-Path $output "INSTALL.txt") -Value $installText -Encoding UTF8

foreach ($doc in @("README.md", "PRIVACY.md", "SECURITY.md", "CHANGELOG.md", "LICENSE")) {
    $source = Join-Path $root $doc
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $output $doc) -Force
    }
}

$releaseNotes = Join-Path $root "RELEASE_NOTES_v$version.md"
if (Test-Path -LiteralPath $releaseNotes) {
    Copy-Item -LiteralPath $releaseNotes -Destination (Join-Path $output "RELEASE_NOTES.md") -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

Compress-Archive -Path (Join-Path $output "*") -DestinationPath $zipPath -Force
$checksum = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$checksum  $(Split-Path -Leaf $zipPath)" -Encoding ASCII

Write-Host "Created release package:"
Write-Host "  $zipPath"
Write-Host "Created SHA-256 checksum:"
Write-Host "  $checksumPath"
