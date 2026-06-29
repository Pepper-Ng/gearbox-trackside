# plugin

C++ camera director plugin (Internals Plugin SDK).

This is reserved for the optional camera/replay work in implementation-plan.md
Phase 10. Keep this plugin's scope limited to camera/replay control only —
everything else (parsing, storage, charts, printing) belongs in /services, per
the "minimal plugin surface" recommendation in the plan.

Suggested layout once work starts:

- sdk/        vendored Studio 397 Internals Plugin SDK headers
- src/        plugin source (camera cycling, override-file reader, incident detection)
- build/      (gitignored) build output
