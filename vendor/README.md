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
  - Verified on July 16, 2026 against the DLL extracted from the 3.7.15.1 archive linked by the upstream README: both files have 86,016 bytes, SHA-256 `9D98D77B767812DCA5AFEB6663F486B3AD5BE090C3E10783F36BC73A470AB5E6`, and PE linker timestamp March 25, 2023 12:37:19 UTC. The archive source is `https://www.mediafire.com/file/s6ojcr9zrs6q9ls/rf2_sm_tools_3.7.15.1.zip/file`.
  - PE machine `0x8664` confirms that it is x64.
  - Dynamically imports the Visual C++ 2013 x64 runtime (`MSVCR120.dll`); modern .NET runtimes do not provide this dependency.
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
- A venue incident confirmed that malformed active-profile `CustomPluginVariables.JSON` after a PC crash can make rFactor 2 Dedicated exit silently. Preserve and validate that whole file before attributing startup failure to a plugin DLL; letting rFactor 2 regenerate the malformed file resolved the incident.
- Keep `DebugISIInternals` disabled for the shipped shared-memory plugin. The source snapshot closes cached output streams without clearing them and can reuse those closed pointers on later callbacks.
- If runtime logs are present under `tools/`, consider whether they should remain tracked or be excluded from version control.
