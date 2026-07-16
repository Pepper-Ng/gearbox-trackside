<#
.SYNOPSIS
Collects rFactor 2 Dedicated Server and Windows crash evidence without changing the host configuration.

.DESCRIPTION
Designed for stock Windows 10 Windows PowerShell 5.1. The script does not require
.NET Core, Python, third-party modules, Sysinternals, or an installation step.

The collector only reads host state and source evidence. Its only writes are to a
new timestamped output directory beneath OutputBase, which defaults to the folder
containing this script (normally the USB stick). It does not modify the registry,
event logs, services, rFactor 2 files, plugin configuration, dump configuration,
firewall, drivers, or Windows settings. It never starts or stops rFactor 2.

Every collection section is isolated. Access denied, a missing WMI class, a missing
folder, or a stopped process becomes a warning while the remaining sections continue.

.PARAMETER Rf2Root
Optional full path to the rFactor 2 Dedicated Server installation. Auto-discovery
also checks a running process, standard Steam locations, and known Steam libraries.

.PARAMETER Days
How many days of event and reliability history to inspect. Default: 30.

.PARAMETER CopySmallDumps
Also copy dump files no larger than 64 MiB each, up to a combined 256 MiB. Without
this switch, dumps are inventoried but are not copied. Text traces and small WER
reports are copied regardless.

.PARAMETER OutputBase
Optional existing directory below which the timestamped evidence folder is created.
When omitted, output is written beside this script.

.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Collect-Rf2CrashEvidence.ps1 -Rf2Root "C:\Racing\rFactor2-Dedicated"

.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Collect-Rf2CrashEvidence.ps1 -Rf2Root "C:\Racing\rFactor2-Dedicated" -CopySmallDumps
#>
[CmdletBinding()]
param(
    [string]$Rf2Root = "",
    [ValidateRange(1, 365)]
    [int]$Days = 30,
    [switch]$CopySmallDumps,
    [string]$OutputBase = ""
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:CollectorVersion = '1.0'
$script:ExpectedPluginHash = '9D98D77B767812DCA5AFEB6663F486B3AD5BE090C3E10783F36BC73A470AB5E6'
$script:Utf8Bom = New-Object System.Text.UTF8Encoding($true)
$script:Warnings = New-Object System.Collections.ArrayList
$script:CompletedSteps = New-Object System.Collections.ArrayList
$script:DetectedRoots = New-Object System.Collections.ArrayList
$script:ServerProcesses = @()
$script:PluginFindings = New-Object System.Collections.ArrayList
$script:ConfigurationFindings = New-Object System.Collections.ArrayList
$script:DumpFiles = New-Object System.Collections.ArrayList
$script:ExistingDumpCount = 0
$script:FreeSpacePreflightWarningIssued = $false
$script:CopiedBytes = [int64]0
$script:MaximumTextCopyBytes = [int64](64MB)
$script:MaximumDumpCopyBytes = [int64](64MB)
$script:MaximumTotalTextCopyBytes = [int64](256MB)
$script:MaximumTotalDumpCopyBytes = [int64](256MB)
$script:CopiedDumpBytes = [int64]0
$script:Since = (Get-Date).AddDays(-1 * $Days)

function Write-HostSafe {
    param([string]$Message, [ConsoleColor]$Color = [ConsoleColor]::Gray)

    try {
        Write-Host $Message -ForegroundColor $Color
    }
    catch {
        Write-Output $Message
    }
}

function Write-Utf8Text {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowEmptyString()][string]$Text
    )

    [System.IO.File]::WriteAllText($Path, $Text, $script:Utf8Bom)
}

function Add-Utf8Line {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowEmptyString()][string]$Text
    )

    [System.IO.File]::AppendAllText($Path, $Text + [Environment]::NewLine, $script:Utf8Bom)
}

function Add-CollectorWarning {
    param(
        [Parameter(Mandatory = $true)][string]$Area,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $cleanMessage = ($Message -replace '[\r\n]+', ' ').Trim()
    $line = '[{0}] {1}: {2}' -f (Get-Date).ToString('s'), $Area, $cleanMessage
    [void]$script:Warnings.Add($line)
    Write-HostSafe ('  WARNING: ' + $cleanMessage) Yellow

    try {
        Add-Utf8Line -Path $script:WarningPath -Text $line
    }
    catch {
        Write-HostSafe '  WARNING: Could not append to WARNINGS.txt on the output drive.' Yellow
    }
}

function Write-CollectorLog {
    param([string]$Message)

    try {
        Add-Utf8Line -Path $script:LogPath -Text ('[{0}] {1}' -f (Get-Date).ToString('s'), $Message)
    }
    catch {
        Write-HostSafe ('  LOGGING FAILURE: ' + $_.Exception.Message) Yellow
    }
}

function Invoke-CollectionStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-HostSafe ('Collecting: ' + $Name) Cyan
    Write-CollectorLog ('START ' + $Name)
    try {
        & $Action
        [void]$script:CompletedSteps.Add($Name)
        Write-CollectorLog ('DONE  ' + $Name)
    }
    catch {
        Add-CollectorWarning -Area $Name -Message $_.Exception.Message
        Write-CollectorLog ('FAIL  ' + $Name + ': ' + $_.Exception.ToString())
    }
}

function New-OutputSubdirectory {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $path = Join-Path $script:OutputRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $path -Force)
    }
    return $path
}

