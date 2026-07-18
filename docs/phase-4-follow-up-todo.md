# Phase 4 Follow-up TODO

These operator-facing UI improvements were identified during the Phase 4 venue run and are now complete. They remain deliberately separated from the completed live-data and venue-readiness validation. Do not use this list to reopen the already-verified rFactor 2 integration work.

## Configuration and status

- [x] **Translate the Status tab title.** Replace the visible Dutch fallback `AdvancedStatus.Title` with the intended Dutch label.
- [x] **Rework advanced status placement.** The Status tab should show information that helps a venue owner understand current operation. Move the diagnostic advanced-status JSON to the Advanced tab, or another clearly diagnostic location, if it is still useful.
- [x] **Remove redundant driver-alias JSON.** The raw alias JSON is unnecessary when Session Setup already provides the supported editing workflow; remove it or replace it with a useful diagnostic.
- [x] **Decide whether Fixture path is operator-facing.** If fixture mode is only a development/fallback concern and venue staff should never change it, hide it from the normal Advanced UI. If it must remain visible, explain when it is relevant and protect it from accidental changes.

## Driver Tracker

- [x] **Give Tracker its own admin tab.** Move the Driver Tracker controls out of the Leaderboards area so the ownership and purpose are obvious.
- [x] **Use the Tracker tab for geometry state.** Show selected-track outlines, recording progress, and the state of live outline generation where available.
- [x] **Keep related tracker settings together.** Place tracker refresh, recording, restart/reset, and diagnostic controls in the same Tracker surface rather than distributing them across unrelated tabs.
- [x] **Keep the Phase 4 limitation visible.** Track-outline generation was not validated during the venue run; the UI should distinguish unavailable, recording, partial, and complete geometry instead of implying that every selected track has a finished outline.

## Sessions workspace

- [x] **Integrate closable result tabs.** An opened old result, or any other closable session tab, should render as one coherent tab with its close affordance inside the tab styling rather than as a normal tab with a detached close button beside it.
- [x] **Replace the technical helper copy.** Remove or rewrite the text about normal workspace mode hiding result management. Use a short description of what staff can see and do on the page.
- [x] **Make driver editing discoverable.** In Manage Results mode, change the per-driver `Inspect` action to `Edit` (or an equally explicit label) so staff understand that it opens editable driver results.
- [x] **Match the light workspace styling.** Restyle form fields on the Sessions page with the same light surfaces, borders, focus states, and text contrast as the surrounding white UI; do not leave dark inputs in the light workspace.
- [x] **Compact the session summary.** Reduce the vertical height of the Track, Session, and Top Driver information and arrange the summary horizontally or in a compact responsive grid.
- [x] **Rewrite the archive description.** Replace the reasoning-heavy sentence about earlier results staying grouped with a concise description of the content shown on the page. Widen the text container so the final word does not wrap unnecessarily.
- [x] **Add safe deletion for older results.** Add a delete action to the Older Results list so staff do not have to open a result, enter edit mode, and delete it. Keep deletion unavailable for In Progress and Recently Completed sessions; preserve the existing safeguards and confirmation for destructive actions.

## Completion check

- [x] Verify the affected labels in Dutch and English.
- [x] Verify the Status, Advanced, Tracker, and Sessions surfaces at the venue-oriented desktop/kiosk widths.
- [x] Confirm that moving or hiding diagnostics does not remove access to source, persistence, or tracker troubleshooting information for administrators.
- [x] Confirm the Older Results delete action cannot target active or recently completed sessions.

## Completion evidence

- Detailed automated, viewport, localization, and safeguard evidence is recorded in [phase-4-follow-up-validation.md](phase-4-follow-up-validation.md).
- Implementation is grouped in commits `7156f33`, `264c0e4`, and `24d1cc8`.
- The .NET solution passes all 96 tests without restoring or installing packages.
- The admin JavaScript passes its syntax check, and `git diff --check` reports no whitespace errors.
- Status, Advanced, Tracker, and Sessions were browser-checked with test content at 1920×1080, 1366×768, and 1024×768. The surfaces stayed within the viewport; wide Tracker diagnostics scroll inside their table frame at the narrowest checked width.
- English and Dutch rendering was exercised in the browser, including Status, Advanced, selected-track geometry, refresh rate, meetpunten, and tracker detail labels.
- Selected-track geometry is retrieved through an authenticated, catalog-validated endpoint. Endpoint response, authorization metadata, route contract, geometry state, and deletion safeguards are covered by automated tests.
- Live rFactor 2 behavior was not rerun for this UI follow-up; fixture/test data was used as intended, and the venue-validation limitation remains visible in the Tracker UI.
