# Vendor Directory

This directory contains third-party vendor artifacts and source snapshots consumed by Trackside.

The current layout is organized by artifact type rather than by package:

- `src/` contains source snapshots and package metadata for vendored plugins.
- `plugin/` contains shipped plugin binaries.
- `tools/` contains supporting utilities and monitoring tools.

## Current contents

- `src/rf2-shared-memory-map/`
  - Source snapshot and documentation for the rFactor 2 shared-memory map plugin.
  - Original upstream docs are preserved in `src/readme.txt` and `src/rf2-shared-memory-map/README.md`.
- `plugin/rf2smmp/rFactor2SharedMemoryMapPlugin64.dll`
  - Plugin binary used by Trackside for rFactor 2 shared-memory integration.
- `tools/rf2smmp_monitor/`
  - Monitor utility for inspecting the rF2 shared memory stream.

- `src/rf2-autocam/`
  - Source snapshot for the rF2 autocam plugin.
- `plugin/rf2autocam/`
  - Autocam plugin binary.

## Usage

These files are maintained as vendored references and are not built as part of the normal Trackside application solution.

Use this folder when:

- inspecting or referencing third-party plugin source
- keeping a reproducible snapshot of upstream vendor artifacts
- validating rFactor 2 integration behavior against existing plugin outputs

## Notes

- Trackside does not claim ownership of the upstream plugins.
- Treat `vendor/` content as dependency snapshots, not active application source.
- If runtime logs are present under `tools/`, consider whether they should remain tracked or be excluded from version control.
