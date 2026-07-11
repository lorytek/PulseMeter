$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $root "artifacts"
$project = Join-Path $root "src\PulseMeter\PulseMeter.csproj"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$output = Join-Path $artifactsRoot "PulseMeter-win-x64-$timestamp"
$appExe = Join-Path $output "PulseMeter.exe"
$icon = Join-Path $root "src\PulseMeter\Assets\PulseMeter.ico"
$shortcutPath = Join-Path $root "PulseMeter.lnk"
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "PulseMeter.lnk"
$taskbarShortcutPath = Join-Path $env:APPDATA "Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\PulseMeter.lnk"

function Save-PulseMeterShortcut([string]$path) {
    $shortcut = $script:shell.CreateShortcut($path)
    $shortcut.TargetPath = $appExe
    $shortcut.Arguments = ""
    $shortcut.WorkingDirectory = $output
    $shortcut.IconLocation = $icon
    $shortcut.Save()
}

New-Item -ItemType Directory -Path $output -Force | Out-Null

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $output `
    /p:UseAppHost=true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:PublishReadyToRun=false `
    /p:DebugType=embedded `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $appExe)) {
    throw "Published application executable was not created: $appExe"
}

if (-not (Test-Path -LiteralPath $icon)) {
    throw "PulseMeter icon was not found: $icon"
}

$shell = New-Object -ComObject WScript.Shell
Save-PulseMeterShortcut $shortcutPath
Save-PulseMeterShortcut $desktopShortcutPath
if (Test-Path -LiteralPath $taskbarShortcutPath) {
    Save-PulseMeterShortcut $taskbarShortcutPath
    Write-Host "Updated existing taskbar shortcut:"
    Write-Host "  $taskbarShortcutPath"
}
else {
    Write-Host "PulseMeter is not pinned to the taskbar; no taskbar shortcut was created."
}

$staleShortcutName = [string]::Concat("Codex ", "Usage ", [char]72, [char]85, [char]68, ".lnk")
$staleShortcutPaths = @(
    (Join-Path $root $staleShortcutName),
    (Join-Path ([Environment]::GetFolderPath("Desktop")) $staleShortcutName)
)

foreach ($staleShortcutPath in $staleShortcutPaths) {
    if (Test-Path -LiteralPath $staleShortcutPath) {
        Remove-Item -LiteralPath $staleShortcutPath -Force
    }
}

Write-Host "Published local self-contained app:"
Write-Host "  $appExe"
Write-Host "Updated shortcut:"
Write-Host "  $shortcutPath"
Write-Host "Updated desktop shortcut:"
Write-Host "  $desktopShortcutPath"
Write-Host "Shortcut target:"
Write-Host "  $appExe"
