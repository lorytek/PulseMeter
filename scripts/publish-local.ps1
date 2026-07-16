$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $root "artifacts"
$project = Join-Path $root "src\PulseMeter\PulseMeter.csproj"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$output = Join-Path $artifactsRoot "PulseMeter-win-x64-$timestamp"
$appExe = Join-Path $output "PulseMeter.exe"
$localHostOutput = Join-Path $artifactsRoot "PulseMeter-local-host-$timestamp"
$localHostDll = Join-Path $localHostOutput "PulseMeter.dll"
$dotnetExe = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
$icon = Join-Path $root "src\PulseMeter\Assets\PulseMeter.ico"
$shortcutPath = Join-Path $root "PulseMeter.lnk"
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "PulseMeter.lnk"
$taskbarShortcutPath = Join-Path $env:APPDATA "Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\PulseMeter.lnk"
$launcherTarget = $appExe
$launcherArguments = ""
$launcherWorkingDirectory = $output
$launcherDescription = "self-contained executable"

function Save-PulseMeterShortcut([string]$path) {
    $shortcut = $script:shell.CreateShortcut($path)
    $shortcut.TargetPath = $launcherTarget
    $shortcut.Arguments = $launcherArguments
    $shortcut.WorkingDirectory = $launcherWorkingDirectory
    $shortcut.IconLocation = $icon
    $shortcut.Save()
}

function Stop-WorkspacePulseMeterInstances {
    $localLauncherPattern = [string]::Concat('*', (Join-Path $artifactsRoot 'PulseMeter-local-host-*\PulseMeter.dll'), '*')
    $publishedExePattern = Join-Path $artifactsRoot 'PulseMeter-win-x64-*\PulseMeter.exe'
    $processes = Get-CimInstance Win32_Process | Where-Object {
        ($_.Name -eq "dotnet.exe" -and $_.CommandLine -like $localLauncherPattern) -or
        ($_.Name -eq "PulseMeter.exe" -and $_.ExecutablePath -like $publishedExePattern)
    }

    foreach ($process in $processes) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }

    if ($processes) {
        Start-Sleep -Milliseconds 500
    }
}

function Test-PulseMeterLaunch(
    [string]$target,
    [string]$arguments,
    [string]$workingDirectory) {
    $probeId = [Guid]::NewGuid().ToString("N")
    $stdoutPath = Join-Path $env:TEMP "PulseMeter-launch-$probeId.out"
    $stderrPath = Join-Path $env:TEMP "PulseMeter-launch-$probeId.err"
    $process = $null

    try {
        Stop-WorkspacePulseMeterInstances
        $startParameters = @{
            FilePath = $target
            WorkingDirectory = $workingDirectory
            WindowStyle = "Hidden"
            RedirectStandardOutput = $stdoutPath
            RedirectStandardError = $stderrPath
            PassThru = $true
        }
        if (-not [string]::IsNullOrWhiteSpace($arguments)) {
            $startParameters.ArgumentList = $arguments
        }

        $process = Start-Process @startParameters

        if (-not $process.WaitForExit(12000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit()
            return
        }

        $outputText = @(
            if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw }
            if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw }
        ) -join [Environment]::NewLine
        $detail = if ([string]::IsNullOrWhiteSpace($outputText)) {
            "The process exited before the launch probe completed."
        }
        else {
            $outputText.Trim()
        }

        throw "Published PulseMeter launcher '$target' failed its launch probe.`n$detail"
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }

        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
New-Item -ItemType Directory -Path $localHostOutput -Force | Out-Null

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

if (-not (Test-Path -LiteralPath $dotnetExe)) {
    throw "The local .NET host was not found: $dotnetExe"
}

dotnet publish $project `
    -c Release `
    --self-contained false `
    -o $localHostOutput `
    /p:UseAppHost=false `
    /p:PublishSingleFile=false `
    /p:DebugType=embedded `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "Local host publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $localHostDll)) {
    throw "Published local host DLL was not created: $localHostDll"
}

if (-not (Test-Path -LiteralPath $icon)) {
    throw "PulseMeter icon was not found: $icon"
}

Write-Host "Checking that Windows permits the new local build to launch..."
try {
    Test-PulseMeterLaunch $appExe "" $output
}
catch {
    $selfContainedFailure = $_
    Write-Warning "The self-contained executable was blocked. Trying the framework-dependent launcher."
    try {
        Test-PulseMeterLaunch `
            $dotnetExe `
            ([string]::Concat('"', $localHostDll, '"')) `
            $localHostOutput
        $launcherTarget = $dotnetExe
        $launcherArguments = [string]::Concat('"', $localHostDll, '"')
        $launcherWorkingDirectory = $localHostOutput
        $launcherDescription = "framework-dependent launcher"
    }
    catch {
        if (Test-Path -LiteralPath $desktopShortcutPath) {
            Start-Process -FilePath $desktopShortcutPath
        }

        throw "Both published PulseMeter launchers were blocked. Existing shortcuts were not changed.`nSelf-contained: $selfContainedFailure`nFramework-dependent: $_"
    }
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
Write-Host "Published local framework-dependent launcher:"
Write-Host "  $dotnetExe $localHostDll"
Write-Host "Updated shortcut:"
Write-Host "  $shortcutPath"
Write-Host "Updated desktop shortcut:"
Write-Host "  $desktopShortcutPath"
Write-Host "Shortcut target:"
Write-Host "  $launcherTarget ($launcherDescription)"

Start-Process -FilePath $desktopShortcutPath
Write-Host "Relaunched PulseMeter from the verified desktop shortcut."
