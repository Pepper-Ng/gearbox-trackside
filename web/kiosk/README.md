# web/kiosk

React/Vite kiosk shell for Trackside display screens.

The ASP.NET Core service serves a static fallback kiosk from `services/trackside/src/Trackside.Service/wwwroot` for packaged operation. This folder is the frontend development workspace: React components, typed API contracts, SignalR client wiring, and Vite build tooling.

## Commands

Run from the repository root:

```powershell
npm --prefix web\kiosk install
npm --prefix web\kiosk run dev
npm --prefix web\kiosk run build
npm --prefix web\kiosk test
```

The Vite dev server proxies `/api` and `/hubs` to `http://127.0.0.1:8877`, so run the backend alongside it:

```powershell
dotnet run --project services\trackside\src\Trackside.Service -- --console --source fixture --fixture Fixtures\mock-live-session.json
```

## Extension Points

- `src/tracksideApi.ts` defines the browser-facing REST and SignalR contracts.
- `src/ui/App.tsx` is only a basic fixture display, not the final board design.
- Keep initial load through `/api/live-session/current`; use SignalR updates only after the recovery snapshot succeeds.
