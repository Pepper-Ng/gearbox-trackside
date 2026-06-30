[CmdletBinding()]
param(
    [string]$AppVersion = "0.1.0",
    [string]$BundleVersion = "",
    [string]$MinimumCompatibleVersion = "0.1.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "",
    [switch]$SkipTests,
    [switch]$SkipKioskBuild,
    [switch]$NoArchive
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSCommandPath
$tracksideRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $tracksideRoot '..\..')).Path

if ([string]::IsNullOrWhiteSpace($BundleVersion)) {
    $timestamp = Get-Date -Format 'yyyyMMddHHmmss'
    $commit = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
    if ([string]::IsNullOrWhiteSpace($commit)) {
        $commit = 'local'
    }

    $BundleVersion = "$AppVersion-dev.$timestamp.$commit"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'artifacts\trackside\bundles'
}

$bundleName = "Trackside-$BundleVersion-$Runtime"
$bundleRoot = Join-Path $OutputRoot $bundleName
$archivePath = "$bundleRoot.zip"

function Invoke-Tool {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [Parameter(Mandatory)] [string]$WorkingDirectory
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$FilePath exited with code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Copy-KioskDistToWwwRoot {
    param(
        [Parameter(Mandatory)] [string]$Source,
        [Parameter(Mandatory)] [string]$Destination
    )

    foreach ($generatedDirectory in @('assets', 'brand', 'icons')) {
        $generatedPath = Join-Path $Destination $generatedDirectory
        if (Test-Path $generatedPath) {
            Remove-Item -Recurse -Force $generatedPath
        }
    }

    foreach ($generatedFile in @('favicon.ico', 'index.html')) {
        $generatedPath = Join-Path $Destination $generatedFile
        if (Test-Path $generatedPath) {
            Remove-Item -Force $generatedPath
        }
    }

    New-Item -ItemType Directory -Force $Destination | Out-Null
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

if (!$SkipTests) {
    Invoke-Tool -FilePath dotnet -Arguments @('test', (Join-Path $tracksideRoot 'Trackside.slnx'), '-c', $Configuration) -WorkingDirectory $repoRoot
}

if (!$SkipKioskBuild) {
    $kioskRoot = Join-Path $repoRoot 'web\kiosk'
    if (!(Test-Path (Join-Path $kioskRoot 'node_modules'))) {
        Invoke-Tool -FilePath npm -Arguments @('--prefix', $kioskRoot, 'ci') -WorkingDirectory $repoRoot
    }

    Invoke-Tool -FilePath npm -Arguments @('--prefix', $kioskRoot, 'run', 'build') -WorkingDirectory $repoRoot
}

if (Test-Path $bundleRoot) {
    Remove-Item -Recurse -Force $bundleRoot
}

New-Item -ItemType Directory -Force $bundleRoot | Out-Null

$appRoot = Join-Path $bundleRoot 'app'
$serviceOut = Join-Path $appRoot 'service'
$trayOut = Join-Path $appRoot 'tray'
$rigAgentOut = Join-Path $appRoot 'rig-agent'
$updaterOut = Join-Path $appRoot 'updater'

$publishProperties = @(
    "-p:Version=$AppVersion",
    "-p:InformationalVersion=$BundleVersion",
    "-p:SkipKioskBuild=true"
)

$servicePublishArguments = @('publish', (Join-Path $tracksideRoot 'Trackside.Service\Trackside.Service.csproj'), '-c', $Configuration, '-r', $Runtime, '--self-contained', 'false', '-o', $serviceOut) + $publishProperties
$trayPublishArguments = @('publish', (Join-Path $tracksideRoot 'Trackside.Tray\Trackside.Tray.csproj'), '-c', $Configuration, '-r', $Runtime, '--self-contained', 'false', '-o', $trayOut) + $publishProperties
$rigAgentPublishArguments = @('publish', (Join-Path $tracksideRoot 'Trackside.RigAgent\Trackside.RigAgent.csproj'), '-c', $Configuration, '-r', $Runtime, '--self-contained', 'false', '-o', $rigAgentOut) + $publishProperties
$updaterPublishArguments = @('publish', (Join-Path $tracksideRoot 'Trackside.Updater\Trackside.Updater.csproj'), '-c', $Configuration, '-r', $Runtime, '--self-contained', 'false', '-o', $updaterOut) + $publishProperties
Invoke-Tool -FilePath dotnet -Arguments $servicePublishArguments -WorkingDirectory $repoRoot
Invoke-Tool -FilePath dotnet -Arguments $trayPublishArguments -WorkingDirectory $repoRoot
Invoke-Tool -FilePath dotnet -Arguments $rigAgentPublishArguments -WorkingDirectory $repoRoot
Invoke-Tool -FilePath dotnet -Arguments $updaterPublishArguments -WorkingDirectory $repoRoot

if (!$SkipKioskBuild) {
    Copy-KioskDistToWwwRoot (Join-Path $repoRoot 'web\kiosk\dist') (Join-Path $serviceOut 'wwwroot')
}

$configRoot = Join-Path $bundleRoot 'config'
$serviceConfigRoot = Join-Path $configRoot 'service'
$trayConfigRoot = Join-Path $configRoot 'tray'
New-Item -ItemType Directory -Force $serviceConfigRoot, $trayConfigRoot | Out-Null
Copy-Item (Join-Path $tracksideRoot 'Trackside.Service\appsettings.json') (Join-Path $serviceConfigRoot 'appsettings.json') -Force
Copy-Item (Join-Path $tracksideRoot 'Trackside.Tray\appsettings.json') (Join-Path $trayConfigRoot 'appsettings.json') -Force

New-Item -ItemType Directory -Force (Join-Path $bundleRoot 'data'), (Join-Path $bundleRoot 'logs'), (Join-Path $bundleRoot 'updates\staging'), (Join-Path $bundleRoot 'install') | Out-Null
Set-Content (Join-Path $bundleRoot 'data\.keep') ''
Set-Content (Join-Path $bundleRoot 'logs\.keep') ''
Set-Content (Join-Path $bundleRoot 'updates\staging\.keep') ''

Copy-Item (Join-Path $scriptRoot 'Install-Trackside.ps1') (Join-Path $bundleRoot 'install\Install-Trackside.ps1') -Force
Copy-Item (Join-Path $scriptRoot 'Uninstall-Trackside.ps1') (Join-Path $bundleRoot 'install\Uninstall-Trackside.ps1') -Force
Copy-Item (Join-Path $scriptRoot 'Invoke-TracksideBundleSmoke.ps1') (Join-Path $bundleRoot 'install\Invoke-TracksideBundleSmoke.ps1') -Force

@"
# Trackside Bundle Layout

- app/service: Trackside.Service executable and packaged web assets.
- app/tray: Trackside.Tray notification-area companion.
- app/rig-agent: future rig-side worker executable.
- app/updater: out-of-process manifest verification/update boundary.
- config/service: editable service configuration.
- config/tray: editable tray configuration.
- data: durable runtime data such as SQLite files.
- logs: runtime logs and diagnostics output.
- updates/staging: candidate update bundles and manifests.
- install: install, uninstall, and bundle smoke-test scripts.
"@ | Set-Content (Join-Path $bundleRoot 'INSTALL_LAYOUT.md') -Encoding UTF8

$manifestPath = Join-Path $bundleRoot 'manifest.json'
$files = Get-ChildItem $bundleRoot -Recurse -File |
    Where-Object { $_.FullName -ne $manifestPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = [System.IO.Path]::GetRelativePath($bundleRoot, $_.FullName).Replace('\', '/')
        $hash = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant()
        [pscustomobject]@{
            path = $relativePath
            sha256 = $hash
            bytes = $_.Length
        }
    }

$manifest = [ordered]@{
    schemaVersion = 1
    appId = 'Gearbox.Trackside'
    appVersion = $AppVersion
    bundleVersion = $BundleVersion
    minimumCompatibleVersion = $MinimumCompatibleVersion
    runtime = $Runtime
    createdUtc = (Get-Date).ToUniversalTime().ToString('O')
    installLayout = [ordered]@{
        appPath = 'app'
        configPath = 'config'
        dataPath = 'data'
        logsPath = 'logs'
        updatesPath = 'updates'
    }
    entryPoints = [ordered]@{
        service = 'app/service/Trackside.Service.exe'
        tray = 'app/tray/Trackside.Tray.exe'
        rigAgent = 'app/rig-agent/Trackside.RigAgent.exe'
        updater = 'app/updater/Trackside.Updater.exe'
    }
    files = $files
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content $manifestPath -Encoding UTF8

if (!$NoArchive) {
    if (Test-Path $archivePath) {
        Remove-Item -Force $archivePath
    }

    Compress-Archive -Path (Join-Path $bundleRoot '*') -DestinationPath $archivePath -Force
}

[pscustomobject]@{
    BundleRoot = $bundleRoot
    ManifestPath = $manifestPath
    ArchivePath = if ($NoArchive) { $null } else { $archivePath }
    BundleVersion = $BundleVersion
} | ConvertTo-Json