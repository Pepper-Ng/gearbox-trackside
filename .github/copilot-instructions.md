# Trackside Coding Instructions

These instructions apply to the Gearbox Trackside workspace. Keep changes consistent with the current .NET/ASP.NET Core service, React kiosk, and Windows venue deployment shape.

## System Shape And Boundaries

- Trackside is a local venue system: an ASP.NET Core service hosts APIs, SignalR hubs, admin pages, and the static React kiosk; optional tray, rig-agent, and updater executables stay separate from the core host.
- Keep the Clean Architecture split intact: `Trackside.Service` composes endpoints/workers/UI hosting, `Trackside.Application` owns contracts/options, `Trackside.Infrastructure` owns adapters such as SQLite and rFactor 2 shared memory, and `Trackside.Domain` stays pure.
- Preserve the `ILiveSessionSource` boundary. Kiosk/admin clients should not need to know whether data came from fixtures, recorded data, or shared memory.
- Keep shared-memory parsing and polling guarded behind the existing reader/parser/resolver types. Avoid putting native or hot-loop behavior directly in endpoints or UI code.
- Persistence belongs behind `ITracksideStore`. Live publishing, source alias resolution, admin endpoints, and leaderboard builders should depend on store contracts rather than SQLite details.
- Shared-memory streams should be projected once into small live-data frames and then consumed by registered modules. Avoid letting UI, endpoints, or unrelated services repeatedly read raw maps.
- For browser work, keep feed/API helpers separate from presentation components where practical. Kiosk screens are operational displays, not marketing pages.

## Live UI And Admin Interaction

- Prefer pages that feel live and app-like. Browser views should update themselves; needing F5/Ctrl+F5 to see ordinary backend changes should be rare.
- Prefer direct push/update mechanisms over polling where practical. SignalR is already available and should be used for near-real-time backend-originated changes when it fits the workflow.
- Polling is acceptable for simple admin/configuration status where push would add needless machinery; for most configuration surfaces, around 1 second is plenty.
- Prefer implicit saving for configuration and routine admin edits: update on change, blur, or short debounce instead of requiring a form submit.
- Add explicit Save/Submit buttons only when they improve clarity, match user expectation, batch related fields intentionally, or guard a potentially surprising action.
- Keep status text and table diagnostics live enough that staff can tell whether a setting, source, or recording process is waiting, saving, ready, or failing.

## Comments And Documentation

- Prefer slightly expressive comments for non-obvious behavior. A short comment is welcome when it explains why code exists, why a threshold is chosen, why data is intentionally delayed, or why a fallback is safe.
- Do not comment every line. Avoid comments that merely repeat the code, such as "set the value" or "loop over items".
- Public C# types and members should keep useful XML documentation. Private helper comments should be brief and focused on intent or venue/runtime constraints.
- For tracker geometry, shared-memory polling, persistence throttling, and kiosk refresh behavior, add concise comments around safety gates, fallback behavior, candidate/temporary state, and resource-pressure decisions.
- When changing defaults or operational behavior, update nearby comments/docs so admin UI, README configuration notes, and code remain aligned.

## Runtime And Resource Practices

- Treat venue PCs as constrained Windows machines that may already be running rFactor 2 Dedicated Server. Avoid unnecessary high-frequency loops, repeated native probing, excessive browser rerenders, and repeated unchanged SQLite writes.
- Keep browser refresh rates configurable. Safer defaults are preferred, but admin settings should still allow higher rates when hardware can handle them.
- Missing shared-memory maps are normal while rFactor 2 or the plugin is not publishing. Polling/discovery should back off or be operator-controlled instead of hammering Windows APIs.
- High-frequency loops are allowed when the domain needs them, but keep them in dedicated, cancellable components such as a worker, polling loop, thread, or process. Design those components so they can fail, stop, or be respawned without taking down unrelated UI or admin workflows.
- Keep hot loops off request handlers and UI render paths. Push compact snapshots or frames outward; do not make every browser/client interaction redo native probing or heavy parsing.
- Prefer bounded caches, explicit pruning, backoff, cancellation tokens, and resource-pressure tests around long-running venue features.

## Validation

- For .NET changes, run `dotnet test services/trackside/Trackside.slnx` when possible. If a local Debug assembly is locked by a previous PowerShell reflection session, validate with `-c Release` and mention the lock.
- For kiosk changes, run `npm --prefix web/kiosk test` and `npm --prefix web/kiosk run build`.
- Run `git diff --check` before finishing edits.
- Do not commit or push unless explicitly asked.