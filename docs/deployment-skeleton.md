# Trackside Deployment Skeleton

This document describes the Phase 0C packaged runtime shape. It is intentionally script-based for now: no MSI/MSIX installer, no remote hosting, and no silent updates.

## Bundle Command

Create a versioned bundle from the repository root:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File services\trackside\scripts\New-TracksideBundle.ps1
```

The script builds/tests the .NET solution, builds the kiosk, publishes `Trackside.Service`, `Trackside.Tray`, `Trackside.RigAgent`, and `Trackside.Updater`, then writes a bundle under `artifacts\trackside\bundles\Trackside-<bundle-version>-win-x64`.

Useful options:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File services\trackside\scripts\New-TracksideBundle.ps1 -BundleVersion 0.1.0-canary.1
pwsh -NoProfile -ExecutionPolicy Bypass -File services\trackside\scripts\New-TracksideBundle.ps1 -NoArchive
```

## Bundle Layout

The generated bundle uses this layout:

```text
app/
  service/      Trackside.Service executable and packaged web assets
  tray/         Trackside.Tray notification-area companion
  rig-agent/    future rig-side worker executable
  updater/      out-of-process update boundary
config/
  service/      editable service appsettings
  tray/         editable tray appsettings
data/           durable runtime data such as SQLite files
logs/           service, tray, updater, and diagnostics output
updates/
  staging/      candidate bundles/manifests for future update flow
install/        install, uninstall, and smoke-test scripts
manifest.json   bundle version, layout, entry points, files, checksums
```

Packaged service runs should point at the external paths with the deployment CLI aliases:

```powershell
Trackside.Service.exe --config-root <install>\config --install-mode Service --install-root <install> --data-path <install>\data --logs-path <install>\logs --updates-path <install>\updates --bundle-version <version> --manifest-path <install>\manifest.json
```

## Install And Uninstall

Preview install actions without elevation:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File <bundle>\install\Install-Trackside.ps1 -DryRun
```

Install as a Windows Service from an elevated PowerShell session:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File <bundle>\install\Install-Trackside.ps1 -InstallRoot "C:\Program Files\Gearbox Trackside"
Start-Service Trackside
```

The install script verifies `manifest.json` with `Trackside.Updater` before copying, preserves existing configuration unless `-OverwriteConfig` is supplied, creates `data`, `logs`, and `updates\staging`, installs or updates the `Trackside` Windows Service, configures restart-on-failure, and writes a current-user Run entry for the tray companion.

Preview uninstall actions:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File <bundle>\install\Uninstall-Trackside.ps1 -DryRun
```

Uninstall removes the service and tray auto-start entry. It keeps files/data by default; use `-RemoveFiles` and `-RemoveData` deliberately.

## Manifest And Updater Boundary

`manifest.json` is schema version 1. It includes:

* `appVersion`, `bundleVersion`, `minimumCompatibleVersion`, and `runtime`;
* install layout paths;
* executable entry points for service, tray, rig agent, and updater;
* one SHA-256 and byte length entry per bundle file.

`Trackside.Updater` is a tiny out-of-process boundary. It can inspect manifests, verify bundle files, and compare current/candidate manifests for a future staff-approved update flow:

```powershell
<bundle>\app\updater\Trackside.Updater.exe inspect --manifest <bundle>\manifest.json
<bundle>\app\updater\Trackside.Updater.exe verify --manifest <bundle>\manifest.json --root <bundle>
<bundle>\app\updater\Trackside.Updater.exe plan --current <installed>\manifest.json --candidate <bundle>\manifest.json
```

It does not replace running binaries yet. Future update application should remain outside `Trackside.Service` so the service is not responsible for overwriting itself.

## Smoke Test

Run the bundle smoke test after packaging:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File <bundle>\install\Invoke-TracksideBundleSmoke.ps1
```

The smoke test verifies the manifest with `Trackside.Updater`, starts `Trackside.Service` from the bundle in console/fixture mode on `http://127.0.0.1:18877`, checks `/api/health`, and fetches `/api/live-session/current`.

## Health Fields

`/api/health` now exposes deployment/update placeholders:

* `appVersion`, `bundleVersion`, `installMode`, and `serviceState`;
* `installRoot`, `configPath`, `dataPath`, `logsPath`, `updatesPath`, and `manifestPath`;
* `update.status`, `update.channel`, `update.manifestUrlConfigured`, `update.candidateManifestPath`, and `update.minimumCompatibleVersion`.

The current update status defaults to `NotConfigured`. Remote manifest checks and staff-approved update application are later features.