function Export-SafeCsv {
    param(
        [AllowNull()][object[]]$InputData,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rows = @($InputData | Where-Object { $null -ne $_ })
    if ($rows.Count -eq 0) {
        Write-Utf8Text -Path $Path -Text '(no matching records)'
        return
    }

    $rows | Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
}

function Export-ObjectText {
    param(
        [AllowNull()][object]$InputData,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if ($null -eq $InputData) {
        Write-Utf8Text -Path $Path -Text '(not available)'
        return
    }

    $text = $InputData | Format-List * | Out-String -Width 4096
    Write-Utf8Text -Path $Path -Text $text
}

function Get-PropertyValue {
    param(
        [AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        [AllowNull()][object]$DefaultValue = $null
    )

    if ($null -eq $InputObject) { return $DefaultValue }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) { return $DefaultValue }
    return $property.Value
}

function Protect-CommandLine {
    param([AllowNull()][string]$CommandLine)

    if ([string]::IsNullOrWhiteSpace($CommandLine)) { return $CommandLine }

    $protected = $CommandLine
    $secretNames = 'password|passwd|pwd|token|apikey|api-key|secret|clientsecret|client-secret|connectionstring|connection-string'
    $argumentPattern = '(?i)(--?(?:{0})|/(?:{0}))(?<separator>\s+|=|:)(?<value>"[^"]*"|''[^'']*''|\S+)' -f $secretNames
    $protected = [regex]::Replace(
        $protected,
        $argumentPattern,
        '${1}${separator}<redacted>')
    $protected = [regex]::Replace($protected, '(?i)([?&](?:token|access_token|api[_-]?key|secret|password)=)[^&\s]+', '${1}<redacted>')
    $protected = [regex]::Replace($protected, '(?i)(https?://[^:/\s]+:)[^@/\s]+@', '${1}<redacted>@')
    return $protected
}

function Test-OutputFreeSpace {
    param([Parameter(Mandatory = $true)][int64]$AdditionalBytes)

    try {
        $driveRoot = [System.IO.Path]::GetPathRoot($script:OutputRoot)
        if ([string]::IsNullOrWhiteSpace($driveRoot)) { return $true }
        $driveInfo = New-Object System.IO.DriveInfo -ArgumentList $driveRoot
        if (-not $driveInfo.IsReady) { return $false }
        return (($driveInfo.AvailableFreeSpace - $AdditionalBytes) -ge 25MB)
    }
    catch {
        # UNC paths and unusual mount points may not expose DriveInfo. The actual write remains exception guarded.
        if (-not $script:FreeSpacePreflightWarningIssued) {
            $script:FreeSpacePreflightWarningIssued = $true
            Add-CollectorWarning -Area 'Output free-space preflight' -Message ('Could not query free space; guarded writes will continue: ' + $_.Exception.Message)
        }
        return $true
    }
}

function Get-ManagementInstances {
    param([Parameter(Mandatory = $true)][string]$ClassName)

    if (Get-Command Get-CimInstance -ErrorAction SilentlyContinue) {
        return @(Get-CimInstance -ClassName $ClassName -ErrorAction Stop)
    }

    return @(Get-WmiObject -Class $ClassName -ErrorAction Stop)
}

function Save-ManagementClass {
    param(
        [Parameter(Mandatory = $true)][string]$ClassName,
        [Parameter(Mandatory = $true)][string[]]$Properties,
        [Parameter(Mandatory = $true)][string]$OutputFile,
        [scriptblock]$Filter
    )

    try {
        $rows = @(Get-ManagementInstances -ClassName $ClassName)
        if ($null -ne $Filter) {
            $rows = @($rows | Where-Object $Filter)
        }
        $selected = @($rows | Select-Object -Property $Properties)
        Export-SafeCsv -InputData $selected -Path $OutputFile
    }
    catch {
        Add-CollectorWarning -Area $ClassName -Message $_.Exception.Message
        Write-Utf8Text -Path $OutputFile -Text ('Unavailable: ' + $_.Exception.Message)
    }
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = $null
    $algorithm = $null
    try {
        $share = [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, $share)
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        $hash = $algorithm.ComputeHash($stream)
        return (($hash | ForEach-Object { $_.ToString('X2') }) -join '')
    }
    finally {
        if ($null -ne $algorithm) { $algorithm.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function Get-PeMetadata {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = $null
    $reader = $null
    try {
        $share = [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, $share)
        $reader = New-Object System.IO.BinaryReader($stream)
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5A4D) {
            return $null
        }

        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0 -or ($peOffset + 26) -gt $stream.Length) {
            return $null
        }

        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            return $null
        }

        $machine = $reader.ReadUInt16()
        $sections = $reader.ReadUInt16()
        $timestamp = $reader.ReadUInt32()
        [void]$reader.ReadUInt32()
        [void]$reader.ReadUInt32()
        [void]$reader.ReadUInt16()
        $characteristics = $reader.ReadUInt16()
        $optionalMagic = $reader.ReadUInt16()

        $architecture = switch ($machine) {
            0x014C { 'x86' }
            0x8664 { 'x64/AMD64' }
            0xAA64 { 'ARM64' }
            default { 'Unknown' }
        }
        $format = switch ($optionalMagic) {
            0x010B { 'PE32' }
            0x020B { 'PE32+' }
            default { 'Unknown' }
        }
        $epoch = [DateTime]::SpecifyKind([DateTime]'1970-01-01 00:00:00', [DateTimeKind]::Utc)

        return [pscustomobject]@{
            Machine = ('0x{0:X4}' -f $machine)
            Architecture = $architecture
            Format = $format
            Sections = $sections
            Characteristics = ('0x{0:X4}' -f $characteristics)
            LinkerTimestampUtc = $epoch.AddSeconds($timestamp).ToString('o')
        }
    }
    finally {
        if ($null -ne $reader) { $reader.Dispose() }
        elseif ($null -ne $stream) { $stream.Dispose() }
    }
}

function Get-FileEvidenceRecord {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [switch]$Hash
    )

    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($item.FullName)
    $pe = Get-PeMetadata -Path $item.FullName
    $sha256 = ''
    if ($Hash) {
        $sha256 = Get-Sha256 -Path $item.FullName
    }

    return [pscustomobject]@{
        Path = $item.FullName
        Length = $item.Length
        CreatedUtc = $item.CreationTimeUtc.ToString('o')
        LastWriteUtc = $item.LastWriteTimeUtc.ToString('o')
        FileVersion = $version.FileVersion
        ProductVersion = $version.ProductVersion
        Description = $version.FileDescription
        Company = $version.CompanyName
        Machine = if ($null -ne $pe) { $pe.Machine } else { '' }
        Architecture = if ($null -ne $pe) { $pe.Architecture } else { '' }
        PeFormat = if ($null -ne $pe) { $pe.Format } else { '' }
        LinkerTimestampUtc = if ($null -ne $pe) { $pe.LinkerTimestampUtc } else { '' }
        SHA256 = $sha256
        SignatureStatus = 'Not collected (avoids certificate/network side effects)'
    }
}

function Get-SafeDestinationName {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    $parentName = $File.Directory.Name
    $baseName = '{0}_{1}_{2}' -f $File.LastWriteTimeUtc.ToString('yyyyMMdd-HHmmss'), $parentName, $File.Name
    $safeName = $baseName -replace '[\\/:*?"<>|]', '_'
    if ($safeName.Length -gt 180) {
        $extension = $File.Extension
        $safeName = $safeName.Substring(0, 170 - $extension.Length) + $extension
    }
    return $safeName
}

function Copy-EvidenceFile {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][int64]$MaximumFileBytes,
        [switch]$IsDump
    )

    try {
        $file = Get-Item -LiteralPath $Source -ErrorAction Stop
        if ($file.PSIsContainer) { return }
        if ($file.Length -gt $MaximumFileBytes) {
            Add-CollectorWarning -Area 'Evidence copy' -Message ("Skipped oversized file ({0:N0} bytes): {1}" -f $file.Length, $file.FullName)
            return
        }

        if ($IsDump) {
            if (($script:CopiedDumpBytes + $file.Length) -gt $script:MaximumTotalDumpCopyBytes) {
                Add-CollectorWarning -Area 'Evidence copy' -Message ('Skipped dump because the 256 MiB dump-copy cap was reached: ' + $file.FullName)
                return
            }
        }
        elseif (($script:CopiedBytes + $file.Length) -gt $script:MaximumTotalTextCopyBytes) {
            Add-CollectorWarning -Area 'Evidence copy' -Message ('Skipped file because the 256 MiB evidence-copy cap was reached: ' + $file.FullName)
            return
        }

        if (-not (Test-OutputFreeSpace -AdditionalBytes $file.Length)) {
            Add-CollectorWarning -Area 'Evidence copy' -Message ('Skipped file to preserve at least 25 MiB free on the output drive: ' + $file.FullName)
            return
        }

        $destinationDirectory = New-OutputSubdirectory -RelativePath ('evidence\' + $Category)
        $destinationName = Get-SafeDestinationName -File $file
        $destination = Join-Path $destinationDirectory $destinationName
        $counter = 1
        while (Test-Path -LiteralPath $destination) {
            $destination = Join-Path $destinationDirectory ('{0}_{1}{2}' -f [System.IO.Path]::GetFileNameWithoutExtension($destinationName), $counter, $file.Extension)
            $counter++
        }

        $sourceStream = $null
        $destinationStream = $null
        try {
            $share = [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
            $sourceStream = [System.IO.File]::Open($file.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, $share)
            $destinationStream = [System.IO.File]::Open($destination, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            $sourceStream.CopyTo($destinationStream)
            $destinationStream.Flush()
        }
        catch {
            if (Test-Path -LiteralPath $destination) {
                Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
            }
            throw
        }
        finally {
            if ($null -ne $destinationStream) { $destinationStream.Dispose() }
            if ($null -ne $sourceStream) { $sourceStream.Dispose() }
        }

        if ($IsDump) { $script:CopiedDumpBytes += $file.Length }
        else { $script:CopiedBytes += $file.Length }
        Write-CollectorLog ('Copied evidence: ' + $file.FullName + ' -> ' + $destination)
    }
    catch {
        Add-CollectorWarning -Area 'Evidence copy' -Message ($Source + ': ' + $_.Exception.Message)
    }
}

function Add-DetectedRoot {
    param([string]$Candidate, [string]$Source)

    if ([string]::IsNullOrWhiteSpace($Candidate)) { return }
    try {
        $fullPath = [System.IO.Path]::GetFullPath($Candidate.Trim('"'))
        if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) { return }
        $hasExpectedShape = (Test-Path -LiteralPath (Join-Path $fullPath 'Bin64')) -or
            (Test-Path -LiteralPath (Join-Path $fullPath 'UserData'))
        if (-not $hasExpectedShape) { return }
        if (-not (@($script:DetectedRoots | Where-Object { $_.Path -eq $fullPath }).Count)) {
            [void]$script:DetectedRoots.Add([pscustomobject]@{ Path = $fullPath; Source = $Source })
        }
    }
    catch {
        Add-CollectorWarning -Area 'rFactor 2 discovery' -Message ($Candidate + ': ' + $_.Exception.Message)
    }
}

function Get-EventRows {
    param(
        [Parameter(Mandatory = $true)][string]$LogName,
        [Parameter(Mandatory = $true)][int[]]$Ids,
        [int]$Maximum = 1000
    )

    if (-not (Get-Command Get-WinEvent -ErrorAction SilentlyContinue)) {
        throw 'Get-WinEvent is unavailable.'
    }

    try {
        $rawRecords = @(Get-WinEvent -FilterHashtable @{ LogName = $LogName; StartTime = $script:Since; Id = $Ids } -MaxEvents $Maximum -ErrorAction Stop)
    }
    catch [System.Exception] {
        if ($_.FullyQualifiedErrorId -match '^NoMatchingEventsFound') { return @() }
        throw
    }

    $rows = foreach ($record in $rawRecords) {
        $message = ''
        try { $message = $record.Message }
        catch { $message = '<message text unavailable: ' + $_.Exception.Message + '>' }
        [pscustomobject]@{
            TimeCreated = if ($null -ne $record.TimeCreated) { $record.TimeCreated.ToString('o') } else { '' }
            LogName = $record.LogName
            Provider = $record.ProviderName
            Id = $record.Id
            Level = $record.LevelDisplayName
            RecordId = $record.RecordId
            Message = $message
        }
    }
    return @($rows)
}

function Get-RegistrySnapshotText {
    param([Parameter(Mandatory = $true)][string[]]$Paths)

    $builder = New-Object System.Text.StringBuilder
    foreach ($path in $Paths) {
        [void]$builder.AppendLine(('=== ' + $path + ' ==='))
        try {
            if (Test-Path -LiteralPath $path) {
                $value = Get-ItemProperty -LiteralPath $path -ErrorAction Stop |
                    Select-Object * -ExcludeProperty PSPath, PSParentPath, PSChildName, PSDrive, PSProvider
                [void]$builder.AppendLine(($value | Format-List * | Out-String -Width 4096).TrimEnd())
            }
            else {
                [void]$builder.AppendLine('(key not present)')
            }
        }
        catch {
            [void]$builder.AppendLine(('Unavailable: ' + $_.Exception.Message))
            Add-CollectorWarning -Area 'Registry inventory' -Message ($path + ': ' + $_.Exception.Message)
        }
        [void]$builder.AppendLine('')
    }
    return $builder.ToString()
}

# Resolve the output location before touching host evidence.
$scriptPath = $MyInvocation.MyCommand.Path
$scriptDirectory = if ([string]::IsNullOrWhiteSpace($scriptPath)) { (Get-Location).Path } else { Split-Path -Parent $scriptPath }
if ([string]::IsNullOrWhiteSpace($OutputBase)) { $OutputBase = $scriptDirectory }

try {
    $OutputBase = [System.IO.Path]::GetFullPath($OutputBase)
    if (-not (Test-Path -LiteralPath $OutputBase -PathType Container)) {
        throw "OutputBase does not exist: $OutputBase"
    }

    $driveRoot = [System.IO.Path]::GetPathRoot($OutputBase)
    if (-not [string]::IsNullOrWhiteSpace($driveRoot)) {
        try {
            $driveInfo = New-Object System.IO.DriveInfo -ArgumentList $driveRoot
            if ($driveInfo.IsReady -and $driveInfo.AvailableFreeSpace -lt 25MB) {
                throw ('The output drive has less than 25 MiB free: ' + $driveRoot)
            }
        }
        catch [System.ArgumentException] {
            # UNC or unusual mounted paths may not map to DriveInfo. Directory creation below is the final preflight.
        }
    }

    $folderName = 'Rf2CrashEvidence-{0}-{1}' -f (Get-Date).ToString('yyyyMMdd-HHmmss'), $PID
    $script:OutputRoot = Join-Path $OutputBase $folderName
    [void](New-Item -ItemType Directory -Path $script:OutputRoot -ErrorAction Stop)
    $script:LogPath = Join-Path $script:OutputRoot 'collector.log'
    $script:WarningPath = Join-Path $script:OutputRoot 'WARNINGS.txt'
    Write-Utf8Text -Path $script:LogPath -Text ('Collector {0} started {1}' -f $script:CollectorVersion, (Get-Date).ToString('o'))
    Write-Utf8Text -Path $script:WarningPath -Text "Warnings are non-fatal. A missing/denied source is recorded here while other collection continues.`r`n"
}
catch {
    Write-HostSafe ('FATAL: Cannot create a safe evidence directory. No host configuration was changed. ' + $_.Exception.Message) Red
    exit 10
}

Write-HostSafe ''
Write-HostSafe 'rFactor 2 crash evidence collector' Green
Write-HostSafe ('Output: ' + $script:OutputRoot) Green
Write-HostSafe 'Host access is read-only; all output goes to the directory above.' Green
Write-HostSafe ''

$isAdministrator = $false
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    $isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
catch {
    Add-CollectorWarning -Area 'Privilege detection' -Message $_.Exception.Message
}

Invoke-CollectionStep -Name 'collector and host context' -Action {
    $context = [pscustomobject]@{
        CollectorVersion = $script:CollectorVersion
        StartedLocal = (Get-Date).ToString('o')
        StartedUtc = (Get-Date).ToUniversalTime().ToString('o')
        ComputerName = $env:COMPUTERNAME
        UserName = [Environment]::UserName
        IsAdministrator = $isAdministrator
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        PowerShellEdition = if ($PSVersionTable.ContainsKey('PSEdition')) { $PSVersionTable.PSEdition } else { 'Desktop' }
        ProcessArchitecture = if ([Environment]::Is64BitProcess) { '64-bit' } else { '32-bit' }
        OperatingSystemArchitecture = if ([Environment]::Is64BitOperatingSystem) { '64-bit' } else { '32-bit' }
        Culture = [Globalization.CultureInfo]::CurrentCulture.Name
        TimeZone = [TimeZoneInfo]::Local.DisplayName
        EvidenceSinceLocal = $script:Since.ToString('o')
        OutputRoot = $script:OutputRoot
        CopySmallDumps = [bool]$CopySmallDumps
    }
    Export-ObjectText -InputData $context -Path (Join-Path $script:OutputRoot 'host-context.txt')
}

Invoke-CollectionStep -Name 'operating system and hardware inventory' -Action {
    $directory = New-OutputSubdirectory -RelativePath 'system'
    Save-ManagementClass -ClassName 'Win32_OperatingSystem' -Properties @('Caption','Version','BuildNumber','OSArchitecture','InstallDate','LastBootUpTime','LocalDateTime','FreePhysicalMemory','TotalVisibleMemorySize','SystemDrive') -OutputFile (Join-Path $directory 'operating-system.csv')
    Save-ManagementClass -ClassName 'Win32_ComputerSystem' -Properties @('Manufacturer','Model','SystemType','TotalPhysicalMemory','Domain','PartOfDomain') -OutputFile (Join-Path $directory 'computer-system.csv')
    Save-ManagementClass -ClassName 'Win32_Processor' -Properties @('Name','Manufacturer','NumberOfCores','NumberOfLogicalProcessors','MaxClockSpeed','Status') -OutputFile (Join-Path $directory 'processors.csv')
    Save-ManagementClass -ClassName 'Win32_BIOS' -Properties @('Manufacturer','Name','SMBIOSBIOSVersion','ReleaseDate','Status') -OutputFile (Join-Path $directory 'bios.csv')
    Save-ManagementClass -ClassName 'Win32_BaseBoard' -Properties @('Manufacturer','Product','Version','SerialNumber','Status') -OutputFile (Join-Path $directory 'baseboard.csv')
    Save-ManagementClass -ClassName 'Win32_PhysicalMemory' -Properties @('Manufacturer','PartNumber','SerialNumber','Capacity','Speed','ConfiguredClockSpeed','DeviceLocator') -OutputFile (Join-Path $directory 'physical-memory.csv')
    Save-ManagementClass -ClassName 'Win32_DiskDrive' -Properties @('Model','Manufacturer','FirmwareRevision','InterfaceType','MediaType','Size','Status','DeviceID') -OutputFile (Join-Path $directory 'disk-drives.csv')
    Save-ManagementClass -ClassName 'Win32_LogicalDisk' -Properties @('DeviceID','VolumeName','FileSystem','Size','FreeSpace','Status') -OutputFile (Join-Path $directory 'logical-disks.csv') -Filter { $_.DriveType -eq 3 }
    Save-ManagementClass -ClassName 'Win32_VideoController' -Properties @('Name','AdapterCompatibility','DriverVersion','DriverDate','Status','VideoProcessor','AdapterRAM') -OutputFile (Join-Path $directory 'video-controllers.csv')
}

Invoke-CollectionStep -Name 'relevant processes and loaded modules' -Action {
    $directory = New-OutputSubdirectory -RelativePath 'processes'
    $allProcesses = @(Get-ManagementInstances -ClassName 'Win32_Process')
    $script:ServerProcesses = @($allProcesses | Where-Object {
        $_.Name -match '(?i)rFactor|Dedicated|Trackside|SteamCMD'
    })
    $processRows = @($script:ServerProcesses | Select-Object Name,ProcessId,ParentProcessId,SessionId,CreationDate,ExecutablePath,@{Name='CommandLine';Expression={ Protect-CommandLine -CommandLine $_.CommandLine }})
    Export-SafeCsv -InputData $processRows -Path (Join-Path $directory 'relevant-processes.csv')

    $moduleRows = New-Object System.Collections.ArrayList
    foreach ($processRow in $script:ServerProcesses) {
        try {
            $process = Get-Process -Id $processRow.ProcessId -ErrorAction Stop
            foreach ($module in @($process.Modules)) {
                [void]$moduleRows.Add([pscustomobject]@{
                    ProcessName = $processRow.Name
                    ProcessId = $processRow.ProcessId
                    ModuleName = $module.ModuleName
                    FileName = $module.FileName
                    FileVersion = $module.FileVersionInfo.FileVersion
                    ProductVersion = $module.FileVersionInfo.ProductVersion
                })
            }
        }
        catch {
            Add-CollectorWarning -Area 'Loaded modules' -Message ("PID {0} ({1}): {2}" -f $processRow.ProcessId, $processRow.Name, $_.Exception.Message)
        }
    }
    Export-SafeCsv -InputData @($moduleRows) -Path (Join-Path $directory 'loaded-modules.csv')
}

Invoke-CollectionStep -Name 'relevant services' -Action {
    $rows = @()
    try {
        $rows = @(Get-Service -ErrorAction Stop | Where-Object {
            $_.Name -match '(?i)Trackside|rFactor|Steam' -or $_.DisplayName -match '(?i)Trackside|rFactor'
        } | Select-Object Name,DisplayName,Status,StartType)
    }
    catch {
        Add-CollectorWarning -Area 'Services' -Message $_.Exception.Message
    }
    Export-SafeCsv -InputData $rows -Path (Join-Path $script:OutputRoot 'relevant-services.csv')
}

Invoke-CollectionStep -Name 'rFactor 2 installation discovery' -Action {
    Add-DetectedRoot -Candidate $Rf2Root -Source 'command-line parameter'

    foreach ($processRow in $script:ServerProcesses) {
        if ([string]::IsNullOrWhiteSpace($processRow.ExecutablePath)) { continue }
        try {
            $exeDirectory = Split-Path -Parent $processRow.ExecutablePath
            if ((Split-Path -Leaf $exeDirectory) -match '^(?i)(Bin32|Bin64)$') {
                Add-DetectedRoot -Candidate (Split-Path -Parent $exeDirectory) -Source ('running process ' + $processRow.Name)
            }
        }
        catch {
            Add-CollectorWarning -Area 'rFactor 2 discovery' -Message $_.Exception.Message
        }
    }

    $commonRoots = @(
        "$env:ProgramFiles\Steam\steamapps\common\rFactor 2",
        "$env:ProgramFiles\Steam\steamapps\common\rFactor2-Dedicated",
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\rFactor 2",
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\rFactor2-Dedicated",
        'C:\Racing\rFactor2-Dedicated',
        'C:\rFactor2-Dedicated'
    )
    foreach ($candidate in $commonRoots) { Add-DetectedRoot -Candidate $candidate -Source 'common location' }

    $steamRoots = New-Object System.Collections.ArrayList
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        try {
            if (Test-Path -LiteralPath $key) {
                $values = Get-ItemProperty -LiteralPath $key -ErrorAction Stop
                foreach ($propertyName in @('SteamPath','InstallPath')) {
                    $value = Get-PropertyValue -InputObject $values -Name $propertyName
                    if (-not [string]::IsNullOrWhiteSpace($value) -and -not $steamRoots.Contains($value)) {
                        [void]$steamRoots.Add($value)
                    }
                }
            }
        }
        catch {
            Add-CollectorWarning -Area 'Steam discovery' -Message ($key + ': ' + $_.Exception.Message)
        }
    }

    foreach ($steamRoot in @($steamRoots)) {
        foreach ($folder in @('rFactor 2','rFactor2-Dedicated','rFactor 2 Dedicated')) {
            Add-DetectedRoot -Candidate (Join-Path $steamRoot ('steamapps\common\' + $folder)) -Source 'Steam root'
        }
        $libraryFile = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $libraryFile) {
            try {
                $rawLibrary = [System.IO.File]::ReadAllText($libraryFile)
                $libraryPathCaptures = [regex]::Matches($rawLibrary, '(?im)"path"\s+"(?<path>[^"]+)"')
                foreach ($capture in $libraryPathCaptures) {
                    $libraryRoot = $capture.Groups['path'].Value -replace '\\\\', '\'
                    foreach ($folder in @('rFactor 2','rFactor2-Dedicated','rFactor 2 Dedicated')) {
                        Add-DetectedRoot -Candidate (Join-Path $libraryRoot ('steamapps\common\' + $folder)) -Source 'Steam libraryfolders.vdf'
                    }
                }
            }
            catch {
                Add-CollectorWarning -Area 'Steam library discovery' -Message $_.Exception.Message
            }
        }
    }

    Export-SafeCsv -InputData @($script:DetectedRoots) -Path (Join-Path $script:OutputRoot 'rf2-installations.csv')
    if ($script:DetectedRoots.Count -eq 0) {
        Add-CollectorWarning -Area 'rFactor 2 discovery' -Message 'No installation was found. General Windows evidence was still collected. Re-run with -Rf2Root to collect installation files.'
    }
}

Invoke-CollectionStep -Name 'rFactor 2 binaries, plugin configuration, and logs' -Action {
    $rootNumber = 0
    foreach ($rootRecord in @($script:DetectedRoots)) {
        $rootNumber++
        $root = $rootRecord.Path
        $relativeDirectory = 'rf2-{0:D2}' -f $rootNumber
        $directory = New-OutputSubdirectory -RelativePath $relativeDirectory
        Write-Utf8Text -Path (Join-Path $directory 'installation-root.txt') -Text ("Path: {0}`r`nDiscovered from: {1}`r`n" -f $root, $rootRecord.Source)

        $binaryRows = New-Object System.Collections.ArrayList
        $binaryCandidates = @(
            (Join-Path $root 'Bin64\rFactor2 Dedicated.exe'),
            (Join-Path $root 'Bin64\Dedicated.exe'),
            (Join-Path $root 'Bin32\rFactor2 Dedicated.exe'),
            (Join-Path $root 'Bin32\Dedicated.exe'),
            (Join-Path $root 'Bin64\Plugins\rFactor2SharedMemoryMapPlugin64.dll'),
            (Join-Path $root 'Bin32\Plugins\rFactor2SharedMemoryMapPlugin32.dll')
        )
        foreach ($candidate in $binaryCandidates) {
            if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
            try {
                $record = Get-FileEvidenceRecord -Path $candidate -Hash
                [void]$binaryRows.Add($record)
                if ($record.Path -match '(?i)rFactor2SharedMemoryMapPlugin64\.dll$') {
                    $matchesExpected = $record.SHA256 -eq $script:ExpectedPluginHash
                    [void]$script:PluginFindings.Add([pscustomobject]@{
                        Installation = $root
                        Path = $record.Path
                        SHA256 = $record.SHA256
                        MatchesRepositoryUpstream3151 = $matchesExpected
                        Architecture = $record.Architecture
                        LastWriteUtc = $record.LastWriteUtc
                    })
                }
            }
            catch {
                Add-CollectorWarning -Area 'Binary inspection' -Message ($candidate + ': ' + $_.Exception.Message)
            }
        }
        Export-SafeCsv -InputData @($binaryRows) -Path (Join-Path $directory 'key-binaries.csv')

        $pluginDirectory = Join-Path $root 'Bin64\Plugins'
        $pluginRows = New-Object System.Collections.ArrayList
        if (Test-Path -LiteralPath $pluginDirectory -PathType Container) {
            try {
                foreach ($plugin in @(Get-ChildItem -LiteralPath $pluginDirectory -Filter '*.dll' -ErrorAction Stop)) {
                    try { [void]$pluginRows.Add((Get-FileEvidenceRecord -Path $plugin.FullName)) }
                    catch { Add-CollectorWarning -Area 'Plugin inventory' -Message ($plugin.FullName + ': ' + $_.Exception.Message) }
                }
            }
            catch {
                Add-CollectorWarning -Area 'Plugin inventory' -Message $_.Exception.Message
            }
        }
        Export-SafeCsv -InputData @($pluginRows) -Path (Join-Path $directory 'all-bin64-plugins.csv')

        $configurationRows = New-Object System.Collections.ArrayList
        $configurationReport = New-Object System.Text.StringBuilder
        $userData = Join-Path $root 'UserData'
        if (Test-Path -LiteralPath $userData -PathType Container) {
            $configFiles = @()
            try {
                $configFiles = @(Get-ChildItem -LiteralPath $userData -Filter 'CustomPluginVariables.JSON' -Recurse -ErrorAction Stop)
            }
            catch {
                Add-CollectorWarning -Area 'Plugin configuration discovery' -Message $_.Exception.Message
            }

            foreach ($configFile in $configFiles) {
                $jsonValid = $false
                $entryFound = $false
                $enabledValue = '(entry not found)'
                $pluginBlock = ''
                try {
                    $raw = [System.IO.File]::ReadAllText($configFile.FullName)
                    try {
                        [void]($raw | ConvertFrom-Json -ErrorAction Stop)
                        $jsonValid = $raw.TrimStart().StartsWith('{')
                    }
                    catch {
                        $jsonValid = $false
                    }

                    $blockMatch = [regex]::Match($raw, '(?is)"rFactor2SharedMemoryMapPlugin64\.dll"\s*:\s*\{(?<body>.*?)\}')
                    if ($blockMatch.Success) {
                        $entryFound = $true
                        $pluginBlock = $blockMatch.Value
                        $enabledMatch = [regex]::Match($pluginBlock, '(?is)" Enabled"\s*:\s*(?<value>-?\d+)')
                        if ($enabledMatch.Success) { $enabledValue = $enabledMatch.Groups['value'].Value }
                        else { $enabledValue = '(" Enabled" value not present)'}
                    }

                    $configurationRecord = [pscustomobject]@{
                        Installation = $root
                        Path = $configFile.FullName
                        Length = $configFile.Length
                        LastWriteUtc = $configFile.LastWriteTimeUtc.ToString('o')
                        SHA256 = Get-Sha256 -Path $configFile.FullName
                        JsonValid = $jsonValid
                        SharedMemoryPluginEntryFound = $entryFound
                        EnabledValue = $enabledValue
                    }
                    [void]$configurationRows.Add($configurationRecord)
                    [void]$script:ConfigurationFindings.Add($configurationRecord)
                    Copy-EvidenceFile -Source $configFile.FullName -Category ('rf2-{0:D2}-configuration' -f $rootNumber) -MaximumFileBytes $script:MaximumTextCopyBytes
                    if (-not $jsonValid) {
                        Add-CollectorWarning -Area 'CRITICAL plugin configuration finding' -Message ('Malformed CustomPluginVariables.JSON can silently prevent rFactor 2 Dedicated from starting: ' + $configFile.FullName)
                    }
                    [void]$configurationReport.AppendLine(('=== ' + $configFile.FullName + ' ==='))
                    [void]$configurationReport.AppendLine(('Valid JSON: ' + $jsonValid))
                    [void]$configurationReport.AppendLine(('Plugin entry found: ' + $entryFound))
                    [void]$configurationReport.AppendLine(('" Enabled" value: ' + $enabledValue))
                    if ($entryFound) { [void]$configurationReport.AppendLine($pluginBlock) }
                    [void]$configurationReport.AppendLine('')
                }
                catch {
                    Add-CollectorWarning -Area 'Plugin configuration inspection' -Message ($configFile.FullName + ': ' + $_.Exception.Message)
                }
            }

            $profileMetadata = New-Object System.Collections.ArrayList
            foreach ($pattern in @('player.JSON','Multiplayer.JSON','CustomPluginVariables.JSON')) {
                try {
                    foreach ($profileFile in @(Get-ChildItem -LiteralPath $userData -Filter $pattern -Recurse -ErrorAction Stop)) {
                        [void]$profileMetadata.Add([pscustomobject]@{
                            Path = $profileFile.FullName
                            Length = $profileFile.Length
                            CreatedUtc = $profileFile.CreationTimeUtc.ToString('o')
                            LastWriteUtc = $profileFile.LastWriteTimeUtc.ToString('o')
                            SHA256 = Get-Sha256 -Path $profileFile.FullName
                        })
                    }
                }
                catch {
                    Add-CollectorWarning -Area 'Profile metadata' -Message ($pattern + ': ' + $_.Exception.Message)
                }
            }
            Export-SafeCsv -InputData @($profileMetadata) -Path (Join-Path $directory 'profile-file-metadata.csv')

            $logDirectory = Join-Path $userData 'Log'
            if (Test-Path -LiteralPath $logDirectory -PathType Container) {
                foreach ($pattern in @('trace*.txt','RF2SMMP*.txt')) {
                    try {
                        $logs = @(Get-ChildItem -LiteralPath $logDirectory -Filter $pattern -ErrorAction Stop |
                            Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 20)
                        foreach ($log in $logs) {
                            Copy-EvidenceFile -Source $log.FullName -Category ('rf2-{0:D2}-logs' -f $rootNumber) -MaximumFileBytes $script:MaximumTextCopyBytes
                        }
                    }
                    catch {
                        Add-CollectorWarning -Area 'rFactor 2 log copy' -Message ($pattern + ': ' + $_.Exception.Message)
                    }
                }
            }

            try {
                foreach ($dump in @(Get-ChildItem -LiteralPath $userData -Filter '*.dmp' -Recurse -ErrorAction Stop)) {
                    [void]$script:DumpFiles.Add($dump)
                }
            }
            catch {
                Add-CollectorWarning -Area 'rFactor 2 dump discovery' -Message $_.Exception.Message
            }
        }
        Export-SafeCsv -InputData @($configurationRows) -Path (Join-Path $directory 'plugin-configuration-files.csv')
        Write-Utf8Text -Path (Join-Path $directory 'shared-memory-plugin-configuration.txt') -Text $configurationReport.ToString()
    }
    Export-SafeCsv -InputData @($script:PluginFindings) -Path (Join-Path $script:OutputRoot 'shared-memory-plugin-findings.csv')
}

Invoke-CollectionStep -Name 'application and system event logs' -Action {
    $directory = New-OutputSubdirectory -RelativePath 'events'

    try {
        $applicationRows = Get-EventRows -LogName 'Application' -Ids @(33,1000,1001,1026) -Maximum 1000
        Export-SafeCsv -InputData $applicationRows -Path (Join-Path $directory 'application-crash-events.csv')
    }
    catch {
        Add-CollectorWarning -Area 'Application event log' -Message $_.Exception.Message
        Write-Utf8Text -Path (Join-Path $directory 'application-crash-events.csv') -Text ('Unavailable: ' + $_.Exception.Message)
    }

    try {
        $systemRows = Get-EventRows -LogName 'System' -Ids @(7,9,11,15,17,18,19,20,41,47,51,55,57,98,129,153,157,161,1001,6008) -Maximum 2000
        Export-SafeCsv -InputData $systemRows -Path (Join-Path $directory 'system-stability-events.csv')
    }
    catch {
        Add-CollectorWarning -Area 'System event log' -Message $_.Exception.Message
        Write-Utf8Text -Path (Join-Path $directory 'system-stability-events.csv') -Text ('Unavailable: ' + $_.Exception.Message)
    }

    foreach ($channel in @('Microsoft-Windows-WER-Diag/Operational','Microsoft-Windows-Kernel-WHEA/Errors')) {
        try {
            $safeChannelName = $channel -replace '[\\/]', '_'
            $rows = @(Get-WinEvent -FilterHashtable @{ LogName = $channel; StartTime = $script:Since } -MaxEvents 500 -ErrorAction Stop | ForEach-Object {
                $message = ''
                try { $message = $_.Message } catch { $message = '<message unavailable>' }
                [pscustomobject]@{
                    TimeCreated = if ($null -ne $_.TimeCreated) { $_.TimeCreated.ToString('o') } else { '' }
                    Provider = $_.ProviderName
                    Id = $_.Id
                    Level = $_.LevelDisplayName
                    RecordId = $_.RecordId
                    Message = $message
                }
            })
            Export-SafeCsv -InputData $rows -Path (Join-Path $directory ($safeChannelName + '.csv'))
        }
        catch {
            # These optional channels are commonly disabled or absent.
            Write-Utf8Text -Path (Join-Path $directory (($channel -replace '[\\/]', '_') + '.csv')) -Text ('Optional channel unavailable: ' + $_.Exception.Message)
            Write-CollectorLog ('Optional event channel unavailable: ' + $channel + ': ' + $_.Exception.Message)
        }
    }
}

Invoke-CollectionStep -Name 'Windows reliability records' -Action {
    $directory = New-OutputSubdirectory -RelativePath 'events'
    try {
        $records = @(Get-ManagementInstances -ClassName 'Win32_ReliabilityRecords' |
            Sort-Object TimeGenerated -Descending | Select-Object -First 1000 |
            Select-Object TimeGenerated,SourceName,ProductName,EventIdentifier,LogFile,Message)
        Export-SafeCsv -InputData $records -Path (Join-Path $directory 'reliability-records.csv')
    }
    catch {
        Add-CollectorWarning -Area 'Reliability records' -Message $_.Exception.Message
        Write-Utf8Text -Path (Join-Path $directory 'reliability-records.csv') -Text ('Unavailable: ' + $_.Exception.Message)
    }
}

Invoke-CollectionStep -Name 'crash-dump and WER configuration' -Action {
    $directory = New-OutputSubdirectory -RelativePath 'crash-configuration'
    $registryPaths = @(
        'HKLM:\SYSTEM\CurrentControlSet\Control\CrashControl',
        'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting',
        'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps',
        'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\rFactor2 Dedicated.exe',
        'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\Dedicated.exe'
    )
    Write-Utf8Text -Path (Join-Path $directory 'registry-settings.txt') -Text (Get-RegistrySnapshotText -Paths $registryPaths)
}

Invoke-CollectionStep -Name 'installed runtime inventory' -Action {
    $directory = New-OutputSubdirectory -RelativePath 'runtimes'
    $rows = New-Object System.Collections.ArrayList
    foreach ($uninstallRoot in @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )) {
        try {
            if (-not (Test-Path -LiteralPath $uninstallRoot)) { continue }
            foreach ($key in @(Get-ChildItem -LiteralPath $uninstallRoot -ErrorAction Stop)) {
                try {
                    $product = Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction Stop
                    $displayName = Get-PropertyValue -InputObject $product -Name 'DisplayName' -DefaultValue ''
                    if ($displayName -match '(?i)\.NET|ASP\.NET|Visual C\+\+.*2013|Visual C\+\+.*Redistributable') {
                        [void]$rows.Add([pscustomobject]@{
                            DisplayName = $displayName
                            DisplayVersion = Get-PropertyValue -InputObject $product -Name 'DisplayVersion' -DefaultValue ''
                            Publisher = Get-PropertyValue -InputObject $product -Name 'Publisher' -DefaultValue ''
                            InstallDate = Get-PropertyValue -InputObject $product -Name 'InstallDate' -DefaultValue ''
                            InstallLocation = Get-PropertyValue -InputObject $product -Name 'InstallLocation' -DefaultValue ''
                            RegistryPath = $key.Name
                        })
                    }
                }
                catch {
                    # Individual uninstall entries can be malformed; skip without aborting the inventory.
                }
            }
        }
        catch {
            Add-CollectorWarning -Area 'Runtime inventory' -Message ($uninstallRoot + ': ' + $_.Exception.Message)
        }
    }
    Export-SafeCsv -InputData @($rows) -Path (Join-Path $directory 'installed-runtime-products.csv')

    $runtimeDirectories = New-Object System.Collections.ArrayList
    foreach ($sharedRoot in @(
        "$env:ProgramFiles\dotnet\shared",
        "${env:ProgramFiles(x86)}\dotnet\shared"
    )) {
        if ([string]::IsNullOrWhiteSpace($sharedRoot) -or -not (Test-Path -LiteralPath $sharedRoot)) { continue }
        try {
            foreach ($framework in @(Get-ChildItem -LiteralPath $sharedRoot -Directory -ErrorAction Stop)) {
                foreach ($version in @(Get-ChildItem -LiteralPath $framework.FullName -Directory -ErrorAction Stop)) {
                    [void]$runtimeDirectories.Add([pscustomobject]@{
                        Framework = $framework.Name
                        Version = $version.Name
                        Path = $version.FullName
                    })
                }
            }
        }
        catch {
            Add-CollectorWarning -Area '.NET directory inventory' -Message ($sharedRoot + ': ' + $_.Exception.Message)
        }
    }
    Export-SafeCsv -InputData @($runtimeDirectories) -Path (Join-Path $directory 'dotnet-shared-frameworks.csv')

    $vcRuntime = Join-Path $env:WINDIR 'System32\msvcr120.dll'
    if (Test-Path -LiteralPath $vcRuntime -PathType Leaf) {
        Export-SafeCsv -InputData @((Get-FileEvidenceRecord -Path $vcRuntime -Hash)) -Path (Join-Path $directory 'msvcr120-x64.csv')
    }
    else {
        Write-Utf8Text -Path (Join-Path $directory 'msvcr120-x64.csv') -Text ('Not found: ' + $vcRuntime)
    }
}

Invoke-CollectionStep -Name 'existing dump and WER report inventory' -Action {
    $dumpCandidates = New-Object System.Collections.ArrayList
    $dumpLocations = New-Object System.Collections.ArrayList
    $defaultCrashDumpFolder = ''
    [void]$dumpLocations.Add((Join-Path $env:WINDIR 'MEMORY.DMP'))
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $defaultCrashDumpFolder = Join-Path $env:LOCALAPPDATA 'CrashDumps'
        [void]$dumpLocations.Add($defaultCrashDumpFolder)
    }

    foreach ($localDumpKey in @(
        'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps',
        'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\rFactor2 Dedicated.exe',
        'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\Dedicated.exe'
    )) {
        try {
            if (-not (Test-Path -LiteralPath $localDumpKey)) { continue }
            $settings = Get-ItemProperty -LiteralPath $localDumpKey -ErrorAction Stop
            $configuredFolder = Get-PropertyValue -InputObject $settings -Name 'DumpFolder' -DefaultValue ''
            if (-not [string]::IsNullOrWhiteSpace($configuredFolder)) {
                $expandedFolder = [Environment]::ExpandEnvironmentVariables([string]$configuredFolder)
                if (-not $dumpLocations.Contains($expandedFolder)) { [void]$dumpLocations.Add($expandedFolder) }
            }
        }
        catch {
            Add-CollectorWarning -Area 'LocalDumps folder discovery' -Message ($localDumpKey + ': ' + $_.Exception.Message)
        }
    }

    foreach ($path in @($dumpLocations | Select-Object -Unique)) {
        try {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                [void]$dumpCandidates.Add((Get-Item -LiteralPath $path -ErrorAction Stop))
            }
            elseif (Test-Path -LiteralPath $path -PathType Container) {
                foreach ($file in @(Get-ChildItem -LiteralPath $path -Filter '*.dmp' -ErrorAction Stop | Where-Object {
                    $_.Name -match '(?i)rFactor|Dedicated|Trackside'
                })) {
                    [void]$dumpCandidates.Add($file)
                }
            }
        }
        catch {
            Add-CollectorWarning -Area 'Dump inventory' -Message ($path + ': ' + $_.Exception.Message)
        }
    }

    foreach ($directoryPath in @((Join-Path $env:WINDIR 'Minidump'), (Join-Path $env:WINDIR 'LiveKernelReports'))) {
        try {
            if (Test-Path -LiteralPath $directoryPath -PathType Container) {
                foreach ($file in @(Get-ChildItem -LiteralPath $directoryPath -Filter '*.dmp' -Recurse -ErrorAction Stop)) {
                    [void]$dumpCandidates.Add($file)
                }
            }
        }
        catch {
            Add-CollectorWarning -Area 'Dump inventory' -Message ($directoryPath + ': ' + $_.Exception.Message)
        }
    }

    foreach ($file in @($script:DumpFiles)) { [void]$dumpCandidates.Add($file) }

    $uniqueDumps = @($dumpCandidates | Group-Object FullName | ForEach-Object { $_.Group[0] } | Sort-Object LastWriteTimeUtc -Descending)
    $script:ExistingDumpCount = $uniqueDumps.Count
    $dumpRows = @($uniqueDumps | ForEach-Object {
        [pscustomobject]@{
            Path = $_.FullName
            Length = $_.Length
            CreatedUtc = $_.CreationTimeUtc.ToString('o')
            LastWriteUtc = $_.LastWriteTimeUtc.ToString('o')
            CopiedByCollector = [bool]($CopySmallDumps -and $_.Length -le $script:MaximumDumpCopyBytes)
        }
    })
    Export-SafeCsv -InputData $dumpRows -Path (Join-Path $script:OutputRoot 'existing-dumps.csv')

    if ($CopySmallDumps) {
        foreach ($dump in @($uniqueDumps | Select-Object -First 30)) {
            Copy-EvidenceFile -Source $dump.FullName -Category 'dumps' -MaximumFileBytes $script:MaximumDumpCopyBytes -IsDump
        }
    }

    foreach ($werRoot in @(
        (Join-Path $env:ProgramData 'Microsoft\Windows\WER\ReportArchive'),
        (Join-Path $env:ProgramData 'Microsoft\Windows\WER\ReportQueue')
    )) {
        try {
            if (-not (Test-Path -LiteralPath $werRoot -PathType Container)) { continue }
            $allRecentFiles = @(Get-ChildItem -LiteralPath $werRoot -Recurse -File -ErrorAction Stop | Where-Object { $_.LastWriteTime -ge $script:Since })
            $relevantReportDirectories = @{}
            foreach ($werFile in @($allRecentFiles | Where-Object { $_.Extension -eq '.wer' })) {
                $isRelevant = $werFile.FullName -match '(?i)rFactor|Dedicated|Trackside'
                if (-not $isRelevant -and $werFile.Length -le 4MB) {
                    try {
                        $werText = [System.IO.File]::ReadAllText($werFile.FullName)
                        $isRelevant = $werText -match '(?im)^(AppName|AppPath|FriendlyEventName|Sig\[0\]\.Value)=.*(rFactor|Dedicated|Trackside)'
                    }
                    catch {
                        Write-CollectorLog ('Could not inspect WER report relevance: ' + $werFile.FullName + ': ' + $_.Exception.Message)
                    }
                }
                if ($isRelevant) { $relevantReportDirectories[$werFile.DirectoryName] = $true }
            }
            $reports = @($allRecentFiles | Where-Object {
                $relevantReportDirectories.ContainsKey($_.DirectoryName) -and $_.Extension -in @('.wer','.txt','.xml','.dmp')
            } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 100)
            foreach ($report in $reports) {
                if ($report.Extension -eq '.wer' -or $report.Extension -eq '.txt' -or $report.Extension -eq '.xml') {
                    Copy-EvidenceFile -Source $report.FullName -Category 'wer-reports' -MaximumFileBytes $script:MaximumTextCopyBytes
                }
                elseif ($CopySmallDumps -and $report.Extension -eq '.dmp') {
                    Copy-EvidenceFile -Source $report.FullName -Category 'dumps' -MaximumFileBytes $script:MaximumDumpCopyBytes -IsDump
                }
            }
        }
        catch {
            Add-CollectorWarning -Area 'WER report inventory' -Message ($werRoot + ': ' + $_.Exception.Message)
        }
    }
}

