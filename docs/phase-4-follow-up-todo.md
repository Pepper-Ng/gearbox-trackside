# Phase 4 Follow-up TODO

These are the remaining operator-facing UI improvements identified during the Phase 4 venue run. They are deliberately separated from the completed live-data and venue-readiness validation. Do not use this list to reopen the already-verified rFactor 2 integration work.

## Configuration and status

- **Translate the Status tab title.** Replace the visible Dutch fallback `AdvancedStatus.Title` with the intended Dutch label.
- **Rework advanced status placement.** The Status tab should show information that helps a venue owner understand current operation. Move the diagnostic advanced-status JSON to the Advanced tab, or another clearly diagnostic location, if it is still useful.
- **Remove redundant driver-alias JSON.** The raw alias JSON is unnecessary when Session Setup already provides the supported editing workflow; remove it or replace it with a useful diagnostic.
- **Decide whether Fixture path is operator-facing.** If fixture mode is only a development/fallback concern and venue staff should never change it, hide it from the normal Advanced UI. If it must remain visible, explain when it is relevant and protect it from accidental changes.

## Driver Tracker

- **Give Tracker its own admin tab.** Move the Driver Tracker controls out of the Leaderboards area so the ownership and purpose are obvious.
- **Use the Tracker tab for geometry state.** Show selected-track outlines, recording progress, and the state of live outline generation where available.
- **Keep related tracker settings together.** Place tracker refresh, recording, restart/reset, and diagnostic controls in the same Tracker surface rather than distributing them across unrelated tabs.
- **Keep the Phase 4 limitation visible.** Track-outline generation was not validated during the venue run; the UI should distinguish unavailable, recording, partial, and complete geometry instead of implying that every selected track has a finished outline.

## Sessions workspace

- **Integrate closable result tabs.** An opened old result, or any other closable session tab, should render as one coherent tab with its close affordance inside the tab styling rather than as a normal tab with a detached close button beside it.
- **Replace the technical helper copy.** Remove or rewrite the text about normal workspace mode hiding result management. Use a short description of what staff can see and do on the page.
- **Make driver editing discoverable.** In Manage Results mode, change the per-driver `Inspect` action to `Edit` (or an equally explicit label) so staff understand that it opens editable driver results.
- **Match the light workspace styling.** Restyle form fields on the Sessions page with the same light surfaces, borders, focus states, and text contrast as the surrounding white UI; do not leave dark inputs in the light workspace.
- **Compact the session summary.** Reduce the vertical height of the Track, Session, and Top Driver information and arrange the summary horizontally or in a compact responsive grid.
- **Rewrite the archive description.** Replace the reasoning-heavy sentence about earlier results staying grouped with a concise description of the content shown on the page. Widen the text container so the final word does not wrap unnecessarily.
- **Add safe deletion for older results.** Add a delete action to the Older Results list so staff do not have to open a result, enter edit mode, and delete it. Keep deletion unavailable for In Progress and Recently Completed sessions; preserve the existing safeguards and confirmation for destructive actions.

## Completion check

- Verify the affected labels in Dutch and English.
- Verify the Status, Advanced, Tracker, and Sessions surfaces at the venue-oriented desktop/kiosk widths.
- Confirm that moving or hiding diagnostics does not remove access to source, persistence, or tracker troubleshooting information for administrators.
- Confirm the Older Results delete action cannot target active or recently completed sessions.
