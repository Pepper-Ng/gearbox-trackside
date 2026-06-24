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
    [switch]$SkipTrayAutostart,
    [switch]$SkipAdminBootstrap,
    [string]$AdminUsername = "",
    [securestring]$AdminPassword
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

$updaterExe = Join-Path $BundleRoot 'app\updater\Trackside.Updater.exe'
if (!(Test-Path $updaterExe)) {
    throw "Updater executable not found at '$updaterExe'."
}

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

function ConvertTo-PlainText {
    param([securestring]$Value)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function New-AdminPasswordHash {
    param([string]$Password)

    $salt = [byte[]]::new(32)
    [Security.Cryptography.RandomNumberGenerator]::Fill($salt)
    $derive = [Security.Cryptography.Rfc2898DeriveBytes]::new(
        $Password,
        $salt,
        210000,
        [Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        $hash = $derive.GetBytes(32)
        return [pscustomobject]@{
            algorithm = 'PBKDF2-HMACSHA256'
            iterations = 210000
            salt = [Convert]::ToBase64String($salt)
            hash = [Convert]::ToBase64String($hash)
        }
    }
    finally {
        $derive.Dispose()
    }
}

function Initialize-AdminStore {
    param(
        [string]$StoreRoot,
        [string]$Username,
        [securestring]$Password,
        [switch]$Skip
    )

    if ($Skip) {
        Write-Step 'Skipping first admin bootstrap.'
        return
    }

    $securityPath = Join-Path $StoreRoot 'security'
    $storePath = Join-Path $securityPath 'admin-users.json'
    if (Test-Path $storePath) {
        Write-Step "Preserving existing admin user store at '$storePath'."
        return
    }

    if ($DryRun) {
        Write-Step "Would create first admin user store at '$storePath'."
        return
    }

    if ([string]::IsNullOrWhiteSpace($Username)) {
        $Username = Read-Host 'Trackside first admin username'
    }

    if ([string]::IsNullOrWhiteSpace($Username) -or $Username.Length -lt 3 -or $Username -match '\s') {
        throw 'Admin username must be at least 3 characters and cannot contain whitespace.'
    }

    if ($null -eq $Password) {
        $Password = Read-Host 'Trackside first admin password' -AsSecureString
    }

    $plainPassword = ConvertTo-PlainText $Password
    try {
        if ($plainPassword.Length -lt 12) {
            throw 'Admin password must be at least 12 characters.'
        }

        $passwordHash = New-AdminPasswordHash $plainPassword
    }
    finally {
        $plainPassword = $null
    }

    $now = [DateTimeOffset]::UtcNow.ToString('o')
    $document = [ordered]@{
        schemaVersion = 1
        users = @(
            [ordered]@{
                username = $Username.Trim()
                displayName = $Username.Trim()
                passwordHashAlgorithm = $passwordHash.algorithm
                passwordHashIterations = $passwordHash.iterations
                passwordSalt = $passwordHash.salt
                passwordHash = $passwordHash.hash
                createdUtc = $now
                updatedUtc = $now
            }
        )
    }

    New-Item -ItemType Directory -Force $securityPath | Out-Null
    & icacls.exe $securityPath /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to secure admin store directory '$securityPath'."
    }

    $document | ConvertTo-Json -Depth 5 | Set-Content -Path $storePath -Encoding UTF8
    Write-Step "Created first admin user '$($Username.Trim())' at '$storePath'."
}

Write-Host "Verifying Trackside bundle manifest at '$manifestPath'."
& $updaterExe verify --manifest $manifestPath --root $BundleRoot
if ($LASTEXITCODE -ne 0) {
    throw "Bundle manifest verification failed with exit code $LASTEXITCODE."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

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

Initialize-AdminStore -StoreRoot $dataPath -Username $AdminUsername -Password $AdminPassword -Skip:$SkipAdminBootstrap

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