Invoke-CollectionStep -Name 'output manifest and final summary' -Action {
    $summary = New-Object System.Text.StringBuilder
    [void]$summary.AppendLine('rFactor 2 crash evidence summary')
    [void]$summary.AppendLine('================================')
    [void]$summary.AppendLine(('Collector version: ' + $script:CollectorVersion))
    [void]$summary.AppendLine(('Completed local: ' + (Get-Date).ToString('o')))
    [void]$summary.AppendLine(('Output directory: ' + $script:OutputRoot))
    [void]$summary.AppendLine(('Ran as administrator: ' + $isAdministrator))
    [void]$summary.AppendLine(('PowerShell: ' + $PSVersionTable.PSVersion.ToString()))
    [void]$summary.AppendLine(('Lookback days: ' + $Days))
    [void]$summary.AppendLine(('Warnings: ' + $script:Warnings.Count))
    [void]$summary.AppendLine(('Detected rFactor 2 installations: ' + $script:DetectedRoots.Count))
    [void]$summary.AppendLine(('Relevant running processes: ' + @($script:ServerProcesses).Count))
    [void]$summary.AppendLine(('Shared-memory plugin binaries found: ' + $script:PluginFindings.Count))
    [void]$summary.AppendLine(('Plugin configuration files found: ' + $script:ConfigurationFindings.Count))
    [void]$summary.AppendLine(('Malformed plugin configuration files: ' + @($script:ConfigurationFindings | Where-Object { -not $_.JsonValid }).Count))
    [void]$summary.AppendLine(('Existing dump files found: ' + $script:ExistingDumpCount))
    [void]$summary.AppendLine(('Text evidence copied: {0:N0} bytes' -f $script:CopiedBytes))
    [void]$summary.AppendLine(('Dump evidence copied: {0:N0} bytes' -f $script:CopiedDumpBytes))
    [void]$summary.AppendLine('')
    [void]$summary.AppendLine('Plugin identity results:')
    if ($script:PluginFindings.Count -eq 0) {
        [void]$summary.AppendLine('  No rFactor2SharedMemoryMapPlugin64.dll was found in detected installations.')
    }
    else {
        foreach ($finding in @($script:PluginFindings)) {
            [void]$summary.AppendLine(('  {0}' -f $finding.Path))
            [void]$summary.AppendLine(('    SHA-256: {0}' -f $finding.SHA256))
            [void]$summary.AppendLine(('    Matches repository/upstream 3.7.15.1: {0}' -f $finding.MatchesRepositoryUpstream3151))
            [void]$summary.AppendLine(('    Architecture: {0}' -f $finding.Architecture))
        }
    }
    [void]$summary.AppendLine('')
    [void]$summary.AppendLine('Plugin configuration results:')
    if ($script:ConfigurationFindings.Count -eq 0) {
        [void]$summary.AppendLine('  No CustomPluginVariables.JSON was found in detected installations.')
    }
    else {
        foreach ($finding in @($script:ConfigurationFindings)) {
            $status = if ($finding.JsonValid) { 'VALID JSON' } else { 'MALFORMED JSON - known to prevent Dedicated Server startup' }
            [void]$summary.AppendLine(('  {0}' -f $finding.Path))
            [void]$summary.AppendLine(('    Status: {0}' -f $status))
            [void]$summary.AppendLine(('    Shared-memory plugin entry: {0}' -f $finding.SharedMemoryPluginEntryFound))
            [void]$summary.AppendLine(('    " Enabled" value: {0}' -f $finding.EnabledValue))
        }
    }
    [void]$summary.AppendLine('')
    [void]$summary.AppendLine('Interpretation guardrails:')
    [void]$summary.AppendLine('  - Presence on disk does not prove that the plugin remained loaded or received callbacks.')
    [void]$summary.AppendLine('  - " Enabled":0 or a missing enabled entry normally prevents active plugin startup/callbacks.')
    [void]$summary.AppendLine('  - Malformed CustomPluginVariables.JSON is a confirmed cause of silent Dedicated Server startup failure.')
    [void]$summary.AppendLine('  - Kernel-Power event 41 records an unclean shutdown; it does not identify the cause.')
    [void]$summary.AppendLine('  - A faulting module name is evidence, but a dump stack is needed for strong attribution.')
    [void]$summary.AppendLine('  - See WARNINGS.txt for sources that were absent or inaccessible.')
    [void]$summary.AppendLine('')
    [void]$summary.AppendLine('Safety: this collector did not change host configuration. Its only writes are in this output directory.')
    Write-Utf8Text -Path (Join-Path $script:OutputRoot '00-SUMMARY.txt') -Text $summary.ToString()

}

