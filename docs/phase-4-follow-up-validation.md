# Phase 4 Follow-up Validation

Date: 2026-07-18

Implementation commits:

- `1d738d0` — Status, Advanced diagnostics, and dedicated Tracker administration.
- `2c4c783` — Sessions workspace polish and protected historical-result deletion.
- `974b76d` — Selected-track geometry lookup, preview, localization, and endpoint tests.
- `dda352f` — Advanced source consolidation and pinned Status diagnostics.
- `a45b7b3` — Safe stored/in-progress outline deletion and endpoint tests.
- `a7ab8e2` — Active-only Tracker polling, action guidance, and delete UI.
- `6c24602` — Independent multi-result tabs with integrated close controls.
- `51c2fa0` — Semantic track-evidence gating and permanent catalog-row deletion.
- `e8229d4` — Live Sessions polling, Tracker detail wrapping, and aligned result-tab rows.

## Automated validation

The following checks ran without restoring or installing packages:

- `dotnet test services/trackside/Trackside.slnx --no-restore`
  - Result: 115 passed, 0 failed, 0 skipped.
- `npm --prefix web/kiosk test`
  - Result: 20 passed, 0 failed.
- `npm --prefix web/kiosk run build`
  - Result: passed.
- `node --check services/trackside/Trackside.Service/wwwroot/configuration.js`
  - Result: passed.
- `git diff --check`
  - Result: passed.

The selected-track endpoint tests cover missing names, unknown catalog entries, known catalog geometry, delete responses, and authorization metadata. Recorder tests verify deletion removes persisted geometry, resets recording/candidate state, and publishes an unavailable frame. Existing tests continue to cover geometry state and active/recent session deletion protection.

## Browser viewport validation

The actual `configuration.html` markup and repository `styles.css` were loaded in an integrated Chromium browser. Representative test content was inserted for Status, Advanced, Tracker, and an opened Sessions result. This kept the check independent of rFactor 2 and avoided creating runtime service data.

The following viewport sizes were exercised:

| Surface | 1920×1080 | 1366×768 | 1024×768 |
| --- | --- | --- | --- |
| Status | No page-level horizontal overflow | No page-level horizontal overflow | No page-level horizontal overflow |
| Advanced | No page-level horizontal overflow | No page-level horizontal overflow | No page-level horizontal overflow |
| Tracker | No page-level horizontal overflow; table fits | No page-level horizontal overflow; table fits | No page-level horizontal overflow; wide diagnostics scroll inside the table frame |
| Sessions | No page-level horizontal overflow; compact summary fits | No page-level horizontal overflow; compact summary fits | No page-level horizontal overflow; compact summary and light correction form fit |

At 1024px the Tracker outline panel, state facts, progress values, and table remained readable. The Sessions result tab group, six summary metrics, participant actions, and light correction input remained within the workspace. After the final Source-tab consolidation, the reduced admin navigation fit on one row at this width.

The later Advanced/Status consolidation was checked again at 1366×768, 1024×768, and 600×800. The paired source checkboxes and polling-rate fields remained side by side at the venue-oriented widths and stacked at the existing 620px narrow-layout breakpoint. No page-level horizontal overflow occurred. Raw status appeared only in the collapsible Status diagnostic, not in Advanced.

The final Tracker and Sessions changes were exercised with mocked API test data in the real admin JavaScript. Tracker catalog values changed once per second while Tracker was active; 32 catalog refreshes produced only one current-geometry/source probe. Delete Outline reset recording, samples, candidate coverage, row state, and preview detail without deleting sessions. Two simultaneous result tabs preserved independent Manage Results state, switched to the correct cached detail, exposed exactly one selected ARIA tab, and restored focus to the neighboring tab after close. Five-tab layout checks at 1920, 1366, and 1024 used a single horizontally scrolling result rail without page-level overflow.

The follow-up placeholder/layout/live-session fixes were also browser-checked with synthetic API data. A live session that became active after the Sessions page loaded appeared without pressing Refresh Sessions, and polling stopped immediately after leaving the tab. At 1366px, a long Tracker detail wrapped inside a fixed 260px column while the 220px action column stayed inside the table frame. At 1024px, overview and result-tab rows had a measured 0px vertical gap; active and inactive result tabs shared the same top/bottom alignment against the workspace canvas.

## Localization validation

The browser executed the admin localization code and switched from English to Dutch. The affected Dutch labels rendered as:

- Status tab: `Status`
- Advanced tab: `Geavanceerd`
- Tracker title: `Drivertracker`
- Refresh-rate field: `Verversingssnelheid client (Hz)`
- Samples column/fact: `Meetpunten`
- Detail column/fact: `Toelichting`
- Selected geometry title: `Geometrie van geselecteerde baan`

English rendering was checked before switching languages, including `Selected Track Geometry`, the selected-track outline explanation, Status, Advanced, and Sessions helper copy.

## Functional completion checks

- Status presents live service, source, persistence/results, and Tracker state for venue staff. Raw status JSON is available in a subordinate collapsible diagnostic; automatic refresh pauses while that payload is open so it can be inspected or copied reliably.
- Advanced contains the live-source configuration, shared-memory discovery, and protected read-only fixture path. Source selection, checkbox behavior, and polling rates are visually grouped, while the removed Source-tab preference migrates to Advanced for existing browsers.
- The redundant alias JSON editor is absent while existing aliases continue to round-trip safely.
- Tracker settings and actions are consolidated, and unavailable, recording, partial, and complete states remain distinct.
- Selecting any catalog track loads its stored geometry through the authenticated, catalog-validated admin endpoint; the preview is no longer limited to the current live track.
- Improve Outline retains existing geometry and averages more completed laps; Start Over clears geometry before fresh recording. Delete Outline removes stored/in-progress geometry without touching sessions or laps. Catalog polling runs only while the visible Tracker tab is active and does not repeatedly probe the current source.
- Track catalog entries are created only after active GreenFlag scoring provides finite lap distance and at least one valid on-track driver/lap context. Placeholder display names therefore cannot create geometry rows. Deletion removes the persisted file and in-memory catalog row, so legacy placeholders stay gone unless fresh semantic track evidence is later observed.
- Sessions supports multiple coherent closable result tabs. Each tab preserves its own driver selection, Compare Drivers visibility, and Manage Results state. The close icon is part of the tab surface, keyboard navigation and focus restoration follow tab semantics, and overflow remains inside a single result rail.
- The Sessions workspace polls its lightweight live snapshot while authenticated, visible, and active. Live cards update immediately; persisted summaries reconcile only on transitions or during a short bounded wait for Open Results, without repeatedly refreshing open result-tab details.
- Older Results exposes direct deletion only when the result is neither active nor inside the protected three-hour recent window. The API independently enforces the same safeguard and confirmation remains in the browser flow.

## Limitation

Live rFactor 2 behavior was not rerun for this operator-UI follow-up. Fixture and synthetic test data were used, and the Tracker continues to display the Phase 4 venue-validation limitation explicitly.
