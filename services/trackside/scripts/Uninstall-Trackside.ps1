[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InstallRoot = "$env:ProgramFiles\Gearbox Trackside",
    [string]$ServiceName = "Trackside",
    [string]$TrayRunName = "Trackside Tray",
    [switch]$DryRun,
    [switch]$RemoveFiles,
    [switch]$RemoveData
)

$ErrorActionPreference = 'Stop'
$installRootFull = [System.IO.Path]::GetFullPath($InstallRoot)

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

if (!$DryRun -and !(Test-IsAdministrator)) {
    throw 'Uninstalling the Windows Service requires an elevated PowerShell session. Re-run with -DryRun to preview without elevation.'
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($DryRun) {
        Write-Step "Stop and delete service '$ServiceName'."
    }
    else {
        if ($service.Status -ne 'Stopped') {
            Stop-Service -Name $ServiceName -Force -ErrorAction Stop
        }

        & sc.exe delete $ServiceName | Out-Host
    }
}
else {
    Write-Step "Service '$ServiceName' is not installed."
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($DryRun) {
    Write-Step "Remove tray auto-start '$TrayRunName'."
}
else {
    Remove-ItemProperty -Path $runKey -Name $TrayRunName -ErrorAction SilentlyContinue
}

if ($RemoveFiles) {
    foreach ($relativePath in @('app', 'config', 'manifest.json', 'INSTALL_LAYOUT.md')) {
        $path = Join-Path $installRootFull $relativePath
        if ($DryRun) {
            Write-Step "Remove '$path'."
        }
        elseif (Test-Path $path) {
            Remove-Item -Recurse -Force $path
        }
    }
}

if ($RemoveData) {
    foreach ($relativePath in @('data', 'logs', 'updates')) {
        $path = Join-Path $installRootFull $relativePath
        if ($DryRun) {
            Write-Step "Remove '$path'."
        }
        elseif (Test-Path $path) {
            Remove-Item -Recurse -Force $path
        }
    }
}

Write-Step 'Uninstall complete.'