try {
    $completionText = @"
COLLECTION COMPLETE

Completed local: $(Get-Date -Format o)
Collector version: $($script:CollectorVersion)
Warnings: $($script:Warnings.Count)

Open 00-SUMMARY.txt first, then WARNINGS.txt. A warning means that one source was
missing or inaccessible; the remaining sections still completed. Host configuration
was not changed. All collector writes are contained in this evidence directory.
"@
    Write-CollectorLog 'COLLECTION COMPLETE'

    # Generate this after the last mutable evidence write so hashes describe final files.
    $manifestRows = New-Object System.Collections.ArrayList
    foreach ($file in @(Get-ChildItem -LiteralPath $script:OutputRoot -File -Recurse -ErrorAction Stop | Where-Object { $_.Name -ne 'SHA256-MANIFEST.csv' })) {
        try {
            [void]$manifestRows.Add([pscustomobject]@{
                Path = $file.FullName.Substring($script:OutputRoot.Length).TrimStart('\')
                Length = $file.Length
                LastWriteUtc = $file.LastWriteTimeUtc.ToString('o')
                SHA256 = Get-Sha256 -Path $file.FullName
            })
        }
        catch {
            throw ('Could not hash final output file: ' + $file.FullName + ': ' + $_.Exception.Message)
        }
    }
    Export-SafeCsv -InputData @($manifestRows) -Path (Join-Path $script:OutputRoot 'SHA256-MANIFEST.csv')

    # The completion marker is intentionally last. Its presence means all required finalization succeeded.
    Write-Utf8Text -Path (Join-Path $script:OutputRoot 'COLLECTION-COMPLETE.txt') -Text $completionText

    Write-HostSafe ''
    Write-HostSafe 'Collection complete.' Green
    Write-HostSafe ('Open: ' + (Join-Path $script:OutputRoot '00-SUMMARY.txt')) Green
    if ($script:Warnings.Count -gt 0) {
        Write-HostSafe ('Warnings: ' + $script:Warnings.Count + ' (see WARNINGS.txt)') Yellow
    }
    exit 0
}
catch {
    Write-HostSafe ('FATAL while finalizing output: ' + $_.Exception.Message) Red
    try {
        $completionPath = Join-Path $script:OutputRoot 'COLLECTION-COMPLETE.txt'
        if (Test-Path -LiteralPath $completionPath) { Remove-Item -LiteralPath $completionPath -Force -ErrorAction SilentlyContinue }
        Write-Utf8Text -Path (Join-Path $script:OutputRoot 'COLLECTION-FAILED.txt') -Text ('Finalization failed: ' + $_.Exception.ToString())
    }
    catch {}
    exit 20
}
