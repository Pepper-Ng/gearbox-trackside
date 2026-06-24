[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$BundleRoot = "",
    [string]$InstallRoot = "$env:ProgramFiles\Gearbox Trackside",
    [string]$ServiceName = "Trackside",
    [string]$ServiceDisplayName = "Trackside Service",
    [string]$TrayRunName = "Trackside Tray",
    [switch]$DryRun,
    [switch]$OverwriteConfig,
    [switch]$SkipService,
    [switch]$SkipTrayAutostart
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
    $BundleRoot = Resolve-Path (Join-Path (Split-Path -Parent $PSCommandPath) '..')
}

$BundleRoot = (Resolve-Path $BundleRoot).Path
$manifestPath = Join-Path $BundleRoot 'manifest.json'
if (!(Test-Path $manifestPath)) {
    throw "Bundle manifest not found at '$manifestPath'."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$installRootFull = [System.IO.Path]::GetFullPath($InstallRoot)
$configPath = Join-Path $installRootFull 'config'
$dataPath = Join-Path $installRootFull 'data'
$logsPath = Join-Path $installRootFull 'logs'
$updatesPath = Join-Path $installRootFull 'updates'
$serviceExe = Join-Path $installRootFull 'app\service\Trackside.Service.exe'
$trayExe = Join-Path $installRootFull 'app\tray\Trackside.Tray.exe'
$installedManifestPath = Join-Path $installRootFull 'manifest.json'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-Step {
    param([string]$Message)
    if ($DryRun) {
        Write-Host "DRY-RUN: $Message"
    }
    else {
        Write-Host $Message
    }
}

function Quote-CommandArgument {
    param([string]$Value)
    '"' + $Value.Replace('"', '\"') + '"'
}

function Copy-DirectoryContent {
    param(
        [string]$Source,
        [string]$Destination,
        [switch]$PreserveExisting
    )

    if ($DryRun) {
        Write-Step "Copy '$Source' to '$Destination'."
        return
    }

    if ($PreserveExisting -and (Test-Path $Destination)) {
        Write-Step "Preserving existing '$Destination'."
        return
    }

    if (Test-Path $Destination) {
        Remove-Item -Recurse -Force $Destination
    }

    New-Item -ItemType Directory -Force $Destination | Out-Null
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

if (!$DryRun -and !$SkipService -and !(Test-IsAdministrator)) {
    throw 'Installing the Windows Service requires an elevated PowerShell session. Re-run with -DryRun to preview without elevation.'
}

Write-Step "Installing Trackside bundle $($manifest.bundleVersion) to '$installRootFull'."

Copy-DirectoryContent (Join-Path $BundleRoot 'app') (Join-Path $installRootFull 'app')
Copy-DirectoryContent (Join-Path $BundleRoot 'config') $configPath -PreserveExisting:(!$OverwriteConfig)

foreach ($directory in @($dataPath, $logsPath, (Join-Path $updatesPath 'staging'))) {
    if ($DryRun) {
        Write-Step "Ensure directory '$directory'."
    }
    else {
        New-Item -ItemType Directory -Force $directory | Out-Null
    }
}

if ($DryRun) {
    Write-Step "Copy manifest to '$installedManifestPath'."
}
else {
    Copy-Item $manifestPath $installedManifestPath -Force
}

if (!$SkipService) {
    $serviceArguments = @(
        '--config-root', $configPath,
        '--install-mode', 'Service',
        '--install-root', $installRootFull,
        '--data-path', $dataPath,
        '--logs-path', $logsPath,
        '--updates-path', $updatesPath,
        '--bundle-version', $manifest.bundleVersion,
        '--manifest-path', $installedManifestPath,
        '--service-name', $ServiceName
    )
    $binaryPath = (Quote-CommandArgument $serviceExe) + ' ' + (($serviceArguments | ForEach-Object { Quote-CommandArgument $_ }) -join ' ')

    if ($DryRun) {
        Write-Step "Install or update service '$ServiceName' with binary path: $binaryPath"
    }
    else {
        $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($existingService) {
            if ($existingService.Status -ne 'Stopped') {
                Stop-Service -Name $ServiceName -Force -ErrorAction Stop
            }

            & sc.exe config $ServiceName binPath= $binaryPath DisplayName= $ServiceDisplayName start= auto | Out-Host
        }
        else {
            New-Service -Name $ServiceName -BinaryPathName $binaryPath -DisplayName $ServiceDisplayName -StartupType Automatic | Out-Null
        }

        & sc.exe failure $ServiceName reset= 60 actions= restart/5000/restart/5000/""/60000 | Out-Host
    }
}

if (!$SkipTrayAutostart) {
    $trayArguments = @('--config-root', $configPath)
    $trayCommand = (Quote-CommandArgument $trayExe) + ' ' + (($trayArguments | ForEach-Object { Quote-CommandArgument $_ }) -join ' ')
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

    if ($DryRun) {
        Write-Step "Set tray auto-start '$TrayRunName' to: $trayCommand"
    }
    else {
        New-Item -Path $runKey -Force | Out-Null
        Set-ItemProperty -Path $runKey -Name $TrayRunName -Value $trayCommand
    }
}

Write-Step "Install complete. Start the service with: Start-Service $ServiceName"