# Session Results Workspace Refactor Plan

This page is not primarily about sessions. It is about staff interacting with customers after a session.

Trackside stores and queries session data, but the admin surface should behave like an operational workspace backed by session data rather than a raw persistence browser. The point of the page is to help staff answer customer questions, inspect recent results, compare drivers, open follow-up tools such as telemetry, and only rarely correct bad data.

## Preliminary Implementation Brief

This document is sufficient to implement a preliminary releasable version of the session results workspace, provided the first delivery stays disciplined about scope and modularity.

### Delivery target

The first release should produce a staff-usable workspace that:

* separates In Progress, Recently Completed, and Older Results;
* makes recent completed results the primary workflow surface;
* lets staff open one session and inspect driver-centric results without flooding the page with correction controls;
* exposes Compare Drivers and telemetry/report entry points in the selected-session workflow;
* keeps result-management actions available, but secondary and intentionally invoked.

This first release does not need to solve every long-term archive or analytics workflow. It only needs to be operationally credible, internally consistent, and safe to evolve.

### Working assumptions for the first release

Unless testing proves otherwise, the first implementation may assume:

* the current persistence and session-detail model are good enough to drive the first selected-session experience;
* existing APIs can support the first pass, with only small summary or filtering additions if the frontend becomes too awkward;
* selecting a session should preserve list context while revealing detail, but the reveal mechanism itself is not fixed yet;
* compare-drivers within one session is the only comparison mode needed in the first release;
* Manage Results remains a secondary workflow and does not need to define a durable review or approval state.

These assumptions are intentionally provisional. The implementation should be structured so that any one of them can be revised after testing without forcing a page rewrite.

### Non-locking implementation posture

The implementation should avoid locking in unresolved interaction choices too early.

Specifically:

* do not hard-code the selected-session reveal to one visual pattern if the state model can remain layout-agnostic;
* do not couple list rendering to one exact grouping or filter layout if the data model can represent sections generically;
* do not couple result-management controls to the default detail render path;
* do not bake telemetry/report behavior into the session-results layout beyond entry points and routing hooks.

### Required modular boundaries

The code should be organized so that layout can change without rewriting the underlying session workspace logic.

At minimum, keep these layers distinct:

* session workspace state and view-model shaping;
* data access and API adapters;
* list/section rendering;
* selected-session detail rendering;
* result-management rendering and actions;
* formatting helpers for time, rank, status, and labels.

The goal is a clear layout layer on top of the model, with transformation logic isolated enough that grouping, reveal behavior, and detail composition can be adjusted after testing.

### Preliminary release acceptance check

The first implementation should be considered releasable when:

1. Staff can distinguish active sessions, recent completed sessions, and older results at a glance.
2. The default view no longer exposes participant and lap correction controls by default.
3. Selecting a session reveals driver-centric result detail without disrupting the list.
4. Compare Drivers and telemetry/report entry points are present in the selected-session workflow.
5. Manage Results actions remain available, but clearly secondary.
6. The page structure can be adjusted after testing without rewriting the session data model or API contracts wholesale.

## 1. Product Premise

The current Sessions tab mixes three concerns into one default surface:

* browsing stored historical sessions;
* reviewing recent results with customers;
* correcting exceptions in session, participant, or lap data.

That creates the wrong default experience. The default experience should optimize for the normal venue flow:

1. A session completes.
2. Staff and customers want to see what happened.
3. Staff may compare drivers or open telemetry/report tools.
4. Only if something is clearly wrong should staff move into an explicit result-management path.

The page should therefore be treated as a session results workspace, not as a session history editor.

## 2. Core Principles

### Staff-first operational framing

The page should help staff answer practical post-session questions such as:

* Who was fastest?
* Which lap mattered?
* Did the driver improve?
* How do two drivers compare?
* Can I open the telemetry/report flow for this result?

### Normal flow first, exception flow second

Correction and exclusion features remain important, but they are secondary. They should not dominate the default view.

### Recent results deserve focus

