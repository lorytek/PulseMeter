$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $root "artifacts"
$project = Join-Path $root "src\PulseMeter\PulseMeter.csproj"
$output = Join-Path $artifactsRoot "PulseMeter-win-x64"
$exe = Join-Path $output "PulseMeter.exe"
$launcher = Join-Path $output "launch-pulsemeter.vbs"
$shortcutPath = Join-Path $root "PulseMeter.lnk"
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "PulseMeter.lnk"
$wscript = Join-Path $env:WINDIR "System32\wscript.exe"

function Escape-VbScriptString([string]$value) {
    return $value.Replace('"', '""')
}

function Save-PulseMeterShortcut([string]$path) {
    $shortcut = $script:shell.CreateShortcut($path)
    $shortcut.TargetPath = $wscript
    $shortcut.Arguments = "`"$launcher`""
    $shortcut.WorkingDirectory = $output
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Save()
}

$resolvedArtifactsRoot = [System.IO.Path]::GetFullPath($artifactsRoot)
$resolvedOutput = [System.IO.Path]::GetFullPath($output)
$artifactPrefix = $resolvedArtifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedOutput.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean unexpected publish output path: $resolvedOutput"
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

New-Item -ItemType Directory -Path $output -Force | Out-Null

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $output `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:UseAppHost=true `
    /p:DebugType=embedded `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $exe)) {
    throw "Published executable was not created: $exe"
}

$sidecarDll = Join-Path $output "PulseMeter.dll"
if (Test-Path -LiteralPath $sidecarDll) {
    throw "Single-file publish should not create a sidecar app dll: $sidecarDll"
}

if (-not (Test-Path -LiteralPath $wscript)) {
    throw "Windows Script Host was not found: $wscript"
}

$escapedOutput = Escape-VbScriptString $output
$escapedExe = Escape-VbScriptString $exe
$launcherContent = @"
Set shell = CreateObject("WScript.Shell")
shell.CurrentDirectory = "$escapedOutput"
commandLine = """" & "$escapedExe" & """"
shell.Run commandLine, 0, False
"@
Set-Content -LiteralPath $launcher -Value $launcherContent -Encoding ASCII

$shell = New-Object -ComObject WScript.Shell
Save-PulseMeterShortcut $shortcutPath
Save-PulseMeterShortcut $desktopShortcutPath

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

Write-Host "Published single-file executable:"
Write-Host "  $exe"
Write-Host "Created hidden launcher:"
Write-Host "  $launcher"
Write-Host "Updated shortcut:"
Write-Host "  $shortcutPath"
Write-Host "Updated desktop shortcut:"
Write-Host "  $desktopShortcutPath"
Write-Host "Shortcut target:"
Write-Host "  $wscript `"$launcher`""
