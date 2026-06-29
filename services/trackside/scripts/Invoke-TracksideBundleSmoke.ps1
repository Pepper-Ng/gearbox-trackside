[CmdletBinding()]
param(
    [string]$BundleRoot = "",
    [int]$Port = 18877,
    [int]$TimeoutSeconds = 20,
    [switch]$SkipRuntimeSmoke
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
    $BundleRoot = Resolve-Path (Join-Path (Split-Path -Parent $PSCommandPath) '..')
}

$BundleRoot = (Resolve-Path $BundleRoot).Path
$manifestPath = Join-Path $BundleRoot 'manifest.json'
$updaterExe = Join-Path $BundleRoot 'app\updater\Trackside.Updater.exe'
$serviceExe = Join-Path $BundleRoot 'app\service\Trackside.Service.exe'
$trayExe = Join-Path $BundleRoot 'app\tray\Trackside.Tray.exe'
$rigAgentExe = Join-Path $BundleRoot 'app\rig-agent\Trackside.RigAgent.exe'

foreach ($requiredPath in @($manifestPath, $updaterExe, $serviceExe, $trayExe, $rigAgentExe)) {
    if (!(Test-Path $requiredPath)) {
        throw "Required bundle file missing: $requiredPath"
    }
}

& $updaterExe verify --manifest $manifestPath --root $BundleRoot
if ($LASTEXITCODE -ne 0) {
    throw "Updater verification failed with exit code $LASTEXITCODE."
}

if ($SkipRuntimeSmoke) {
    Write-Host 'Bundle manifest smoke passed. Runtime smoke skipped.'
    return
}

$baseUrl = "http://127.0.0.1:$Port"
$configRoot = Join-Path $BundleRoot 'config'
$dataPath = Join-Path $BundleRoot 'data'
$logsPath = Join-Path $BundleRoot 'logs'
$updatesPath = Join-Path $BundleRoot 'updates'
$arguments = @(
    '--console',
    '--source', 'fixture',
    '--fixture', 'Fixtures/mock-live-session.json',
    '--listen-url', $baseUrl,
    '--public-base-url', $baseUrl,
    '--config-root', $configRoot,
    '--install-mode', 'BundleSmoke',
    '--install-root', $BundleRoot,
    '--data-path', $dataPath,
    '--logs-path', $logsPath,
    '--updates-path', $updatesPath,
    '--bundle-version', (Get-Content $manifestPath -Raw | ConvertFrom-Json).bundleVersion,
    '--manifest-path', $manifestPath
)

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $serviceExe
$startInfo.WorkingDirectory = Split-Path -Parent $serviceExe
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
foreach ($argument in $arguments) {
    [void]$startInfo.ArgumentList.Add($argument)
}

$process = [System.Diagnostics.Process]::Start($startInfo)
if ($null -eq $process) {
    throw 'Failed to start Trackside.Service from the bundle.'
}

$smokeFailure = $null
try {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $health = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            throw "Trackside.Service exited early with code $($process.ExitCode)."
        }

        try {
            $health = Invoke-RestMethod -Uri "$baseUrl/api/health" -TimeoutSec 2
            break
        }
        catch {
            [Threading.Thread]::Sleep(500)
        }
    }

    if ($null -eq $health) {
        throw "Timed out waiting for '$baseUrl/api/health'."
    }

    if ($health.installMode -ne 'BundleSmoke') {
        throw "Expected health installMode BundleSmoke, received '$($health.installMode)'."
    }

    if ($health.serviceState -ne 'Console') {
        throw "Expected health serviceState Console, received '$($health.serviceState)'."
    }

    $kioskPage = Invoke-WebRequest -Uri "$baseUrl/" -UseBasicParsing -TimeoutSec 5
    if ($kioskPage.Content -notmatch '<div id="root"') {
        throw 'Kiosk page did not include the React root element.'
    }

    $scriptMatch = [regex]::Match($kioskPage.Content, 'src="(?<path>/assets/[^"]+\.js)"')
    if (!$scriptMatch.Success) {
        throw 'Kiosk page did not reference a built JavaScript asset.'
    }

    $stylesheetMatch = [regex]::Match($kioskPage.Content, 'href="(?<path>/assets/[^"]+\.css)"')
    if (!$stylesheetMatch.Success) {
        throw 'Kiosk page did not reference a built stylesheet asset.'
    }

    [void](Invoke-WebRequest -Uri "$baseUrl$($scriptMatch.Groups['path'].Value)" -UseBasicParsing -TimeoutSec 5)
    [void](Invoke-WebRequest -Uri "$baseUrl$($stylesheetMatch.Groups['path'].Value)" -UseBasicParsing -TimeoutSec 5)

    $adminPage = Invoke-WebRequest -Uri "$baseUrl/configuration.html" -UseBasicParsing -TimeoutSec 5
    if ($adminPage.Content -notmatch 'Trackside Admin' -or $adminPage.Content -notmatch 'configuration\.js') {
        throw 'Admin configuration page did not include the expected shell and script reference.'
    }

    $snapshot = Invoke-RestMethod -Uri "$baseUrl/api/live-session/current" -TimeoutSec 5
    if ([string]::IsNullOrWhiteSpace($snapshot.session.trackName)) {
        throw 'Fixture snapshot did not include a track name.'
    }

    $trackGeometry = Invoke-RestMethod -Uri "$baseUrl/api/track-geometry/current" -TimeoutSec 5
    if ($null -eq $trackGeometry.isAvailable -or $null -eq $trackGeometry.coveragePercent -or $null -eq $trackGeometry.isCompleteLap) {
        throw 'Track geometry endpoint did not return geometry quality metadata.'
    }

    Write-Host "Bundle runtime smoke passed at $baseUrl using track '$($snapshot.session.trackName)'."
}
catch {
    $smokeFailure = $_
}

if (!$process.HasExited) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    [void]$process.WaitForExit(5000)
}

if ($null -ne $smokeFailure) {
    throw $smokeFailure
}