The newest completed sessions should be the center of attention because they are the ones customers and staff care about immediately after driving.

### Historical scale must not pollute the default view

As stored history grows, the page must stay operationally readable. Older results belong in a clearly secondary area with grouping, search, and filtering.

### All operational actions should be repeatable and never consume a session.

Opening a result, comparing drivers, launching telemetry, or revisiting an older session should not make that session disappear, become exhausted, or move through an approval workflow just because staff used it.

### No implied approval workflow

The page should not frame staff interaction as formally approving or reviewing sessions. Trackside should assume sessions are usable by default unless staff decides something needs to be managed or corrected.

## 3. Primary Jobs

The page should optimize for these jobs, in this order:

1. Show recent completed results in a way staff can use with customers.
2. Provide quick operational actions such as compare drivers and open telemetry/report.
3. Offer access to older results without overwhelming the recent-results workflow.
4. Allow rare corrections through an explicit management path.

It should not default to exposing raw persistence details, low-level correction fields, or all stored lap rows across every participant.

## 4. Information Architecture

The page should be reorganized into three time-horizon sections.

### In Progress

This is a small separate panel above the completed-session workspace.

Purpose:

* give staff immediate visibility into currently active sessions when useful;
* allow rare live interventions when needed.

Constraints:

* visually distinct from completed sessions;
* clearly secondary to completed results;
* compact, not a full live-session console.

Allowed live actions for the first refactor:

* correct display name;
* invalidate a lap.

### Recently Completed

This is the primary section and should always be expanded.

Default rule:

* show up to 4 completed sessions within the last 3 hours.

Purpose:

* support the normal post-session staff and customer interaction flow;
* keep the most relevant results immediately accessible;
* avoid any notion of consuming or approving a session.

### Older Results

This is the secondary history-browsing section.

Default organization:

* Earlier Today
* Yesterday
* Older

These groups should be collapsible and filterable so the page still scales as history grows.

## 5. Default List Behavior

The default list should be compact and readable. It should show only the fields staff need to identify and open a result quickly.

Recommended default row fields:

* track;
* session type;
* ended at or last seen;
* driver count;
* best lap or top result summary.

Fields that should not appear by default in the list:

* rig/setup identity as a main field;
* vehicle;
* raw validity counters expressed in internal wording;
* correction inputs;
* lap flag fields;
* destructive actions shown inline.

The list should support:

* newest-first ordering;
* pagination or incremental loading for older results;
* text search;
* track filters;
* session-type filters;
* status filters;
* saved views.

## 6. Session Selection And Detail Reveal

Selecting a session should reveal detailed information without disrupting the list.

The implementation is intentionally not locked to one layout. A side panel is an excellent solution, but it is not the only acceptable solution. A nested detail surface or mini-application can also work as long as the session list remains intact and readable while details are revealed.

First-version detail behavior:

* default to driver results summary first;
* keep the detail surface as one simple, scrollable workflow rather than a large nested editor;
* do not expose all participant and lap correction controls immediately on selection.

## 7. Driver Results Detail

The first visible result layer should be one compact row per driver.

Default driver-row fields:

* display name;
* position or session result;
* best lap;
* lap count.

Secondary data can appear only after further reveal or expansion:

* rig name;
* last lap;
* gap or delta to leader;
* telemetry/report shortcut;
* correction badge when edits exist.

Lap-level data should not appear for every participant by default. It should appear only after staff drills into a driver.

## 8. Operational Actions

The normal completed-session workflow should make these actions one click away:

* open result detail;
* compare drivers;
* open telemetry/report;
* enter result-management mode when needed.

The first comparison mode should be narrow and practical:

* compare drivers within one selected session.

That keeps the first version useful without turning the page into a general analytics tool.

## 9. Result Management Mode

The current idea of a special mode is still correct, but it should not be called Review Mode because that implies approval.

Preferred names:

* Edit Results
* Manage Results

Purpose:

* separate rare exception-handling tasks from the normal customer-facing result workflow;
* keep dangerous or low-frequency actions out of the default view;
* avoid staff accidentally editing stored history while just browsing.

Result-management mode should reveal correction controls and other exceptional actions only after the operator intentionally enters that mode.

Capabilities that must remain available in this mode:

* exclude whole session from leaderboards/history;
* exclude participant from history;
* override display name;
* invalidate a lap;
* delete stored session.

Capabilities that should not dominate the first refactor:

* always-visible inline correction inputs;
* always-visible participant exclusions;
* always-visible lap correction tables;
* manual numeric lap-time override as a default workflow.

Dangerous actions should be hidden under an explicit More or Manage Results affordance rather than shown inline in the list.

## 10. Language And Terminology

The page should use staff language rather than implementation-flavored labels.

Preferred language to anchor the redesign:

* In Progress
* Recently Completed
* Older Results
* Compare Drivers
* Driver Results

Terms that should be removed, renamed, or avoided as primary labels:

* Session History
* Session Detail
* Timed
* Boards
* Correction
* Vehicle as a default visible field

Examples of clearer replacements:

* Counts for Leaderboards instead of Boards
* Driver Results instead of Session Detail
* Manage Results or Edit Results instead of Review or Correction
* Valid Laps or Counted Laps only when that distinction truly matters to the operator

## 11. API And Frontend Implications

The current frontend structure is centered around:

* one flat sessions table;
* one always-visible detail section;
* always-visible participant correction controls;
* always-visible lap correction tables.

That should be refactored into separate UI states.

### Frontend state model

The page should have explicit state for:

* in-progress sessions;
* recently completed sessions;
* older result groups;
* active filters/search/saved view;
* selected session;
* whether Manage Results mode is active.

### Summary versus detail contracts

The page should stop treating the list view and the detail view as the same data surface.

Recommended shape:

* compact session summary contract for lists;
* richer selected-session detail contract for result exploration;
* explicit summary badges such as has corrections or excluded from leaderboards when needed.

### Backend support

The first refactor can reuse most existing APIs, but the shape will improve if the backend can provide:

* compact session summary responses;
* grouped or filterable session queries;
* simple summary flags for exceptional states;
* stable support for selecting one session and then drilling into driver/lap details.

## 12. Implementation Phasing

### Phase A - Restructure The Page

* Replace the single Session History surface with In Progress, Recently Completed, and Older Results sections.
* Remove always-visible session detail from the main page flow.
* Make selecting a session reveal detailed information without disrupting the list.
* Keep the first detail reveal focused on driver results rather than edit controls.

### Phase B - Add Operational Workflow First

* Add clear open-result behavior.
* Add Compare Drivers entry point for one session.
* Add telemetry/report entry point and placeholder integration.
* Hide destructive and corrective actions behind an explicit Manage Results affordance.

### Phase C - Reintroduce Exception Handling Carefully

* Add Manage Results mode.
* Move participant corrections into that mode.
* Move lap invalidation into deeper driver/lap detail rather than default render.
* Keep delete and exclusion actions explicit and confirmed.

### Phase D - Scale History Browsing

* Add grouping for Older Results.
* Add search and filters.
* Add saved views.
* Add pagination or incremental loading if the grouped history still grows too large.

## 13. Non-Goals

The first refactor should not attempt to:

* turn the page into a raw persistence inspector;
* expose every correction mechanism by default;
* build a full telemetry/report UX inside the session results workspace;
* introduce a formal review or approval workflow;
* merge live monitoring and completed-result handling into one undifferentiated table.

## 14. Acceptance Criteria For The Refactor

The refactor is successful when:

1. Staff can immediately see recent completed results without wading through archive rows.
2. The page reads like a customer-facing operational workspace rather than a persistence editor.
3. Selecting a session reveals detailed information without disrupting the list.
4. Driver results are easy to understand before any correction controls are shown.
5. Manage Results functionality exists, but it is clearly secondary and intentionally invoked.
6. Older results remain accessible through grouping, filtering, and search without polluting the default workflow.
