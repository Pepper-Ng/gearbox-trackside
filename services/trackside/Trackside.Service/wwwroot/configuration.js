const statusElement = document.querySelector('#configurationStatus');
const setupPanel = document.querySelector('#setupPanel');
const loginPanel = document.querySelector('#loginPanel');
const dashboardPanel = document.querySelector('#dashboardPanel');
const logoutButton = document.querySelector('#logoutButton');
const setupButton = document.querySelector('#setupButton');
const loginButton = document.querySelector('#loginButton');
const setupUsernameElement = document.querySelector('#setupUsername');
const setupDisplayNameElement = document.querySelector('#setupDisplayName');
const setupPasswordElement = document.querySelector('#setupPassword');
const loginUsernameElement = document.querySelector('#loginUsername');
const loginPasswordElement = document.querySelector('#loginPassword');
const languageSelectElement = document.querySelector('#languageSelect');
const sourceModeElement = document.querySelector('#sourceMode');
const scoringMapNameElement = document.querySelector('#scoringMapName');
const processIdElement = document.querySelector('#processId');
const autoDiscoverElement = document.querySelector('#autoDiscover');
const processNamesElement = document.querySelector('#processNames');
const multipleMapPolicyElement = document.querySelector('#multipleMapPolicy');
const scoringPollHzElement = document.querySelector('#scoringPollHz');
const telemetryEnabledElement = document.querySelector('#telemetryEnabled');
const telemetryPollHzElement = document.querySelector('#telemetryPollHz');
const refreshElement = document.querySelector('#refreshDiscovery');
const discoveryStatusElement = document.querySelector('#discoveryStatus');
const candidateMapsElement = document.querySelector('#candidateMaps');
const discoveredMapsElement = document.querySelector('#discoveredMaps');
const ambiguousMapsElement = document.querySelector('#ambiguousMaps');
const adminUsersElement = document.querySelector('#adminUsers');
const sessionsStatusElement = document.querySelector('#sessionsStatus');
const sessionOverviewLiveTabButtonElement = document.querySelector('#sessionOverviewLiveTabButton');
const sessionOverviewRecentTabButtonElement = document.querySelector('#sessionOverviewRecentTabButton');
const sessionOverviewOlderTabButtonElement = document.querySelector('#sessionOverviewOlderTabButton');
const sessionOverviewLivePanelElement = document.querySelector('#sessionOverviewLivePanel');
const sessionOverviewRecentPanelElement = document.querySelector('#sessionOverviewRecentPanel');
const sessionOverviewOlderPanelElement = document.querySelector('#sessionOverviewOlderPanel');
const sessionWorkspaceUtilityRowElement = document.querySelector('#sessionWorkspaceUtilityRow');
const liveSessionSurfaceElement = document.querySelector('#liveSessionSurface');
const recentSessionsSurfaceElement = document.querySelector('#recentSessionsSurface');
const olderSessionsSurfaceElement = document.querySelector('#olderSessionsSurface');
const openSessionResultTabsRailElement = document.querySelector('#openSessionResultTabsRail');
const selectedSessionPanelElement = document.querySelector('#selectedSessionPanel');
const selectedSessionTitleElement = document.querySelector('#selectedSessionTitle');
const selectedSessionSummaryElement = document.querySelector('#selectedSessionSummary');
const selectedSessionMetricsElement = document.querySelector('#selectedSessionMetrics');
const sessionParticipantRowsElement = document.querySelector('#sessionParticipantRows');
const sessionParticipantDetailSurfaceElement = document.querySelector('#sessionParticipantDetailSurface');
const sessionCompareSurfaceElement = document.querySelector('#sessionCompareSurface');
const compareDriversButton = document.querySelector('#compareDriversButton');
const openTelemetryButton = document.querySelector('#openTelemetryButton');
const toggleManageResultsButton = document.querySelector('#toggleManageResultsButton');
const refreshSessionsButton = document.querySelector('#refreshSessions');
const deleteEmptySessionsButton = document.querySelector('#deleteEmptySessions');
const sessionSetupRowsElement = document.querySelector('#sessionSetupRows');
const addSetupRowButton = document.querySelector('#addSetupRow');
const clearSessionSetupButton = document.querySelector('#clearSessionSetup');
const profileDisplayNameElement = document.querySelector('#profileDisplayName');
const profileEmailElement = document.querySelector('#profileEmail');
const profileNotesElement = document.querySelector('#profileNotes');
const createProfileButton = document.querySelector('#createProfile');
const monthlyTrackStatusElement = document.querySelector('#monthlyTrackStatus');
const monthlyTrackNameElement = document.querySelector('#monthlyTrackName');
const monthlyTrackReasonElement = document.querySelector('#monthlyTrackReason');
const kioskDisplayModeElement = document.querySelector('#kioskDisplayMode');
const saveKioskDisplayModeButton = document.querySelector('#saveKioskDisplayMode');
const driverTrackerClientRefreshHzElement = document.querySelector('#driverTrackerClientRefreshHz');
const driverTrackerGeometryRecordingLapsElement = document.querySelector('#driverTrackerGeometryRecordingLaps');
const saveDriverTrackerSettingsButton = document.querySelector('#saveDriverTrackerSettings');
const refreshDriverTrackerTracksButton = document.querySelector('#refreshDriverTrackerTracks');
const driverTrackerTrackRowsElement = document.querySelector('#driverTrackerTrackRows');
const driverTrackerSelectionHintElement = document.querySelector('#driverTrackerSelectionHint');
const driverTrackerGeometryFactsElement = document.querySelector('#driverTrackerGeometryFacts');
const driverTrackerOutlinePolylineElement = document.querySelector('#driverTrackerOutlinePolyline');
const driverTrackerOutlineMessageElement = document.querySelector('#driverTrackerOutlineMessage');
const setMonthlyTrackButton = document.querySelector('#setMonthlyTrack');
const resetMonthlyTrackButton = document.querySelector('#resetMonthlyTrack');
const monthlyBestLapsElement = document.querySelector('#monthlyBestLaps');
const retentionCleanupStatusElement = document.querySelector('#retentionCleanupStatus');
const runRetentionCleanupButton = document.querySelector('#runRetentionCleanup');
const newAdminUsernameElement = document.querySelector('#newAdminUsername');
const newAdminDisplayNameElement = document.querySelector('#newAdminDisplayName');
const newAdminPasswordElement = document.querySelector('#newAdminPassword');
const createAdminButton = document.querySelector('#createAdminButton');
const passwordUsernameElement = document.querySelector('#passwordUsername');
const newPasswordElement = document.querySelector('#newPassword');
const changePasswordButton = document.querySelector('#changePasswordButton');
const statusUpdatedAtElement = document.querySelector('#statusUpdatedAt');
const statusServiceStateElement = document.querySelector('#statusServiceState');
const statusSourceModeElement = document.querySelector('#statusSourceMode');
const statusSourceStateElement = document.querySelector('#statusSourceState');
const statusCurrentTrackElement = document.querySelector('#statusCurrentTrack');
const statusPersistenceStateElement = document.querySelector('#statusPersistenceState');
const statusPersistenceProviderElement = document.querySelector('#statusPersistenceProvider');
const statusPersistenceLocationElement = document.querySelector('#statusPersistenceLocation');
const statusResultsStateElement = document.querySelector('#statusResultsState');
const statusTrackerStateElement = document.querySelector('#statusTrackerState');
const statusTrackerTrackElement = document.querySelector('#statusTrackerTrack');
const statusTrackerCoverageElement = document.querySelector('#statusTrackerCoverage');
const statusTrackerDetailElement = document.querySelector('#statusTrackerDetail');
const statusDiagnosticDetailsElement = document.querySelector('.statusDiagnosticDetails');
const fixturePathDiagnosticElement = document.querySelector('#fixturePathDiagnostic');
const rawStatusElement = document.querySelector('#rawStatus');
const refreshStatusButton = document.querySelector('#refreshStatus');
let isPopulatingSourceForm = false;
let autoSaveTimer = 0;
let sourceEditVersion = 0;
let lastSavedSourceEditVersion = 0;
let sourceSavePromise = null;
let hasLoadedSourceConfiguration = false;
let sourceFixturePathValue = '';
let sourceDriverAliases = {};
let sessionSetupSaveTimer = 0;
let sessionSetupSaveSequence = 0;
let isRenderingSessionSetup = false;
let driverProfiles = [];
let activeSessionWorkspaceTab = 'recent';
let openSessionResultTabs = [];
let activeSessionResultTabId = null;
let latestSessionWorkspace = null;
let isPopulatingDriverTrackerSettings = false;
let driverTrackerSaveTimer = 0;
let driverTrackerSaveSequence = 0;
let selectedDriverTrackerTrackName = null;
let latestCurrentTrackGeometry = null;
let latestSelectedTrackGeometry = null;
let selectedTrackGeometrySequence = 0;
let selectedTrackGeometryAbortController = null;
let latestDriverTrackerCatalog = [];
let driverTrackerTracksLoadSequence = 0;
let driverTrackerTracksLoadPromise = null;
let driverTrackerTracksAbortController = null;
let selectedDriverTrackerCatalogToken = null;
let driverTrackerPollingTimer = 0;
let isDriverTrackerPolling = false;
let latestLiveStatusSnapshot = null;
let latestAdminStatus = null;
let statusPollingTimer = 0;
let isStatusPolling = false;
let isStatusRefreshBusy = false;

const recentSessionWindowMs = 3 * 60 * 60 * 1000;
const activeSessionFreshWindowMs = 15 * 60 * 1000;

const languageStorageKey = 'trackside.admin.language';
const tabStorageKey = 'trackside.admin.tab';
const translations = {
  en: {
    'admin.title': 'Admin',
    'language.label': 'Language',
    'nav.kiosk': 'Kiosk',
    'nav.logout': 'Logout',
    'tabs.sessionSetup': 'Session Setup',
    'tabs.sessions': 'Sessions',
    'tabs.tracker': 'Tracker',
    'tabs.leaderboards': 'Leaderboards',
    'tabs.admins': 'Admins',
    'tabs.status': 'Status',
    'tabs.advanced': 'Advanced',
    'status.checkingSession': 'Checking admin session...',
    'status.adminLoginRequired': 'Admin login required.',
    'status.createFirstAdmin': 'Create the first admin account.',
    'status.signedIn': 'Signed in as {name}',
    'status.sourceChanged': 'Source configuration changed...',
    'status.sourceSaving': 'Saving source configuration...',
    'status.setupChanged': 'Prepared session setup changed...',
    'status.setupSaving': 'Saving prepared session setup...',
    'status.setupSaved': 'Prepared session setup saved.',
    'status.setupCleared': 'Prepared session setup cleared.',
    'status.profileCreated': 'Driver profile created.',
    'status.sessionIncluded': 'Session inclusion updated.',
    'status.kioskSaved': 'Kiosk display mode saved.',
    'status.driverTrackerChanged': 'Driver tracker settings changed...',
    'status.driverTrackerSaving': 'Saving driver tracker settings...',
    'status.driverTrackerSaved': 'Driver tracker settings saved.',
    'status.driverTrackerRecordingStarted': 'Geometry recording started.',
    'status.driverTrackerOutlineDeleted': 'Deleted outline geometry for {track}.',
    'status.monthlyTrackStarted': 'Monthly track started with fresh stats.',
    'status.monthlyTrackReset': 'Monthly track stats reset.',
    'firstAdmin.title': 'First Admin',
    'firstAdmin.username': 'Username',
    'firstAdmin.displayName': 'Display name',
    'firstAdmin.password': 'Password',
    'firstAdmin.createButton': 'Create Admin',
    'firstAdmin.usernamePlaceholder': 'Enter username',
    'firstAdmin.displayNamePlaceholder': 'Enter display name',
    'firstAdmin.passwordPlaceholder': 'Enter password',
    'login.title': 'Login',
    'login.username': 'Username',
    'login.password': 'Password',
    'login.submit': 'Login',
    'sourceConfig.title': 'Live Source',
    'sourceConfig.description': 'Choose the timing source, memory-map discovery behavior, and polling rates used by the live kiosk feed.',
    'sourceConfig.selectionTitle': 'Source Selection',
    'sourceConfig.behaviorTitle': 'Source Behavior',
    'sourceConfig.pollingTitle': 'Polling Rates',
    'source.mode': 'Source mode',
    'source.fixture': 'Fixture',
    'source.sharedMemory': 'Shared memory',
    'source.fixturePath': 'Fixture path',
    'source.scoringMapName': 'Exact scoring map',
    'source.processId': 'Dedicated server PID',
    'source.autoDiscover': 'Auto-discover memory maps',
    'source.processNames': 'Dedicated server process names',
    'source.multipleMapPolicy': 'Multiple map policy',
    'source.requireExplicitSelection': 'Require explicit selection',
    'source.useFirstDiscovered': 'Use first discovered',
    'source.scoringPollHz': 'Scoring poll Hz',
    'source.telemetryEnabled': 'Enable telemetry loop',
    'source.telemetryPollHz': 'Telemetry poll Hz',
    'source.driverAliasesJson': 'Driver aliases JSON',
    'discovery.title': 'Shared Memory Discovery',
    'discovery.noResult': 'No discovery result yet.',
    'discovery.noPid': 'no PID',
    'discovery.refreshButton': 'Reload Source & Discovery',
    'kioskDisplay.title': 'Kiosk Display',
    'kioskDisplay.defaultMode': 'Default display mode',
    'kioskDisplay.saveButton': 'Save Display Mode',
    'kioskDisplay.monthly': 'Monthly',
    'kioskDisplay.weekly': 'Weekly',
    'kioskDisplay.daily': 'Daily',
    'kioskDisplay.lastSession': 'Last Session',
    'kioskDisplay.live': 'Live',
    'kioskDisplay.tracker': 'Tracker',
    'driverTracker.title': 'Driver Tracker',
    'driverTracker.clientRefreshHz': 'Client refresh Hz',
    'driverTracker.geometryRecordingLaps': 'Geometry laps',
    'driverTracker.saveButton': 'Save Tracker',
    'driverTracker.refreshTracks': 'Refresh Tracks',
    'driverTracker.track': 'Track',
    'driverTracker.status': 'Status',
    'driverTracker.coverage': 'Coverage',
    'driverTracker.laps': 'Laps',
    'driverTracker.samples': 'Samples',
    'driverTracker.updated': 'Updated',
    'driverTracker.detail': 'Detail',
    'driverTracker.actions': 'Actions',
    'driverTracker.phase4Note': 'Track-outline generation was not venue-validated in Phase 4. Treat this as operator tooling and confirm on-site behavior before race-night reliance.',
    'driverTracker.actionHelp': 'Improve Outline keeps stored geometry and averages more completed laps. Start Over clears the stored outline and records fresh laps.',
    'driverTracker.currentGeometryTitle': 'Selected Track Geometry',
    'driverTracker.selectionHint': 'Select a track row to inspect its geometry state and stored outline.',
    'driverTracker.selectionLoaded': 'Selected track {track}. Its stored outline is shown when complete geometry is available.',
    'driverTracker.selectionLoading': 'Loading geometry for {track}...',
    'driverTracker.noTracks': 'No tracks seen yet.',
    'driverTracker.unavailable': 'Unavailable',
    'driverTracker.recording': 'Recording',
    'driverTracker.partial': 'Partial',
    'driverTracker.complete': 'Complete',
    'driverTracker.source': 'Source',
    'driverTracker.state': 'State',
    'driverTracker.activeTrack': 'Track',
    'driverTracker.lapProgress': 'Lap progress',
    'driverTracker.noSelection': 'Select a track row to inspect geometry state.',
    'driverTracker.outlineLoading': 'Loading selected track outline...',
    'driverTracker.outlineUnavailable': 'No complete stored outline is available for the selected track yet.',
    'driverTracker.improve': 'Improve Outline',
    'driverTracker.improveTitle': 'Keep the stored outline and add completed laps to improve averaging.',
    'driverTracker.improveAria': 'Improve outline for {track}',
    'driverTracker.restart': 'Start Over',
    'driverTracker.restartTitle': 'Clear the stored outline and record a fresh outline from new laps.',
    'driverTracker.restartAria': 'Start over outline recording for {track}',
    'driverTracker.delete': 'Delete Outline',
    'driverTracker.deleteTitle': 'Delete stored and in-progress outline geometry for this track only.',
    'driverTracker.deleteAria': 'Delete outline for {track}',
    'driverTracker.confirmStartOver': 'Start over outline recording for {track}? This clears stored and in-progress outline geometry for this track only. Sessions and laps are not deleted.',
    'driverTracker.confirmDeleteOutline': 'Delete outline for {track}? This removes stored and in-progress outline geometry for this track only. Sessions and laps are not deleted.',
    'monthlyTrack.title': 'Monthly Track',
    'monthlyTrack.noActive': 'No active monthly track.',
    'monthlyTrack.trackName': 'Track name',
    'monthlyTrack.reason': 'Reason',
    'monthlyTrack.reasonPlaceholder': 'Scheduled rotation',
    'monthlyTrack.setButton': 'Set Track',
    'monthlyTrack.resetButton': 'Reset Current Track',
    'monthlyBests.title': 'Monthly Bests',
    'monthlyBests.empty': 'No counted timed laps yet.',
    'bestLap.rank': 'Rank',
    'bestLap.driver': 'Driver',
    'bestLap.rig': 'Rig',
    'bestLap.lap': 'Lap',
    'bestLap.time': 'Time',
    'bestLap.set': 'Set',
    'retention.title': 'Retention Cleanup',
    'retention.noRun': 'Cleanup has not run from this page.',
    'retention.runButton': 'Run Cleanup',
    'retention.cleanupResult': 'Deleted {detailedLapRecordsDeleted} raw laps, {sessionSummariesDeleted} sessions, {trackBestRecordsDeleted} track bests, and {monthlyTrackPeriodsDeleted} monthly periods.',
    'adminUsers.title': 'Admin Users',
    'adminUsers.username': 'Username',
    'adminUsers.displayName': 'Display name',
    'adminUsers.created': 'Created',
    'adminUsers.updated': 'Updated',
    'adminUsers.createTitle': 'Create Admin',
    'adminUsers.createUsername': 'Username',
    'adminUsers.createDisplayName': 'Display name',
    'adminUsers.createPassword': 'Password',
    'adminUsers.createButton': 'Create',
    'setup.title': 'Prepare Session',
    'setup.description': 'Prepared rig assignments stay active for future sessions until changed or cleared.',
    'setup.rig': 'Rig',
    'setup.screenName': 'Screen name',
    'setup.driverProfile': 'Driver profile',
    'setup.addRig': 'Add Rig',
    'setup.clear': 'Clear Setup',
    'setup.noProfile': 'No profile',
    'setup.remove': 'Remove',
    'profiles.title': 'Driver Profiles',
    'profiles.displayName': 'Display name',
    'profiles.email': 'Email',
    'profiles.notes': 'Notes',
    'profiles.create': 'Create Profile',
    'sessions.title': 'Session Workspace',
    'sessions.workspaceAria': 'Session results workspace',
    'sessions.description': 'Review live, recent, and archived results, then open any session for driver details and follow-up.',
    'sessions.liveTitle': 'In Progress',
    'sessions.liveDescription': 'Active sessions stay visible here without taking over the results workspace.',
    'sessions.recentTitle': 'Recently Completed',
    'sessions.recentDescription': 'Up to 4 completed sessions from the last 3 hours stay ready for staff and customer follow-up.',
    'sessions.olderTitle': 'Older Results',
    'sessions.olderDescription': 'Browse archived sessions grouped by day and open or remove older stored results.',
    'sessions.boards': 'Boards',
    'sessions.track': 'Track',
    'sessions.kind': 'Session',
    'sessions.phase': 'Phase',
    'sessions.lastSeen': 'Last seen',
    'sessions.participants': 'Drivers',
    'sessions.actions': 'Actions',
    'sessions.refresh': 'Refresh Sessions',
    'sessions.deleteEmpty': 'Delete Empty Sessions',
    'sessions.view': 'View',
    'sessions.openResults': 'Open Results',
    'sessions.include': 'Include',
    'sessions.exclude': 'Exclude',
    'sessions.delete': 'Delete',
    'sessions.included': 'Included',
    'sessions.excluded': 'Excluded',
    'sessions.detailTitle': 'Driver Results',
    'sessions.detailDescription': 'Select a session to reveal detailed information without disrupting the list.',
    'sessions.emptyDetail': 'Select a session from the workspace to inspect driver results and open follow-up actions.',
    'sessions.closeResults': 'Close Results',
    'sessions.resultTabTitle': '{track} - {kind}',
    'sessions.resultTabAria': 'Driver results for {track} ({kind}).',
    'sessions.resultTabCloseAria': 'Close result tab for {track} ({kind}).',
    'sessions.loadedSummary': '{recent} recent and {older} older stored sessions loaded.',
    'sessions.viewing': 'Viewing stored session from {lastSeen}.',
    'sessions.includedMessage': 'Session included in historical boards.',
    'sessions.excludedMessage': 'Session excluded from historical boards.',
    'sessions.correction': 'Correction',
    'sessions.exclude': 'Exclude',
    'sessions.manage': 'Manage Results',
    'sessions.manageClose': 'Close Manage Results',
    'sessions.telemetry': 'Telemetry / Report',
    'sessions.compare': 'Compare Drivers',
    'sessions.rank': 'Rank',
    'sessions.driver': 'Driver',
    'sessions.best': 'Best',
    'sessions.lastLap': 'Last Lap',
    'sessions.laps': 'Laps',
    'sessions.position': 'Result',
    'sessions.editDriver': 'Edit',
    'sessions.inspectDriver': 'View',
    'sessions.hideDriver': 'Hide',
    'sessions.counted': 'Counted',
    'sessions.observed': 'Observed',
    'sessions.invalid': 'Invalid',
    'sessions.reason': 'Reason',
    'sessions.selectDriver': 'Select a driver row to inspect laps and follow-up actions.',
    'sessions.compareEmpty': 'Compare Drivers shows the selected session as a compact timing comparison view.',
    'sessions.telemetryPending': 'Telemetry / report workflow is not wired into this workspace yet.',
    'sessions.groupToday': 'Earlier Today',
    'sessions.groupYesterday': 'Yesterday',
    'sessions.groupOlder': 'Older',
    'sessions.noLive': 'No active session is visible right now.',
    'sessions.noRecent': 'No recently completed sessions in the last 3 hours.',
    'sessions.noOlder': 'No older stored results match the current view.',
    'sessions.topDriver': 'Top Driver',
    'sessions.bestLap': 'Best Lap',
    'sessions.driverCount': 'Drivers',
    'sessions.boardStatus': 'Leaderboards',
    'sessions.lastUpdated': 'Updated',
    'sessions.compareDelta': 'Delta',
    'sessions.readOnly': 'Open driver results, compare drivers, or switch on result management for corrections.',
    'sessions.manageHint': 'Manage Results is active. Corrections and destructive actions are now visible for this selected session.',
    'sessions.deleteBlockedRecent': 'This stored result was updated in the last 3 hours and cannot be deleted yet.',
    'sessions.deleteBlockedActive': 'This stored result matches an active live session and cannot be deleted.',
    'sessions.deleteProtectedHint': 'Delete is only available for older sessions outside the recent and active windows.',
    'status.adminCreated': 'Admin user created.',
    'status.passwordChanged': 'Admin password changed.',
    'monthlyBests.empty': 'No counted timed laps yet.',
    'login.usernamePlaceholder': 'Enter username',
    'login.passwordPlaceholder': 'Enter password',
    'source.fixturePathPlaceholder': 'Enter fixture path',
    'source.scoringMapNamePlaceholder': 'Enter exact scoring map',
    'source.processNamesPlaceholder': 'Enter process names',
    'discovery.candidateOrder': 'Candidate Order',
    'discovery.discoveredMaps': 'Discovered Maps',
    'discovery.ambiguousMaps': 'Ambiguous Maps',
    'aria.firstAdmin': 'First admin setup',
    'aria.login': 'Admin login',
    'aria.advancedConfiguration': 'Advanced source configuration',
    'aria.sharedMemoryDiscovery': 'Shared-memory discovery',
    'changePassword.title': 'Change Password',
    'changePassword.username': 'Username',
    'changePassword.newPassword': 'New password',
    'changePassword.button': 'Change Password',
    'statusPanel.title': 'Status',
    'statusPanel.description': 'Live operational summary for venue staff. This view auto-refreshes while active.',
    'statusPanel.lastUpdated': 'Last updated',
    'statusPanel.notAvailable': 'n/a',
    'statusPanel.liveCardTitle': 'Live Service',
    'statusPanel.persistenceCardTitle': 'Persistence / Results',
    'statusPanel.trackerCardTitle': 'Tracker',
    'statusPanel.serviceState': 'Service state',
    'statusPanel.sourceMode': 'Source mode',
    'statusPanel.sourceState': 'Source state',
    'statusPanel.currentTrack': 'Current track',
    'statusPanel.persistenceState': 'Persistence',
    'statusPanel.persistenceProvider': 'Provider',
    'statusPanel.persistenceLocation': 'Location',
    'statusPanel.resultsState': 'Results flow',
    'statusPanel.trackerState': 'State',
    'statusPanel.trackerTrack': 'Track',
    'statusPanel.trackerCoverage': 'Coverage',
    'statusPanel.trackerDetail': 'Detail',
    'statusPanel.persistenceEnabled': 'Enabled',
    'statusPanel.persistenceDisabled': 'Disabled',
    'statusPanel.snapshotReady': 'Current session snapshot available',
    'statusPanel.snapshotWaiting': 'Waiting for current session snapshot',
    'statusPanel.noActiveSession': 'No active session',
    'statusPanel.trackerMix': '{complete} complete, {partial} partial, {recording} recording.',
    'statusPanel.diagnosticsTitle': 'Diagnostic Status',
    'statusPanel.diagnosticsDescription': 'Raw service status for administrator troubleshooting.',
    'statusPanel.noStatus': 'No status loaded.',
    'statusPanel.refreshButton': 'Refresh Status',
    'advancedConfig.fixtureTitle': 'Fixture Fallback',
    'advancedConfig.fixturePathLabel': 'Fixture path diagnostic (read-only)',
    'advancedConfig.fixturePathNote': 'Only relevant to fixture/demo/fallback operation. This field is diagnostic and not part of routine source editing.',
    'advancedConfig.fixturePathUnset': 'No fixture path configured.',
    'sessions.empty': 'No persisted sessions yet.',
    'sessions.noParticipants': 'No participants persisted for this session.',
    'participants.noCompletedLaps': 'No completed laps persisted for this participant.',
    'sessions.deleted': 'Stored session deleted.',
    'sessions.participantCorrectionSaved': 'Driver correction saved.',
    'sessions.lapCorrectionSaved': 'Lap correction saved.',
    'sessions.emptyDeleted': 'Deleted {count} empty stored sessions.',
    'sessions.confirmDelete': 'Delete this stored session from history?',
    'sessions.confirmDeleteEmpty': 'Delete stored sessions that have no participants, no completed laps, or no known track?',
  },
  nl: {
    'admin.title': 'Beheer',
    'language.label': 'Taal',
    'nav.kiosk': 'Kiosk',
    'nav.logout': 'Uitloggen',
    'tabs.sessionSetup': 'Sessie voorbereiden',
    'tabs.sessions': 'Sessies',
    'tabs.tracker': 'Tracker',
    'tabs.leaderboards': 'Klassementen',
    'tabs.admins': 'Beheerders',
    'tabs.status': 'Status',
    'tabs.advanced': 'Geavanceerd',
    'status.checkingSession': 'Beheersessie controleren...',
    'status.adminLoginRequired': 'Beheerlogin vereist.',
    'status.createFirstAdmin': 'Maak het eerste beheeraccount aan.',
    'status.signedIn': 'Ingelogd als {name}',
    'status.sourceChanged': 'Bronconfiguratie gewijzigd...',
    'status.sourceSaving': 'Bronconfiguratie opslaan...',
    'status.setupChanged': 'Sessievoorbereiding gewijzigd...',
    'status.setupSaving': 'Sessievoorbereiding opslaan...',
    'status.setupSaved': 'Sessievoorbereiding opgeslagen.',
    'status.setupCleared': 'Sessievoorbereiding gewist.',
    'status.profileCreated': 'Bestuurdersprofiel aangemaakt.',
    'status.sessionIncluded': 'Sessie-inclusie bijgewerkt.',
    'status.kioskSaved': 'Kiosk-weergavemodus opgeslagen.',
    'status.driverTrackerChanged': 'Drivertracker-instellingen gewijzigd...',
    'status.driverTrackerSaving': 'Drivertracker-instellingen opslaan...',
    'status.driverTrackerSaved': 'Drivertracker-instellingen opgeslagen.',
    'status.driverTrackerRecordingStarted': 'Geometrie-opname gestart.',
    'status.driverTrackerOutlineDeleted': 'Contourgeometrie verwijderd voor {track}.',
    'status.monthlyTrackStarted': 'Maandelijk klassement gestart met frisse statistieken.',
    'status.monthlyTrackReset': 'Maandelijkse klassementstatistieken gereset.',
    'firstAdmin.title': 'Eerste beheerder',
    'firstAdmin.username': 'Gebruikersnaam',
    'firstAdmin.displayName': 'Weergavenaam',
    'firstAdmin.password': 'Wachtwoord',
    'firstAdmin.createButton': 'Beheerder aanmaken',
    'firstAdmin.usernamePlaceholder': 'Voer gebruikersnaam in',
    'firstAdmin.displayNamePlaceholder': 'Voer weergavenaam in',
    'firstAdmin.passwordPlaceholder': 'Voer wachtwoord in',
    'login.title': 'Inloggen',
    'login.username': 'Gebruikersnaam',
    'login.password': 'Wachtwoord',
    'login.submit': 'Inloggen',
    'sourceConfig.title': 'Live bron',
    'sourceConfig.description': 'Kies de timingbron, memory-map ontdekking en polling-snelheden voor de live kioskfeed.',
    'sourceConfig.selectionTitle': 'Bronselectie',
    'sourceConfig.behaviorTitle': 'Brongedrag',
    'sourceConfig.pollingTitle': 'Pollingsnelheden',
    'source.mode': 'Bronmodus',
    'source.fixture': 'Fixture',
    'source.sharedMemory': 'Gedeeld geheugen',
    'source.fixturePath': 'Fixture-pad',
    'source.scoringMapName': 'Exacte scorekaart',
    'source.processId': 'PID van dedicated server',
    'source.autoDiscover': 'Memory-map automatisch ontdekken',
    'source.processNames': 'Procesnamen dedicated server',
    'source.multipleMapPolicy': 'Meerdere kaartbeleid',
    'source.requireExplicitSelection': 'Vereis expliciete selectie',
    'source.useFirstDiscovered': 'Gebruik eerste ontdekte',
    'source.scoringPollHz': 'Scoring poll Hz',
    'source.telemetryEnabled': 'Telemetry loop inschakelen',
    'source.telemetryPollHz': 'Telemetry poll Hz',
    'source.driverAliasesJson': 'Driver-aliases JSON',
    'discovery.title': 'Shared-memory ontdekking',
    'discovery.noResult': 'Nog geen ontdekresultaat.',
    'discovery.noPid': 'geen PID',
    'discovery.refreshButton': 'Bron en ontdekking herladen',
    'kioskDisplay.title': 'Kioskweergave',
    'kioskDisplay.defaultMode': 'Standaard weergavemodus',
    'kioskDisplay.saveButton': 'Weergavemodus opslaan',
    'kioskDisplay.monthly': 'Maandelijks',
    'kioskDisplay.weekly': 'Wekelijks',
    'kioskDisplay.daily': 'Dagelijks',
    'kioskDisplay.lastSession': 'Laatste sessie',
    'kioskDisplay.live': 'Live',
    'kioskDisplay.tracker': 'Tracker',
    'driverTracker.title': 'Drivertracker',
    'driverTracker.clientRefreshHz': 'Verversingssnelheid client (Hz)',
    'driverTracker.geometryRecordingLaps': 'Geometrieronden',
    'driverTracker.saveButton': 'Tracker opslaan',
    'driverTracker.refreshTracks': 'Banen vernieuwen',
    'driverTracker.track': 'Baan',
    'driverTracker.status': 'Status',
    'driverTracker.coverage': 'Dekking',
    'driverTracker.laps': 'Ronden',
    'driverTracker.samples': 'Meetpunten',
    'driverTracker.updated': 'Bijgewerkt',
    'driverTracker.detail': 'Toelichting',
    'driverTracker.actions': 'Acties',
    'driverTracker.phase4Note': 'Generatie van baancontouren is in fase 4 niet op de locatie gevalideerd. Gebruik dit als operatorhulpmiddel en bevestig gedrag op locatie voordat je erop vertrouwt tijdens racedagen.',
    'driverTracker.actionHelp': 'Contour verbeteren behoudt de opgeslagen geometrie en middelt extra voltooide ronden. Opnieuw starten wist de opgeslagen contour en neemt nieuwe ronden op.',
    'driverTracker.currentGeometryTitle': 'Geometrie van geselecteerde baan',
    'driverTracker.selectionHint': 'Selecteer een baanrij om de geometriestatus en opgeslagen baancontour te bekijken.',
    'driverTracker.selectionLoaded': 'Baan {track} geselecteerd. De opgeslagen contour wordt getoond zodra volledige geometrie beschikbaar is.',
    'driverTracker.selectionLoading': 'Geometrie voor {track} laden...',
    'driverTracker.noTracks': 'Nog geen banen gezien.',
    'driverTracker.unavailable': 'Niet beschikbaar',
    'driverTracker.recording': 'Opnemen',
    'driverTracker.partial': 'Gedeeltelijk',
    'driverTracker.complete': 'Volledig',
    'driverTracker.source': 'Bron',
    'driverTracker.state': 'Status',
    'driverTracker.activeTrack': 'Baan',
    'driverTracker.lapProgress': 'Rondevoortgang',
    'driverTracker.noSelection': 'Selecteer een baanrij om geometriestatus te bekijken.',
    'driverTracker.outlineLoading': 'Contour van geselecteerde baan laden...',
    'driverTracker.outlineUnavailable': 'Voor de geselecteerde baan is nog geen volledige opgeslagen contour beschikbaar.',
    'driverTracker.improve': 'Contour verbeteren',
    'driverTracker.improveTitle': 'Behoud de opgeslagen contour en voeg voltooide ronden toe om het gemiddelde te verbeteren.',
    'driverTracker.improveAria': 'Contour verbeteren voor {track}',
    'driverTracker.restart': 'Opnieuw starten',
    'driverTracker.restartTitle': 'Wis de opgeslagen contour en neem een nieuwe contour op met nieuwe ronden.',
    'driverTracker.restartAria': 'Contouropname opnieuw starten voor {track}',
    'driverTracker.delete': 'Contour verwijderen',
    'driverTracker.deleteTitle': 'Verwijder opgeslagen en lopende contourgeometrie voor alleen deze baan.',
    'driverTracker.deleteAria': 'Contour verwijderen voor {track}',
    'driverTracker.confirmStartOver': 'Contouropname opnieuw starten voor {track}? Dit wist opgeslagen en lopende contourgeometrie voor alleen deze baan. Sessies en ronden blijven behouden.',
    'driverTracker.confirmDeleteOutline': 'Contour verwijderen voor {track}? Dit verwijdert opgeslagen en lopende contourgeometrie voor alleen deze baan. Sessies en ronden blijven behouden.',
    'monthlyTrack.title': 'Maandelijkse baan',
    'monthlyTrack.noActive': 'Geen actieve maandelijkse baan.',
    'monthlyTrack.trackName': 'Baannaam',
    'monthlyTrack.reason': 'Reden',
    'monthlyTrack.reasonPlaceholder': 'Geplande rotatie',
    'monthlyTrack.setButton': 'Baan instellen',
    'monthlyTrack.resetButton': 'Huidige baan resetten',
    'monthlyBests.title': 'Maandelijkse bests',
    'monthlyBests.empty': 'Nog geen getelde tijdige ronden.',
    'bestLap.rank': 'Rank',
    'bestLap.driver': 'Driver',
    'bestLap.rig': 'Rig',
    'bestLap.lap': 'Lap',
    'bestLap.time': 'Time',
    'bestLap.set': 'Set',
    'retention.title': 'Retentie opschonen',
    'retention.noRun': 'Opschoning is nog niet uitgevoerd vanaf deze pagina.',
    'retention.runButton': 'Opschoning uitvoeren',
    'retention.cleanupResult': 'Verwijderd {detailedLapRecordsDeleted} ruwe ronden, {sessionSummariesDeleted} sessies, {trackBestRecordsDeleted} circuitrecords en {monthlyTrackPeriodsDeleted} maandperioden.',
    'adminUsers.title': 'Beheeraccounts',
    'adminUsers.username': 'Gebruikersnaam',
    'adminUsers.displayName': 'Weergavenaam',
    'adminUsers.created': 'Aangemaakt',
    'adminUsers.updated': 'Bijgewerkt',
    'adminUsers.createTitle': 'Beheerder aanmaken',
    'adminUsers.createUsername': 'Gebruikersnaam',
    'adminUsers.createDisplayName': 'Weergavenaam',
    'adminUsers.createPassword': 'Wachtwoord',
    'adminUsers.createButton': 'Aanmaken',
    'setup.title': 'Sessie voorbereiden',
    'setup.description': 'Voorbereide rigtoewijzingen blijven actief voor toekomstige sessies totdat ze worden gewijzigd of gewist.',
    'setup.rig': 'Rig',
    'setup.screenName': 'Schermnaam',
    'setup.driverProfile': 'Bestuurdersprofiel',
    'setup.addRig': 'Rig toevoegen',
    'setup.clear': 'Setup wissen',
    'setup.noProfile': 'Geen profiel',
    'setup.remove': 'Verwijderen',
    'profiles.title': 'Bestuurdersprofielen',
    'profiles.displayName': 'Weergavenaam',
    'profiles.email': 'E-mail',
    'profiles.notes': 'Notities',
    'profiles.create': 'Profiel aanmaken',
    'sessions.title': 'Sessiewerkruimte',
    'sessions.workspaceAria': 'Werkruimte voor sessieresultaten',
    'sessions.description': 'Bekijk live, recente en gearchiveerde resultaten en open daarna elke sessie voor rijderdetails en opvolging.',
    'sessions.liveTitle': 'Bezig',
    'sessions.liveDescription': 'Actieve sessies blijven hier zichtbaar zonder de resultatenwerkruimte over te nemen.',
    'sessions.recentTitle': 'Recent voltooid',
    'sessions.recentDescription': 'Tot 4 voltooide sessies uit de laatste 3 uur blijven direct beschikbaar voor personeel en klanten.',
    'sessions.olderTitle': 'Oudere resultaten',
    'sessions.olderDescription': 'Blader door gearchiveerde sessies per dag en open of verwijder oudere opgeslagen resultaten.',
    'sessions.boards': 'Klassementen',
    'sessions.track': 'Circuit',
    'sessions.kind': 'Sessie',
    'sessions.phase': 'Fase',
    'sessions.lastSeen': 'Laatst gezien',
    'sessions.participants': 'Bestuurders',
    'sessions.actions': 'Acties',
    'sessions.refresh': 'Sessies vernieuwen',
    'sessions.deleteEmpty': 'Lege sessies verwijderen',
    'sessions.view': 'Bekijken',
    'sessions.openResults': 'Resultaat openen',
    'sessions.include': 'Inclusief',
    'sessions.exclude': 'Uitsluiten',
    'sessions.delete': 'Verwijderen',
    'sessions.included': 'Inbegrepen',
    'sessions.excluded': 'Uitgesloten',
    'sessions.detailTitle': 'Rijdersresultaten',
    'sessions.detailDescription': 'Selecteer een sessie om detailinformatie te tonen zonder de lijst te verstoren.',
    'sessions.emptyDetail': 'Selecteer een sessie uit de werkruimte om rijdersresultaten en vervolgstappen te bekijken.',
    'sessions.closeResults': 'Resultaten sluiten',
    'sessions.resultTabTitle': '{track} - {kind}',
    'sessions.resultTabAria': 'Rijdersresultaten voor {track} ({kind}).',
    'sessions.resultTabCloseAria': 'Resultaattab sluiten voor {track} ({kind}).',
    'sessions.loadedSummary': '{recent} recente en {older} oudere opgeslagen sessies geladen.',
    'sessions.viewing': 'Opgeslagen sessie van {lastSeen} wordt bekeken.',
    'sessions.includedMessage': 'Sessie opgenomen in historische klassementen.',
    'sessions.excludedMessage': 'Sessie uitgesloten van historische klassementen.',
    'sessions.correction': 'Correctie',
    'sessions.exclude': 'Uitsluiten',
    'sessions.manage': 'Resultaten beheren',
    'sessions.manageClose': 'Resultatenbeheer sluiten',
    'sessions.telemetry': 'Telemetrie / rapport',
    'sessions.compare': 'Bestuurders vergelijken',
    'sessions.rank': 'Positie',
    'sessions.driver': 'Bestuurder',
    'sessions.best': 'Beste',
    'sessions.lastLap': 'Laatste ronde',
    'sessions.laps': 'Ronden',
    'sessions.position': 'Resultaat',
    'sessions.editDriver': 'Bewerken',
    'sessions.inspectDriver': 'Bekijken',
    'sessions.hideDriver': 'Verbergen',
    'sessions.counted': 'Geteld',
    'sessions.observed': 'Gezien',
    'sessions.invalid': 'Ongeldig',
    'sessions.reason': 'Reden',
    'sessions.selectDriver': 'Selecteer een rijderrij om ronden en vervolgstappen te bekijken.',
    'sessions.compareEmpty': 'Bestuurders vergelijken toont de geselecteerde sessie als compacte timingvergelijking.',
    'sessions.telemetryPending': 'Telemetrie- / rapportworkflow is nog niet gekoppeld aan deze werkruimte.',
    'sessions.groupToday': 'Eerder vandaag',
    'sessions.groupYesterday': 'Gisteren',
    'sessions.groupOlder': 'Ouder',
    'sessions.noLive': 'Er is momenteel geen actieve sessie zichtbaar.',
    'sessions.noRecent': 'Geen recent voltooide sessies in de laatste 3 uur.',
    'sessions.noOlder': 'Geen oudere opgeslagen resultaten voor deze weergave.',
    'sessions.topDriver': 'Toprijder',
    'sessions.bestLap': 'Beste ronde',
    'sessions.driverCount': 'Bestuurders',
    'sessions.boardStatus': 'Klassementen',
    'sessions.lastUpdated': 'Bijgewerkt',
    'sessions.compareDelta': 'Verschil',
    'sessions.readOnly': 'Open rijdersresultaten, vergelijk rijders of schakel resultatenbeheer in voor correcties.',
    'sessions.manageHint': 'Resultatenbeheer is actief. Correcties en destructieve acties zijn nu zichtbaar voor deze geselecteerde sessie.',
    'sessions.deleteBlockedRecent': 'Dit opgeslagen resultaat is in de laatste 3 uur bijgewerkt en kan nog niet worden verwijderd.',
    'sessions.deleteBlockedActive': 'Dit opgeslagen resultaat hoort bij een actieve live sessie en kan niet worden verwijderd.',
    'sessions.deleteProtectedHint': 'Verwijderen is alleen beschikbaar voor oudere sessies buiten de recente en actieve vensters.',
    'status.adminCreated': 'Beheerder aangemaakt.',
    'status.passwordChanged': 'Wachtwoord gewijzigd.',
    'monthlyBests.empty': 'Nog geen getelde tijdige ronden.',
    'login.usernamePlaceholder': 'Voer gebruikersnaam in',
    'login.passwordPlaceholder': 'Voer wachtwoord in',
    'source.fixturePathPlaceholder': 'Voer fixture-pad in',
    'source.scoringMapNamePlaceholder': 'Voer exacte scorekaart in',
    'source.processNamesPlaceholder': 'Voer processnamen in',
    'discovery.candidateOrder': 'Kandidaat volgorde',
    'discovery.discoveredMaps': 'Ontdekte kaarten',
    'discovery.ambiguousMaps': 'Ambigue kaarten',
    'aria.firstAdmin': 'Eerste beheerder instellen',
    'aria.login': 'Beheer login',
    'aria.advancedConfiguration': 'Geavanceerde bronconfiguratie',
    'aria.sharedMemoryDiscovery': 'Shared-memory ontdekking',
    'changePassword.title': 'Wachtwoord wijzigen',
    'changePassword.username': 'Gebruikersnaam',
    'changePassword.newPassword': 'Nieuw wachtwoord',
    'changePassword.button': 'Wachtwoord wijzigen',
    'statusPanel.title': 'Status',
    'statusPanel.description': 'Live operationele samenvatting voor locatiepersoneel. Dit overzicht ververst automatisch terwijl het actief is.',
    'statusPanel.lastUpdated': 'Laatst bijgewerkt',
    'statusPanel.notAvailable': 'n.v.t.',
    'statusPanel.liveCardTitle': 'Live service',
    'statusPanel.persistenceCardTitle': 'Persistente opslag / resultaten',
    'statusPanel.trackerCardTitle': 'Tracker',
    'statusPanel.serviceState': 'Servicestatus',
    'statusPanel.sourceMode': 'Bronmodus',
    'statusPanel.sourceState': 'Bronstatus',
    'statusPanel.currentTrack': 'Huidige baan',
    'statusPanel.persistenceState': 'Persistente opslag',
    'statusPanel.persistenceProvider': 'Provider',
    'statusPanel.persistenceLocation': 'Locatie',
    'statusPanel.resultsState': 'Resultaatstroom',
    'statusPanel.trackerState': 'Status',
    'statusPanel.trackerTrack': 'Baan',
    'statusPanel.trackerCoverage': 'Dekking',
    'statusPanel.trackerDetail': 'Detail',
    'statusPanel.persistenceEnabled': 'Ingeschakeld',
    'statusPanel.persistenceDisabled': 'Uitgeschakeld',
    'statusPanel.snapshotReady': 'Huidige sessiesnapshot beschikbaar',
    'statusPanel.snapshotWaiting': 'Wachten op huidige sessiesnapshot',
    'statusPanel.noActiveSession': 'Geen actieve sessie',
    'statusPanel.trackerMix': '{complete} volledig, {partial} gedeeltelijk, {recording} opname.',
    'statusPanel.diagnosticsTitle': 'Diagnostische status',
    'statusPanel.diagnosticsDescription': 'Ruwe servicestatus voor probleemoplossing door beheerders.',
    'statusPanel.noStatus': 'Nog geen status geladen.',
    'statusPanel.refreshButton': 'Status verversen',
    'advancedConfig.fixtureTitle': 'Fixture-fallback',
    'advancedConfig.fixturePathLabel': 'Fixture-pad diagnostiek (alleen lezen)',
    'advancedConfig.fixturePathNote': 'Alleen relevant voor fixture/demo/fallback-gebruik. Dit veld is diagnostisch en geen regulier bewerkveld voor broninstellingen.',
    'advancedConfig.fixturePathUnset': 'Geen fixture-pad geconfigureerd.',
    'sessions.empty': 'Nog geen opgeslagen sessies.',
    'sessions.noParticipants': 'Geen deelnemers opgeslagen voor deze sessie.',
    'participants.noCompletedLaps': 'Geen voltooide ronden opgeslagen voor deze deelnemer.',
    'sessions.deleted': 'Opgeslagen sessie verwijderd.',
    'sessions.participantCorrectionSaved': 'Rijdercorrectie opgeslagen.',
    'sessions.lapCorrectionSaved': 'Rondecorrectie opgeslagen.',
    'sessions.emptyDeleted': '{count} lege opgeslagen sessies verwijderd.',
    'sessions.confirmDelete': 'Deze opgeslagen sessie uit de historie verwijderen?',
    'sessions.confirmDeleteEmpty': 'Opgeslagen sessies zonder deelnemers, zonder voltooide ronden of zonder bekend circuit verwijderen?',
  },
};

function translateMessage(template, params) {
  if (!template) return '';
  return Object.keys(params).reduce((result, key) => result.replace(`{${key}}`, params[key]), template);
}

function t(key, params = {}) {
  const translation = translations[languageSelectElement.value]?.[key] ?? translations.en[key] ?? key;
  return Object.keys(params).length > 0 ? translateMessage(translation, params) : translation;
}

function hasTranslationKey(key) {
  return key in (translations[languageSelectElement.value] ?? {}) || key in translations.en;
}

function setLanguage(language) {
  const nextLanguage = translations[language] ? language : 'en';
  localStorage.setItem(languageStorageKey, nextLanguage);
  languageSelectElement.value = nextLanguage;
  document.documentElement.lang = nextLanguage;
  document.querySelectorAll('[data-i18n]').forEach(element => {
    element.textContent = t(element.dataset.i18n);
  });
  document.querySelectorAll('[data-i18n-placeholder]').forEach(element => {
    element.placeholder = t(element.dataset.i18nPlaceholder);
  });
  document.querySelectorAll('[data-i18n-aria]').forEach(element => {
    element.setAttribute('aria-label', t(element.dataset.i18nAria));
  });
  sessionSetupRowsElement.querySelectorAll('.setupProfileId option[value=""]').forEach(option => {
    option.textContent = t('setup.noProfile');
  });
  sessionSetupRowsElement.querySelectorAll('.sessionSetupRow button').forEach(button => {
    button.textContent = t('setup.remove');
  });

  renderStatusDashboard();
  renderFixturePathDiagnostic();
  renderDriverTrackerTracks(latestDriverTrackerCatalog);
  renderDriverTrackerGeometryPanel();
  renderSelectedSessionWorkspace();
}

setupButton.addEventListener('click', () => createFirstAdmin().catch(showError));
loginButton.addEventListener('click', () => login().catch(showError));
setupPanel.addEventListener('keydown', event => submitAuthPanelOnEnter(event, createFirstAdmin));
loginPanel.addEventListener('keydown', event => submitAuthPanelOnEnter(event, login));
logoutButton.addEventListener('click', () => logout().catch(showError));
languageSelectElement.addEventListener('change', () => saveLocalizationChoice(languageSelectElement.value).catch(showError));
refreshElement.addEventListener('click', () => reloadConfiguration().catch(showError));
refreshSessionsButton.addEventListener('click', () => loadSessions().catch(showError));
deleteEmptySessionsButton.addEventListener('click', () => deleteEmptyHistoricalSessions().catch(showError));
sessionOverviewLiveTabButtonElement.addEventListener('click', () => activateSessionWorkspaceTab('live'));
sessionOverviewRecentTabButtonElement.addEventListener('click', () => activateSessionWorkspaceTab('recent'));
sessionOverviewOlderTabButtonElement.addEventListener('click', () => activateSessionWorkspaceTab('older'));
[sessionOverviewLiveTabButtonElement, sessionOverviewRecentTabButtonElement, sessionOverviewOlderTabButtonElement]
  .forEach(button => button.addEventListener('keydown', handleSessionWorkspaceTabKeydown));
compareDriversButton.addEventListener('click', () => toggleCompareDrivers());
openTelemetryButton.addEventListener('click', () => openTelemetryWorkspace());
toggleManageResultsButton.addEventListener('click', () => toggleManageResults());
addSetupRowButton.addEventListener('click', () => {
  appendSessionSetupRow({ rigName: nextRigName(), displayName: '', driverProfileId: null });
  scheduleSessionSetupAutoSave(0);
});
clearSessionSetupButton.addEventListener('click', () => clearSessionSetup().catch(showError));
createProfileButton.addEventListener('click', () => createDriverProfile().catch(showError));
saveKioskDisplayModeButton.addEventListener('click', () => saveKioskSettings().catch(showError));
saveDriverTrackerSettingsButton.addEventListener('click', () => {
  window.clearTimeout(driverTrackerSaveTimer);
  saveDriverTrackerSettings().catch(showError);
});
refreshDriverTrackerTracksButton.addEventListener('click', () => refreshDriverTrackerTracksManually().catch(showError));
setMonthlyTrackButton.addEventListener('click', () => setMonthlyTrack().catch(showError));
resetMonthlyTrackButton.addEventListener('click', () => resetMonthlyTrack().catch(showError));
runRetentionCleanupButton.addEventListener('click', () => runRetentionCleanup().catch(showError));
createAdminButton.addEventListener('click', () => createAdmin().catch(showError));
changePasswordButton.addEventListener('click', () => changePassword().catch(showError));
refreshStatusButton.addEventListener('click', () => refreshOperationalStatus().catch(showError));
statusDiagnosticDetailsElement.addEventListener('toggle', () => {
  if (!document.getElementById('statusTab')?.classList.contains('active')) {
    return;
  }

  if (statusDiagnosticDetailsElement.open) {
    // Pin the raw payload while it is being inspected or copied; manual refresh remains available.
    stopStatusPolling();
    refreshOperationalStatus().catch(showError);
  } else {
    startStatusPolling();
  }
});
renderSessionWorkspaceNavigation();
bindSourceAutoSave();
bindDriverTrackerAutoSave();
loadLanguageChoice().catch(showError);

document.querySelectorAll('[data-tab]').forEach(button => {
  button.addEventListener('click', () => showTab(button.dataset.tab));
});
document.addEventListener('visibilitychange', handleDocumentVisibilityChange);

loadSession().catch(showError);

function submitAuthPanelOnEnter(event, action) {
  if (event.key !== 'Enter' || event.isComposing || !(event.target instanceof HTMLInputElement)) {
    return;
  }

  // Auth panels are intentionally not full forms, but Enter should still follow normal login/setup muscle memory.
  event.preventDefault();
  action().catch(showError);
}

async function loadSession() {
  const session = await fetchJson('/api/admin/session');
  if (session.bootstrapRequired) {
    showSetup();
    return;
  }

  if (!session.isAuthenticated) {
    showLogin();
    return;
  }

  showDashboard(session);
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadSessions(), loadKioskSettings(), loadDriverTrackerSettings(), loadDriverTrackerTracks(), loadLeaderboards(), loadUsers(), loadAdminStatus()]);
}

async function createFirstAdmin() {
  const session = await postJson('/api/admin/bootstrap', {
    username: setupUsernameElement.value.trim(),
    displayName: setupDisplayNameElement.value.trim(),
    password: setupPasswordElement.value,
  });
  showDashboard(session);
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadSessions(), loadKioskSettings(), loadDriverTrackerSettings(), loadDriverTrackerTracks(), loadLeaderboards(), loadUsers(), loadAdminStatus()]);
}

async function login() {
  const session = await postJson('/api/admin/session', {
    username: loginUsernameElement.value.trim(),
    password: loginPasswordElement.value,
  });
  showDashboard(session);
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadSessions(), loadKioskSettings(), loadDriverTrackerSettings(), loadDriverTrackerTracks(), loadLeaderboards(), loadUsers(), loadAdminStatus()]);
}

async function logout() {
  stopDriverTrackerPolling();
  await fetch('/api/admin/session', { method: 'DELETE', credentials: 'same-origin' });
  showLogin();
}

async function loadConfiguration() {
  const editVersion = sourceEditVersion;
  const configuration = await fetchJson('/api/configuration/source');
  if (editVersion !== sourceEditVersion) {
    return;
  }

  populateForm(configuration);
  renderDiscovery(configuration.discovery);
  setStatus(`Loaded admin configuration from ${configuration.writableConfigurationPath}`);
}

async function saveConfiguration() {
  const previousSave = sourceSavePromise;
  const currentSave = (async () => {
    if (previousSave) {
      await previousSave.catch(() => {});
    }

    const editVersion = sourceEditVersion;
    const payload = readSourceConfigurationForm();
    setStatus(t('status.sourceSaving'));
    const saved = await putJson('/api/configuration/source', payload);
    lastSavedSourceEditVersion = Math.max(lastSavedSourceEditVersion, editVersion);
    if (editVersion !== sourceEditVersion) {
      return;
    }

    populateForm(saved);
    renderDiscovery(saved.discovery);
    setStatus(`Saved admin configuration to ${saved.writableConfigurationPath}`);
  })();

  sourceSavePromise = currentSave;
  try {
    await currentSave;
  } finally {
    if (sourceSavePromise === currentSave) {
      sourceSavePromise = null;
    }
  }
}

async function reloadConfiguration() {
  window.clearTimeout(autoSaveTimer);
  autoSaveTimer = 0;
  if (sourceSavePromise) {
    await sourceSavePromise;
  }

  // Edits can arrive while a save is in flight; catch up every observed version before reloading.
  while (lastSavedSourceEditVersion < sourceEditVersion) {
    await saveConfiguration();
  }

  await loadConfiguration();
}

async function loadUsers() {
  const users = await fetchJson('/api/admin/users');
  adminUsersElement.replaceChildren();
  for (const user of users) {
    const row = document.createElement('tr');
    appendCell(row, user.username);
    appendCell(row, user.displayName);
    appendCell(row, formatDate(user.createdUtc));
    appendCell(row, formatDate(user.updatedUtc));
    adminUsersElement.appendChild(row);
  }
}

async function loadSessions() {
  const [sessions, liveSession] = await Promise.all([
    fetchJson('/api/admin/sessions?limit=50'),
    loadLiveSessionSnapshot(),
  ]);
  const workspace = buildSessionWorkspace(sessions, liveSession);
  latestSessionWorkspace = workspace;
  renderLiveSessionSurface(workspace.liveSession, workspace.activeSessionSummary);
  renderSessionCollection(recentSessionsSurfaceElement, workspace.recentSessions, t('sessions.noRecent'), {
    originTab: 'recent',
    includeDeleteAction: false,
  });
  renderOlderSessionGroups(workspace.olderGroups);

  const availableSessionIds = new Set([
    ...workspace.recentSessions.map(session => session.sessionId),
    ...workspace.olderSessions.map(session => session.sessionId),
    workspace.activeSessionSummary?.sessionId,
  ].filter(Boolean));
  pruneSessionResultTabs(availableSessionIds);
  await refreshSessionResultTabs(availableSessionIds);
  renderSelectedSessionWorkspace();

  const selectableSessions = [...workspace.recentSessions, ...workspace.olderSessions];
  if (selectableSessions.length === 0) {
    sessionsStatusElement.textContent = t('sessions.empty');
    return;
  }

  sessionsStatusElement.textContent = t('sessions.loadedSummary', {
    recent: workspace.recentSessions.length,
    older: workspace.olderSessions.length,
  });
}

async function refreshSessionResultTabs(availableSessionIds) {
  const tabsToRefresh = openSessionResultTabs.filter(tab => availableSessionIds.has(tab.sessionId));
  if (tabsToRefresh.length === 0) {
    return;
  }

  const refreshResults = await Promise.all(tabsToRefresh.map(async tab => {
    try {
      const session = await fetchJson(`/api/admin/sessions/${encodeURIComponent(tab.sessionId)}`);
      return { sessionId: tab.sessionId, session };
    } catch (error) {
      return { sessionId: tab.sessionId, error };
    }
  }));

  const missingSessionIds = new Set();
  for (const refreshResult of refreshResults) {
    if (refreshResult.session) {
      const tab = getSessionResultTabBySessionId(refreshResult.sessionId)
        ?? getSessionResultTabBySessionId(refreshResult.session.sessionId);
      if (tab) {
        syncSessionResultTabDetail(tab, refreshResult.session);
      }
      continue;
    }

    if (isNotFoundRequestError(refreshResult.error)) {
      missingSessionIds.add(refreshResult.sessionId);
    }
  }

  if (missingSessionIds.size > 0) {
    const validSessionIds = new Set(openSessionResultTabs
      .map(tab => tab.sessionId)
      .filter(sessionId => !missingSessionIds.has(sessionId)));
    pruneSessionResultTabs(validSessionIds);
  }
}

function isNotFoundRequestError(error) {
  return error?.status === 404;
}

async function loadSessionDetail(sessionId, { activateTab = true, updateStatus = true, originTab = null } = {}) {
  const session = await fetchJson(`/api/admin/sessions/${encodeURIComponent(sessionId)}`);
  upsertSessionResultTab(session, { originTab, activateTab });
  renderSelectedSessionWorkspace();
  if (updateStatus) {
    setStatus(t('sessions.viewing', { lastSeen: formatDate(session.lastSeenUtc) }));
  }
}

async function setSessionCountForHistory(sessionId, countForHistory) {
  const session = await putJson(`/api/admin/sessions/${encodeURIComponent(sessionId)}/history`, { countForHistory });
  upsertSessionResultTab(session, { activateTab: false });
  setStatus(countForHistory ? t('sessions.includedMessage') : t('sessions.excludedMessage'));
  await loadSessions();
  await loadLeaderboards();
}

async function deleteHistoricalSession(sessionId, { returnTab = null } = {}) {
  const sessionSummary = findWorkspaceSessionById(sessionId)
    ?? getSessionResultTabBySessionId(sessionId)?.cachedDetail
    ?? getActiveSessionDetail();
  const deletionProtectionKey = getSessionDeletionProtectionKey(sessionSummary, latestSessionWorkspace);
  if (deletionProtectionKey) {
    setStatus(t(deletionProtectionKey), true);
    return;
  }

  if (!window.confirm(t('sessions.confirmDelete'))) {
    return;
  }

  await deleteJson(`/api/admin/sessions/${encodeURIComponent(sessionId)}`);
  closeSessionResultTabBySessionId(sessionId, { preferredTab: returnTab ?? 'recent', restoreFocus: true });

  setStatus(t('sessions.deleted'));
  await loadSessions();
  await loadLeaderboards();
}

async function deleteEmptyHistoricalSessions() {
  if (!window.confirm(t('sessions.confirmDeleteEmpty'))) {
    return;
  }

  const result = await deleteJson('/api/admin/sessions/empty');
  setStatus(t('sessions.emptyDeleted').replace('{count}', result.deletedCount ?? 0));
  await loadSessions();
  await loadLeaderboards();
}

function renderSelectedSessionWorkspace() {
  const activeResultTab = getActiveSessionResultTab();
  const selectedSessionDetail = activeResultTab?.cachedDetail ?? null;
  const isManageResultsMode = Boolean(activeResultTab?.isManageResultsMode);
  const isCompareVisible = Boolean(activeResultTab?.isCompareVisible);

  toggleManageResultsButton.textContent = isManageResultsMode ? t('sessions.manageClose') : t('sessions.manage');
  toggleManageResultsButton.classList.toggle('active', isManageResultsMode);
  toggleManageResultsButton.setAttribute('aria-pressed', String(isManageResultsMode));

  if (!selectedSessionDetail) {
    sessionParticipantRowsElement.replaceChildren();
    sessionParticipantDetailSurfaceElement.replaceChildren();
    sessionCompareSurfaceElement.hidden = true;
    selectedSessionMetricsElement.replaceChildren();
    if (activeSessionWorkspaceTab === 'results') {
      activeSessionWorkspaceTab = resolveSessionOverviewReturnTab();
    }
    renderSessionWorkspaceNavigation();
    highlightSelectedSessionSelection();
    return;
  }

  const topParticipant = (selectedSessionDetail.participants ?? [])[0];
  selectedSessionTitleElement.textContent = `${selectedSessionDetail.trackName} - ${selectedSessionDetail.sessionKind}`;
  selectedSessionSummaryElement.textContent = isManageResultsMode
    ? t('sessions.manageHint')
    : t('sessions.readOnly');
  renderSessionMetrics(selectedSessionDetail, topParticipant);
  renderSessionParticipants(selectedSessionDetail.participants ?? [], activeResultTab);
  renderParticipantDetail(selectedSessionDetail.participants ?? [], selectedSessionDetail, activeResultTab);
  renderCompareSurface(selectedSessionDetail.participants ?? [], isCompareVisible);
  renderSessionWorkspaceNavigation();
  highlightSelectedSessionSelection();
}

function renderSessionParticipants(participants, activeResultTab) {
  const selectedParticipantId = activeResultTab?.selectedParticipantId ?? null;
  const isManageResultsMode = Boolean(activeResultTab?.isManageResultsMode);

  sessionParticipantRowsElement.replaceChildren();
  if (participants.length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 6;
    cell.textContent = t('sessions.noParticipants');
    row.appendChild(cell);
    sessionParticipantRowsElement.appendChild(row);
    return;
  }

  for (const participant of participants) {
    const row = document.createElement('tr');
    const isSelected = participant.participantId === selectedParticipantId;
    row.classList.toggle('selectedRow', isSelected);
    appendCell(row, participant.rank);
    appendCell(row, participant.effectiveDisplayName || participant.displayName);
    appendCell(row, `P${participant.rank}`);
    appendCell(row, formatSeconds(participant.bestLapSeconds));
    appendCell(row, participant.completedLaps);
    appendActionsCell(row, [
      {
        label: isSelected
          ? t('sessions.hideDriver')
          : (isManageResultsMode ? t('sessions.editDriver') : t('sessions.view')),
        onClick: () => toggleParticipantDetail(participant.participantId),
      },
    ]);
    sessionParticipantRowsElement.appendChild(row);
  }
}

function renderParticipantDetail(participants, selectedSessionDetail, activeResultTab) {
  const selectedParticipantId = activeResultTab?.selectedParticipantId ?? null;
  const isManageResultsMode = Boolean(activeResultTab?.isManageResultsMode);

  sessionParticipantDetailSurfaceElement.replaceChildren();

  if (isManageResultsMode && selectedSessionDetail) {
    sessionParticipantDetailSurfaceElement.appendChild(renderSessionManagePanel(selectedSessionDetail, activeResultTab));
  }

  if (!selectedParticipantId) {
    const placeholder = document.createElement('p');
    placeholder.className = 'wideField';
    placeholder.textContent = t('sessions.selectDriver');
    sessionParticipantDetailSurfaceElement.appendChild(placeholder);
    return;
  }

  const participant = participants.find(entry => entry.participantId === selectedParticipantId);
  if (!participant) {
    return;
  }

  const panel = document.createElement('section');
  panel.className = 'participantDetailPanel';

  const header = document.createElement('div');
  header.className = 'sessionWorkspaceHeader';
  const headerCopy = document.createElement('div');
  const title = document.createElement('h3');
  title.textContent = participant.effectiveDisplayName || participant.displayName;
  const subtitle = document.createElement('p');
  subtitle.textContent = `${participant.rigName} - ${participant.vehicleName}`;
  headerCopy.append(title, subtitle);
  header.appendChild(headerCopy);
  panel.appendChild(header);

  const summary = document.createElement('div');
  summary.className = 'sessionMetrics';
  summary.appendChild(createMetricCard(t('sessions.bestLap'), formatSeconds(participant.bestLapSeconds)));
  summary.appendChild(createMetricCard(t('sessions.laps'), String(participant.completedLaps ?? 0)));
  summary.appendChild(createMetricCard(t('sessions.lastLap'), formatSeconds(participant.lastLapSeconds)));
  summary.appendChild(createMetricCard(t('sessions.counted'), `${participant.validTimedLapCount}/${participant.lapCount}`));
  panel.appendChild(summary);

  if (isManageResultsMode) {
    panel.appendChild(renderParticipantManageForm(participant));
  }

  panel.appendChild(renderLapTable(participant, isManageResultsMode));
  sessionParticipantDetailSurfaceElement.appendChild(panel);
}

function renderLapTable(participant, isManageResultsMode) {
  const table = document.createElement('table');
  table.className = 'lapCorrectionTable';
  const head = document.createElement('thead');
  const headRow = document.createElement('tr');
  const headings = ['Lap', 'Time', t('sessions.counted'), t('sessions.observed')];
  if (isManageResultsMode) {
    headings.push(t('sessions.invalid'), t('sessions.reason'), '');
  }

  headings.forEach(label => {
    const cell = document.createElement('th');
    cell.textContent = label;
    headRow.appendChild(cell);
  });
  head.appendChild(headRow);
  table.appendChild(head);

  const body = document.createElement('tbody');
  for (const lap of participant.laps ?? []) {
    const row = document.createElement('tr');
    appendCell(row, lap.lapNumber);
    appendCell(row, formatSeconds(lap.effectiveLapSeconds));
    appendCell(row, lap.countsForTiming ? 'Yes' : 'No');
    appendCell(row, formatDate(lap.observedUtc));
    if (isManageResultsMode) {
      const invalidCheckbox = appendPlainCheckboxCell(row, lap.staffInvalidated);
      const reasonInput = appendTextInputCell(row, lap.correctionReason ?? '');
      appendButtonCell(row, 'Save', () => saveLapCorrection(lap.lapId, lap.lapSecondsOverride ?? '', invalidCheckbox.checked, reasonInput.value).catch(showError));
    }
    body.appendChild(row);
  }

  if ((participant.laps ?? []).length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = isManageResultsMode ? 7 : 4;
    cell.textContent = t('participants.noCompletedLaps');
    row.appendChild(cell);
    body.appendChild(row);
  }

  table.appendChild(body);
  return table;
}

async function saveParticipantCorrection(participantId, displayNameOverride, excludedFromHistory) {
  const activeResultTab = getActiveSessionResultTab();
  if (!activeResultTab?.sessionId) {
    return;
  }

  const session = await putJson(`/api/admin/sessions/${encodeURIComponent(activeResultTab.sessionId)}/participants/${participantId}/correction`, {
    displayNameOverride: nullIfEmpty(displayNameOverride),
    excludedFromHistory,
    reason: excludedFromHistory ? 'Staff excluded participant' : null,
  });
  upsertSessionResultTab(session, { activateTab: false });
  renderSelectedSessionWorkspace();
  await loadLeaderboards();
  setStatus(t('sessions.participantCorrectionSaved'));
}

async function saveLapCorrection(lapId, lapSecondsOverride, staffInvalidated, reason) {
  const activeResultTab = getActiveSessionResultTab();
  if (!activeResultTab?.sessionId) {
    return;
  }

  const parsedOverride = parseLapSecondsInput(lapSecondsOverride);
  const session = await putJson(`/api/admin/sessions/${encodeURIComponent(activeResultTab.sessionId)}/laps/${lapId}/correction`, {
    lapSecondsOverride: parsedOverride,
    staffInvalidated,
    reason: nullIfEmpty(reason) ?? (staffInvalidated ? 'Staff invalidated lap' : null),
  });
  upsertSessionResultTab(session, { activateTab: false });
  renderSelectedSessionWorkspace();
  await loadLeaderboards();
  setStatus(t('sessions.lapCorrectionSaved'));
}

function parseLapSecondsInput(value) {
  const trimmed = value.trim();
  if (!trimmed) return null;
  if (/^\d+(?:\.\d+)?$/.test(trimmed)) {
    const seconds = Number.parseFloat(trimmed);
    if (seconds > 0) return seconds;
  }

  const minuteMatch = /^(\d+):([0-5]?\d(?:\.\d+)?)$/.exec(trimmed);
  if (minuteMatch) {
    const minutes = Number.parseInt(minuteMatch[1], 10);
    const seconds = Number.parseFloat(minuteMatch[2]);
    const totalSeconds = minutes * 60 + seconds;
    if (totalSeconds > 0) return totalSeconds;
  }

  throw new Error('Lap correction must be blank, seconds, or m:ss.mmm.');
}

function toggleParticipantDetail(participantId) {
  const activeResultTab = getActiveSessionResultTab();
  if (!activeResultTab) {
    return;
  }

  activeResultTab.selectedParticipantId = activeResultTab.selectedParticipantId === participantId
    ? null
    : participantId;
  renderSelectedSessionWorkspace();
}

function toggleManageResults() {
  const activeResultTab = getActiveSessionResultTab();
  if (!activeResultTab) {
    return;
  }

  activeResultTab.isManageResultsMode = !activeResultTab.isManageResultsMode;
  renderSelectedSessionWorkspace();
}

function toggleCompareDrivers() {
  const activeResultTab = getActiveSessionResultTab();
  if (!activeResultTab) {
    return;
  }

  activeResultTab.isCompareVisible = !activeResultTab.isCompareVisible;
  renderSelectedSessionWorkspace();
}

function normalizeSessionWorkspaceOverviewTab(tabName) {
  return tabName === 'live' || tabName === 'recent' || tabName === 'older'
    ? tabName
    : 'recent';
}

function resolveSessionOverviewReturnTab(preferredTab = null, fallbackOriginTab = null) {
  return normalizeSessionWorkspaceOverviewTab(
    preferredTab
    ?? fallbackOriginTab
    ?? getActiveSessionResultTab()?.originOverviewTab
    ?? activeSessionWorkspaceTab,
  );
}

function createSessionResultTabState(sessionDetail, originTab = null) {
  return {
    tabId: sessionDetail.sessionId,
    sessionId: sessionDetail.sessionId,
    cachedDetail: sessionDetail,
    originOverviewTab: normalizeSessionWorkspaceOverviewTab(originTab),
    selectedParticipantId: null,
    isManageResultsMode: false,
    isCompareVisible: false,
  };
}

function syncSessionResultTabDetail(tab, sessionDetail) {
  const previousTabId = tab.tabId;
  tab.tabId = sessionDetail.sessionId;
  tab.sessionId = sessionDetail.sessionId;
  tab.cachedDetail = sessionDetail;
  if (activeSessionResultTabId === previousTabId) {
    activeSessionResultTabId = tab.tabId;
  }
  if (!(sessionDetail.participants ?? []).some(participant => participant.participantId === tab.selectedParticipantId)) {
    tab.selectedParticipantId = null;
  }
}

function getSessionResultTabIndexBySessionId(sessionId) {
  return openSessionResultTabs.findIndex(tab => tab.sessionId === sessionId);
}

function getSessionResultTabBySessionId(sessionId) {
  const tabIndex = getSessionResultTabIndexBySessionId(sessionId);
  return tabIndex >= 0 ? openSessionResultTabs[tabIndex] : null;
}

function getSessionResultTabById(tabId) {
  return openSessionResultTabs.find(tab => tab.tabId === tabId) ?? null;
}

function getActiveSessionResultTab() {
  return activeSessionResultTabId
    ? getSessionResultTabById(activeSessionResultTabId)
    : null;
}

function getActiveSessionDetail() {
  return getActiveSessionResultTab()?.cachedDetail ?? null;
}

function upsertSessionResultTab(sessionDetail, { originTab = null, activateTab = true } = {}) {
  if (!sessionDetail?.sessionId) {
    return null;
  }

  const existingTab = getSessionResultTabBySessionId(sessionDetail.sessionId);
  if (existingTab) {
    syncSessionResultTabDetail(existingTab, sessionDetail);
    if (originTab) {
      existingTab.originOverviewTab = normalizeSessionWorkspaceOverviewTab(originTab);
    }
    if (activateTab) {
      activateSessionResultTab(existingTab.tabId);
    }
    return existingTab;
  }

  const createdTab = createSessionResultTabState(sessionDetail, originTab);
  openSessionResultTabs.push(createdTab);
  if (activateTab) {
    activateSessionResultTab(createdTab.tabId);
  }
  return createdTab;
}

function activateSessionResultTab(tabId, { activateWorkspace = true } = {}) {
  const tab = getSessionResultTabById(tabId);
  if (!tab) {
    return;
  }

  activeSessionResultTabId = tab.tabId;
  if (activateWorkspace) {
    activeSessionWorkspaceTab = 'results';
  }
  renderSelectedSessionWorkspace();
  highlightSelectedSessionSelection();
}

function resolveAdjacentResultTabId(previousTabs, closingTabId, remainingTabIds) {
  const closingIndex = previousTabs.findIndex(tab => tab.tabId === closingTabId);
  if (closingIndex < 0) {
    return null;
  }

  for (let index = closingIndex + 1; index < previousTabs.length; index += 1) {
    if (remainingTabIds.has(previousTabs[index].tabId)) {
      return previousTabs[index].tabId;
    }
  }

  for (let index = closingIndex - 1; index >= 0; index -= 1) {
    if (remainingTabIds.has(previousTabs[index].tabId)) {
      return previousTabs[index].tabId;
    }
  }

  return null;
}

function closeSessionResultTab(tabId, { preferredTab = null, restoreFocus = false } = {}) {
  const closingIndex = openSessionResultTabs.findIndex(tab => tab.tabId === tabId);
  if (closingIndex < 0) {
    return;
  }

  const closingTab = openSessionResultTabs[closingIndex];
  const wasActive = activeSessionResultTabId === tabId;
  const workspaceWasShowingResults = activeSessionWorkspaceTab === 'results';
  const nextTabId = openSessionResultTabs[closingIndex + 1]?.tabId
    ?? openSessionResultTabs[closingIndex - 1]?.tabId
    ?? null;
  let focusTabId = wasActive ? activeSessionResultTabId : (nextTabId ?? activeSessionResultTabId);
  let focusOverviewTab = null;

  openSessionResultTabs.splice(closingIndex, 1);

  if (wasActive) {
    // Active-tab closure follows right-neighbor, then left-neighbor, then origin overview fallback.
    if (nextTabId && getSessionResultTabById(nextTabId)) {
      activeSessionResultTabId = nextTabId;
      if (workspaceWasShowingResults) {
        activeSessionWorkspaceTab = 'results';
      }
      focusTabId = nextTabId;
    } else {
      activeSessionResultTabId = null;
      if (activeSessionWorkspaceTab === 'results') {
        activeSessionWorkspaceTab = resolveSessionOverviewReturnTab(preferredTab, closingTab.originOverviewTab);
        focusOverviewTab = activeSessionWorkspaceTab;
      }
    }
  }

  renderSelectedSessionWorkspace();
  if (restoreFocus) {
    focusSessionWorkspaceTab(focusTabId, focusOverviewTab);
  }
}

function closeSessionResultTabBySessionId(sessionId, options = {}) {
  const tab = getSessionResultTabBySessionId(sessionId);
  if (!tab) {
    return;
  }

  closeSessionResultTab(tab.tabId, options);
}

function pruneSessionResultTabs(validSessionIds, { preferredOverviewTab = null } = {}) {
  if (openSessionResultTabs.length === 0) {
    return false;
  }

  const previousTabs = [...openSessionResultTabs];
  const previousActiveTabId = activeSessionResultTabId;
  const remainingTabs = previousTabs.filter(tab => validSessionIds.has(tab.sessionId));
  if (remainingTabs.length === previousTabs.length) {
    return false;
  }

  const remainingTabIds = new Set(remainingTabs.map(tab => tab.tabId));
  const removedActiveTab = previousTabs.find(tab => tab.tabId === previousActiveTabId) ?? null;
  const workspaceWasShowingResults = activeSessionWorkspaceTab === 'results';

  openSessionResultTabs = remainingTabs;

  if (previousActiveTabId && !remainingTabIds.has(previousActiveTabId)) {
    const adjacentTabId = resolveAdjacentResultTabId(previousTabs, previousActiveTabId, remainingTabIds);
    if (adjacentTabId) {
      activeSessionResultTabId = adjacentTabId;
      if (workspaceWasShowingResults) {
        activeSessionWorkspaceTab = 'results';
      }
    } else {
      activeSessionResultTabId = null;
      if (activeSessionWorkspaceTab === 'results') {
        activeSessionWorkspaceTab = resolveSessionOverviewReturnTab(preferredOverviewTab, removedActiveTab?.originOverviewTab);
      }
    }
  } else if (!previousActiveTabId && remainingTabs.length > 0) {
    activeSessionResultTabId = remainingTabs[0].tabId;
  }

  return true;
}

function buildSessionResultTabLabels(sessionDetail) {
  const track = String(sessionDetail?.trackName ?? '-');
  const kind = String(sessionDetail?.sessionKind ?? '-');
  return {
    track,
    kind,
    title: t('sessions.resultTabTitle', { track, kind }),
    tabAriaLabel: t('sessions.resultTabAria', { track, kind }),
    closeAriaLabel: t('sessions.resultTabCloseAria', { track, kind }),
  };
}

function sessionResultTabDomId(tabId) {
  return `sessionResultTab-${encodeURIComponent(tabId).replaceAll('%', '_')}`;
}

function getSessionWorkspaceTabButtons() {
  return [
    sessionOverviewLiveTabButtonElement,
    sessionOverviewRecentTabButtonElement,
    sessionOverviewOlderTabButtonElement,
    ...openSessionResultTabsRailElement.querySelectorAll('.sessionWorkspaceResultTabButton'),
  ].filter(button => !button.hidden);
}

function handleSessionWorkspaceTabKeydown(event) {
  if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
    return;
  }

  const buttons = getSessionWorkspaceTabButtons();
  const currentIndex = buttons.indexOf(event.currentTarget);
  if (currentIndex < 0 || buttons.length === 0) {
    return;
  }

  event.preventDefault();
  const targetIndex = event.key === 'Home'
    ? 0
    : event.key === 'End'
      ? buttons.length - 1
      : (currentIndex + (event.key === 'ArrowRight' ? 1 : -1) + buttons.length) % buttons.length;
  const target = buttons[targetIndex];
  target.focus();
  target.click();
}

function focusSessionWorkspaceTab(resultTabId, overviewTabName) {
  window.requestAnimationFrame(() => {
    const resultButton = resultTabId
      ? [...openSessionResultTabsRailElement.querySelectorAll('.sessionWorkspaceResultTabButton')]
        .find(button => button.dataset.resultTabId === resultTabId)
      : null;
    if (resultButton) {
      resultButton.focus();
      return;
    }

    const overviewButton = overviewTabName === 'live'
      ? sessionOverviewLiveTabButtonElement
      : overviewTabName === 'older'
        ? sessionOverviewOlderTabButtonElement
        : sessionOverviewRecentTabButtonElement;
    overviewButton.focus();
  });
}

function renderSessionResultTabs() {
  openSessionResultTabsRailElement.replaceChildren();
  openSessionResultTabsRailElement.hidden = openSessionResultTabs.length === 0;
  if (openSessionResultTabs.length === 0) {
    return;
  }

  for (const tab of openSessionResultTabs) {
    const labels = buildSessionResultTabLabels(tab.cachedDetail);
    const isActive = activeSessionWorkspaceTab === 'results' && tab.tabId === activeSessionResultTabId;

    const wrapper = document.createElement('div');
    wrapper.className = 'sessionWorkspaceResultTab';
    wrapper.setAttribute('role', 'presentation');
    wrapper.classList.toggle('active', isActive);
    wrapper.dataset.resultTabId = tab.tabId;
    wrapper.dataset.sessionId = tab.sessionId;

    const tabButton = document.createElement('button');
    tabButton.type = 'button';
    tabButton.className = 'sessionWorkspaceTab sessionWorkspaceResultTabButton';
    tabButton.id = sessionResultTabDomId(tab.tabId);
    tabButton.dataset.resultTabId = tab.tabId;
    tabButton.dataset.sessionId = tab.sessionId;
    tabButton.setAttribute('role', 'tab');
    tabButton.setAttribute('aria-controls', 'selectedSessionPanel');
    tabButton.title = labels.title;
    tabButton.setAttribute('aria-label', labels.tabAriaLabel);
    setSessionWorkspaceTabState(tabButton, isActive);

    const labelElement = document.createElement('span');
    labelElement.className = 'sessionWorkspaceResultTabLabel';
    labelElement.textContent = labels.track;
    labelElement.title = labels.title;

    const badgeElement = document.createElement('span');
    badgeElement.className = 'sessionWorkspaceResultTabBadge';
    badgeElement.textContent = labels.kind;

    tabButton.append(labelElement, badgeElement);
    tabButton.addEventListener('click', () => activateSessionResultTab(tab.tabId));
    tabButton.addEventListener('keydown', handleSessionWorkspaceTabKeydown);

    const closeButton = document.createElement('button');
    closeButton.type = 'button';
    closeButton.className = 'sessionWorkspaceResultTabClose';
    closeButton.dataset.resultTabId = tab.tabId;
    closeButton.dataset.sessionId = tab.sessionId;
    closeButton.title = labels.closeAriaLabel;
    closeButton.setAttribute('aria-label', labels.closeAriaLabel);
    const closeIcon = document.createElement('span');
    closeIcon.setAttribute('aria-hidden', 'true');
    closeIcon.textContent = '\u00d7';
    closeButton.appendChild(closeIcon);
    closeButton.addEventListener('click', event => {
      event.stopPropagation();
      closeSessionResultTab(tab.tabId, { restoreFocus: true });
    });

    wrapper.append(tabButton, closeButton);
    openSessionResultTabsRailElement.appendChild(wrapper);
  }
}

function activateSessionWorkspaceTab(tabName) {
  if (tabName === 'results') {
    const activeResultTab = getActiveSessionResultTab();
    if (!activeResultTab) {
      return;
    }

    activeSessionWorkspaceTab = 'results';
    renderSessionWorkspaceNavigation();
    return;
  }

  activeSessionWorkspaceTab = normalizeSessionWorkspaceOverviewTab(tabName);
  renderSessionWorkspaceNavigation();
}

function renderSessionWorkspaceNavigation() {
  if (!getActiveSessionResultTab() && openSessionResultTabs.length > 0) {
    activeSessionResultTabId = openSessionResultTabs[0].tabId;
  }

  let activeTab = activeSessionWorkspaceTab;
  if (activeTab === 'results' && !getActiveSessionResultTab()) {
    activeTab = resolveSessionOverviewReturnTab();
    activeSessionWorkspaceTab = activeTab;
  }

  setSessionWorkspaceTabState(sessionOverviewLiveTabButtonElement, activeTab === 'live');
  setSessionWorkspaceTabState(sessionOverviewRecentTabButtonElement, activeTab === 'recent');
  setSessionWorkspaceTabState(sessionOverviewOlderTabButtonElement, activeTab === 'older');
  renderSessionResultTabs();

  const activeResultTab = getActiveSessionResultTab();
  if (activeTab === 'results' && activeResultTab) {
    selectedSessionPanelElement.setAttribute('aria-labelledby', sessionResultTabDomId(activeResultTab.tabId));
  } else {
    selectedSessionPanelElement.removeAttribute('aria-labelledby');
  }

  sessionOverviewLivePanelElement.hidden = activeTab !== 'live';
  sessionOverviewRecentPanelElement.hidden = activeTab !== 'recent';
  sessionOverviewOlderPanelElement.hidden = activeTab !== 'older';
  selectedSessionPanelElement.hidden = activeTab !== 'results' || !Boolean(getActiveSessionDetail());
  sessionWorkspaceUtilityRowElement.hidden = activeTab === 'results' && Boolean(getActiveSessionResultTab());
}

function setSessionWorkspaceTabState(button, isActive) {
  button.classList.toggle('active', isActive);
  button.setAttribute('aria-selected', String(isActive));
  button.tabIndex = isActive ? 0 : -1;
}

function openTelemetryWorkspace() {
  setStatus(t('sessions.telemetryPending'));
}

async function loadLiveSessionSnapshot() {
  try {
    const snapshot = await fetchJson('/api/live-session/current');
    return isLiveSessionSnapshotUsable(snapshot) ? snapshot : null;
  } catch {
    return null;
  }
}

function isLiveSessionSnapshotUsable(snapshot) {
  return Boolean(snapshot?.session?.trackName);
}

function isLiveSessionActive(snapshot) {
  if (!isLiveSessionSnapshotUsable(snapshot)) {
    return false;
  }

  return isSessionPhaseActive(snapshot.session.phase);
}

function isSessionPhaseActive(phase) {
  return !['SessionOver', 'Garage', 'Unknown'].includes(String(phase));
}

function isLiveSnapshotFresh(snapshot, nowMs) {
  if (!isLiveSessionSnapshotUsable(snapshot)) {
    return false;
  }

  const snapshotMs = new Date(snapshot.timestampUtc).getTime();
  return Number.isFinite(snapshotMs) && Math.abs(snapshotMs - nowMs) <= activeSessionFreshWindowMs;
}

function isSessionMatchingLiveSnapshot(session, liveSession) {
  if (!session || !isLiveSessionSnapshotUsable(liveSession)) {
    return false;
  }

  return namesEqual(session.trackName ?? '', liveSession.session.trackName ?? '')
    && String(session.sessionKind) === String(liveSession.session.kind);
}

function buildSessionWorkspace(sessions, liveSession) {
  const now = Date.now();
  const activeSessionSummary = findActiveSessionSummary(sessions, liveSession, now);
  const remainingSessions = sessions
    .filter(session => session.sessionId !== activeSessionSummary?.sessionId)
    .filter(session => !isPlaceholderSession(session))
    .sort((left, right) => new Date(right.lastSeenUtc) - new Date(left.lastSeenUtc));

  const recentCutoff = now - recentSessionWindowMs;
  const recentSessions = remainingSessions
    .filter(session => new Date(session.lastSeenUtc).getTime() >= recentCutoff)
    .slice(0, 4);
  const recentIds = new Set(recentSessions.map(session => session.sessionId));
  const olderSessions = remainingSessions.filter(session => !recentIds.has(session.sessionId));

  return {
    liveSession,
    activeSessionSummary,
    recentSessions,
    olderSessions,
    olderGroups: groupOlderSessions(olderSessions),
    nowEpochMs: now,
    recentCutoffEpochMs: recentCutoff,
  };
}

function findActiveSessionSummary(sessions, liveSession, now) {
  if (!isLiveSnapshotFresh(liveSession, now) || !isSessionPhaseActive(liveSession?.session?.phase)) {
    return null;
  }

  return sessions
    .filter(session => isSessionMatchingLiveSnapshot(session, liveSession))
    .filter(session => Math.abs(new Date(session.lastSeenUtc).getTime() - now) <= activeSessionFreshWindowMs)
    .sort((left, right) => new Date(right.lastSeenUtc) - new Date(left.lastSeenUtc))[0] ?? null;
}

function findWorkspaceSessionById(sessionId) {
  if (!latestSessionWorkspace || !sessionId) {
    return null;
  }

  const allSessions = [
    ...(latestSessionWorkspace.recentSessions ?? []),
    ...(latestSessionWorkspace.olderSessions ?? []),
  ];

  if (latestSessionWorkspace.activeSessionSummary) {
    allSessions.push(latestSessionWorkspace.activeSessionSummary);
  }

  return allSessions.find(session => session.sessionId === sessionId) ?? null;
}

function getSessionDeletionProtectionKey(session, workspace = latestSessionWorkspace) {
  if (!session || !workspace) {
    return null;
  }

  const nowMs = workspace.nowEpochMs ?? Date.now();
  const lastSeenMs = new Date(session.lastSeenUtc).getTime();
  const isDetectedActiveSession = session.sessionId
    && workspace.activeSessionSummary?.sessionId
    && session.sessionId === workspace.activeSessionSummary.sessionId;

  if (isDetectedActiveSession) {
    return 'sessions.deleteBlockedActive';
  }

  const liveSession = workspace.liveSession;
  if (Number.isFinite(lastSeenMs)
    && Math.abs(lastSeenMs - nowMs) <= activeSessionFreshWindowMs
    && isLiveSnapshotFresh(liveSession, nowMs)
    && isSessionPhaseActive(liveSession?.session?.phase)
    && isSessionMatchingLiveSnapshot(session, liveSession)) {
    return 'sessions.deleteBlockedActive';
  }

  if (Number.isFinite(lastSeenMs) && lastSeenMs >= nowMs - recentSessionWindowMs) {
    return 'sessions.deleteBlockedRecent';
  }

  return null;
}

function canDeleteSessionFromWorkspace(session, workspace = latestSessionWorkspace) {
  return !getSessionDeletionProtectionKey(session, workspace);
}

function isPlaceholderSession(session) {
  return (!session.trackName || session.trackName === 'Unknown track')
    || (session.lapCount === 0 && session.validTimedLapCount === 0 && ['Garage', 'Unknown'].includes(String(session.sessionPhase)));
}

function groupOlderSessions(sessions) {
  const groups = new Map([
    ['today', { key: 'today', label: t('sessions.groupToday'), sessions: [] }],
    ['yesterday', { key: 'yesterday', label: t('sessions.groupYesterday'), sessions: [] }],
    ['older', { key: 'older', label: t('sessions.groupOlder'), sessions: [] }],
  ]);

  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const yesterday = new Date(today);
  yesterday.setDate(yesterday.getDate() - 1);

  for (const session of sessions) {
    const lastSeen = new Date(session.lastSeenUtc);
    lastSeen.setHours(lastSeen.getHours(), lastSeen.getMinutes(), lastSeen.getSeconds(), lastSeen.getMilliseconds());
    const bucket = lastSeen >= today
      ? 'today'
      : lastSeen >= yesterday
        ? 'yesterday'
        : 'older';
    groups.get(bucket).sessions.push(session);
  }

  return [...groups.values()].filter(group => group.sessions.length > 0);
}

function renderLiveSessionSurface(liveSession, activeSessionSummary) {
  liveSessionSurfaceElement.replaceChildren();
  if (!isLiveSessionActive(liveSession)) {
    liveSessionSurfaceElement.appendChild(createEmptyWorkspaceNote(t('sessions.noLive')));
    return;
  }

  liveSessionSurfaceElement.appendChild(createLiveSessionCard(liveSession, activeSessionSummary));
}

function renderSessionCollection(container, sessions, emptyMessage, options = {}) {
  container.replaceChildren();
  if (sessions.length === 0) {
    container.appendChild(createEmptyWorkspaceNote(emptyMessage));
    return;
  }

  for (const session of sessions) {
    container.appendChild(createSessionCard(session, options));
  }

  highlightSelectedSessionSelection();
}

function renderOlderSessionGroups(groups) {
  olderSessionsSurfaceElement.replaceChildren();
  if (groups.length === 0) {
    olderSessionsSurfaceElement.appendChild(createEmptyWorkspaceNote(t('sessions.noOlder')));
    return;
  }

  for (const group of groups) {
    const details = document.createElement('details');
    details.className = 'sessionArchiveGroup';
    details.open = group.key === 'today';

    const summary = document.createElement('summary');
    summary.textContent = `${group.label} (${group.sessions.length})`;
    details.appendChild(summary);

    const cards = document.createElement('div');
    cards.className = 'sessionCardGrid';
    group.sessions.forEach(session => cards.appendChild(createSessionCard(session, {
      originTab: 'older',
      includeDeleteAction: true,
    })));
    details.appendChild(cards);
    olderSessionsSurfaceElement.appendChild(details);
  }

  highlightSelectedSessionSelection();
}

function createLiveSessionCard(liveSession, activeSessionSummary) {
  const actions = activeSessionSummary?.sessionId
    ? [{
      label: t('sessions.openResults'),
      onClick: () => loadSessionDetail(activeSessionSummary.sessionId, { originTab: 'live' }).catch(showError),
    }]
    : [];

  return createSessionOverviewRow({
    sessionId: activeSessionSummary?.sessionId ?? null,
    trackName: liveSession.session.trackName,
    descriptor: formatSessionDescriptor(liveSession.session.kind, liveSession.session.phase),
    facts: [
      { label: t('sessions.driverCount'), value: String(liveSession.session.vehicleCount ?? 0) },
      { label: t('sessions.lastUpdated'), value: formatDate(liveSession.timestampUtc) },
    ],
    actions,
    extraClassName: 'liveSessionCard',
  });
}

function createSessionCard(session, { originTab = 'recent', includeDeleteAction = false } = {}) {
  const actions = [{
    label: t('sessions.openResults'),
    onClick: () => loadSessionDetail(session.sessionId, { originTab }).catch(showError),
  }];

  if (includeDeleteAction && canDeleteSessionFromWorkspace(session, latestSessionWorkspace)) {
    actions.push({
      label: t('sessions.delete'),
      danger: true,
      onClick: () => deleteHistoricalSession(session.sessionId, { returnTab: 'older' }).catch(showError),
    });
  }

  return createSessionOverviewRow({
    sessionId: session.sessionId,
    trackName: session.trackName,
    descriptor: formatSessionDescriptor(session.sessionKind, session.sessionPhase),
    facts: [
      { label: t('sessions.driverCount'), value: String(session.participantCount ?? 0) },
      { label: t('sessions.bestLap'), value: formatSeconds(session.bestLapSeconds) },
      { label: t('sessions.lastUpdated'), value: formatDate(session.lastSeenUtc) },
    ],
    actions,
  });
}

function createSessionOverviewRow({ sessionId, trackName, descriptor, facts, actions = [], extraClassName = '' }) {
  const row = document.createElement('article');
  row.className = `sessionSummaryCard ${extraClassName}`.trim();
  if (sessionId) {
    row.dataset.sessionId = sessionId;
  }

  const titleRow = document.createElement('div');
  titleRow.className = 'sessionSummaryRowTitle';
  const title = document.createElement('h4');
  title.textContent = trackName;
  titleRow.appendChild(title);

  if (descriptor) {
    const subtitle = document.createElement('p');
    subtitle.textContent = descriptor;
    titleRow.appendChild(subtitle);
  }

  const metaRow = document.createElement('div');
  metaRow.className = 'sessionSummaryRowMeta';
  facts.filter(fact => fact?.value).forEach((fact, index) => metaRow.appendChild(createSessionFact(fact.label, fact.value, index + 1)));

  if (actions.length > 0) {
    const actionRow = document.createElement('div');
    actionRow.className = 'buttonRow';
    for (const action of actions) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = action.danger ? 'dangerButton' : '';
      button.textContent = action.label;
      button.addEventListener('click', action.onClick);
      actionRow.appendChild(button);
    }
    metaRow.appendChild(actionRow);
  }

  row.append(titleRow, metaRow);
  return row;
}

function createSessionFact(label, value, columnIndex) {
  const fact = document.createElement('span');
  fact.className = 'sessionSummaryFact';
  fact.style.setProperty('--session-summary-column', String(columnIndex));
  const labelElement = document.createElement('span');
  labelElement.className = 'sessionSummaryFactLabel';
  labelElement.textContent = `${label}:`;
  const valueElement = document.createElement('strong');
  valueElement.textContent = value;
  fact.append(labelElement, valueElement);
  return fact;
}

function formatSessionDescriptor(sessionKind, sessionPhase) {
  if (!sessionPhase || String(sessionPhase) === 'SessionOver') {
    return String(sessionKind ?? '');
  }

  return `${sessionKind} - ${sessionPhase}`;
}

function createMetricCard(label, value) {
  const metric = document.createElement('div');
  metric.className = 'sessionMetricCard';
  const labelElement = document.createElement('span');
  labelElement.textContent = label;
  const valueElement = document.createElement('strong');
  valueElement.textContent = value ?? '-';
  metric.append(labelElement, valueElement);
  return metric;
}

function createTag(label, tone) {
  const tag = document.createElement('span');
  tag.className = `sessionTag ${tone}`;
  tag.textContent = label;
  return tag;
}

function createEmptyWorkspaceNote(message) {
  const note = document.createElement('p');
  note.className = 'sessionWorkspaceEmpty';
  note.textContent = message;
  return note;
}

function renderSessionMetrics(session, topParticipant) {
  selectedSessionMetricsElement.replaceChildren();
  selectedSessionMetricsElement.appendChild(createMetricCard(t('sessions.track'), session.trackName));
  selectedSessionMetricsElement.appendChild(createMetricCard(t('sessions.kind'), String(session.sessionKind)));
  selectedSessionMetricsElement.appendChild(createMetricCard(t('sessions.topDriver'), topParticipant?.effectiveDisplayName ?? topParticipant?.displayName ?? '-'));
  selectedSessionMetricsElement.appendChild(createMetricCard(t('sessions.bestLap'), formatSeconds(session.bestLapSeconds)));
  selectedSessionMetricsElement.appendChild(createMetricCard(t('sessions.driverCount'), String(session.participantCount ?? 0)));
  selectedSessionMetricsElement.appendChild(createMetricCard(t('sessions.boardStatus'), session.countForHistory ? t('sessions.included') : t('sessions.excluded')));
}

function renderCompareSurface(participants, isCompareVisible) {
  sessionCompareSurfaceElement.replaceChildren();
  sessionCompareSurfaceElement.hidden = !isCompareVisible;
  if (!isCompareVisible) {
    return;
  }

  const title = document.createElement('h3');
  title.textContent = t('sessions.compare');
  const description = document.createElement('p');
  description.textContent = t('sessions.compareEmpty');
  sessionCompareSurfaceElement.append(title, description);

  const table = document.createElement('table');
  table.className = 'lapCorrectionTable';
  const head = document.createElement('thead');
  const headRow = document.createElement('tr');
  [t('sessions.driver'), t('sessions.best'), t('sessions.lastLap'), t('sessions.laps'), t('sessions.compareDelta')].forEach(label => {
    const cell = document.createElement('th');
    cell.textContent = label;
    headRow.appendChild(cell);
  });
  head.appendChild(headRow);
  table.appendChild(head);

  const validBestLaps = participants.map(participant => participant.bestLapSeconds).filter(Number.isFinite);
  const sessionBestLap = validBestLaps.length > 0 ? Math.min(...validBestLaps) : null;
  const body = document.createElement('tbody');
  for (const participant of participants) {
    const row = document.createElement('tr');
    appendCell(row, participant.effectiveDisplayName || participant.displayName);
    appendCell(row, formatSeconds(participant.bestLapSeconds));
    appendCell(row, formatSeconds(participant.lastLapSeconds));
    appendCell(row, participant.completedLaps);
    appendCell(row, sessionBestLap !== null && Number.isFinite(participant.bestLapSeconds)
      ? formatGap(participant.bestLapSeconds - sessionBestLap)
      : '-');
    body.appendChild(row);
  }
  table.appendChild(body);
  sessionCompareSurfaceElement.appendChild(table);
}

function renderSessionManagePanel(session, activeResultTab) {
  const panel = document.createElement('section');
  panel.className = 'participantDetailPanel sessionManagePanel';

  const title = document.createElement('h3');
  title.textContent = t('sessions.manage');
  const description = document.createElement('p');
  description.textContent = t('sessions.manageHint');
  panel.append(title, description);

  const actions = document.createElement('div');
  actions.className = 'buttonRow';

  const includeButton = document.createElement('button');
  includeButton.type = 'button';
  includeButton.textContent = session.countForHistory ? t('sessions.exclude') : t('sessions.include');
  includeButton.addEventListener('click', () => setSessionCountForHistory(session.sessionId, !session.countForHistory).catch(showError));
  actions.appendChild(includeButton);

  if (canDeleteSessionFromWorkspace(session, latestSessionWorkspace)) {
    const deleteButton = document.createElement('button');
    deleteButton.type = 'button';
    deleteButton.className = 'dangerButton';
    deleteButton.textContent = t('sessions.delete');
    deleteButton.addEventListener('click', () => deleteHistoricalSession(session.sessionId, {
      returnTab: activeResultTab?.originOverviewTab ?? 'recent',
    }).catch(showError));
    actions.appendChild(deleteButton);
  }

  panel.appendChild(actions);

  if (!canDeleteSessionFromWorkspace(session, latestSessionWorkspace)) {
    const protection = document.createElement('p');
    protection.className = 'wideField sessionDeleteProtection';
    protection.textContent = t(getSessionDeletionProtectionKey(session, latestSessionWorkspace) ?? 'sessions.deleteProtectedHint');
    panel.appendChild(protection);
  }

  return panel;
}

function renderParticipantManageForm(participant) {
  const wrapper = document.createElement('div');
  wrapper.className = 'sessionManageForm';

  const label = document.createElement('label');
  const labelTitle = document.createElement('span');
  labelTitle.textContent = t('sessions.correction');
  const nameInput = document.createElement('input');
  nameInput.type = 'text';
  nameInput.value = participant.displayNameOverride ?? '';
  nameInput.spellcheck = false;
  label.append(labelTitle, nameInput);
  wrapper.appendChild(label);

  const checkboxLabel = document.createElement('label');
  checkboxLabel.className = 'checkboxLabel';
  const excludedInput = document.createElement('input');
  excludedInput.type = 'checkbox';
  excludedInput.checked = Boolean(participant.excludedFromHistory);
  const checkboxText = document.createElement('span');
  checkboxText.textContent = t('sessions.exclude');
  checkboxLabel.append(excludedInput, checkboxText);
  wrapper.appendChild(checkboxLabel);

  const buttonRow = document.createElement('div');
  buttonRow.className = 'buttonRow';
  const saveButton = document.createElement('button');
  saveButton.type = 'button';
  saveButton.textContent = 'Save';
  saveButton.addEventListener('click', () => saveParticipantCorrection(participant.participantId, nameInput.value, excludedInput.checked).catch(showError));
  buttonRow.appendChild(saveButton);
  wrapper.appendChild(buttonRow);

  return wrapper;
}

function highlightSelectedSessionSelection() {
  const activeSessionId = getActiveSessionDetail()?.sessionId ?? null;
  document.querySelectorAll('.sessionSummaryCard[data-session-id]').forEach(element => {
    element.classList.toggle('selectedRow', element.dataset.sessionId === activeSessionId);
  });
}

async function loadSessionSetup() {
  const setup = await fetchJson('/api/admin/session-setup');
  driverProfiles = setup.driverProfiles ?? [];
  renderSessionSetup(setup);
}

async function saveSessionSetup(renderAfterSave = false) {
  const sequence = ++sessionSetupSaveSequence;
  const saved = await putJson('/api/admin/session-setup', { entries: readSessionSetupRows() });
  if (sequence !== sessionSetupSaveSequence) {
    return;
  }

  driverProfiles = saved.driverProfiles ?? [];
  if (renderAfterSave) {
    renderSessionSetup(saved);
  }
  setStatus(t('status.setupSaved'));
}

async function clearSessionSetup() {
  const cleared = await deleteJson('/api/admin/session-setup');
  driverProfiles = cleared.driverProfiles ?? [];
  renderSessionSetup(cleared);
  setStatus(t('status.setupCleared'));
}

async function createDriverProfile() {
  await postJson('/api/admin/driver-profiles', {
    displayName: profileDisplayNameElement.value.trim(),
    email: nullIfEmpty(profileEmailElement.value),
    notes: nullIfEmpty(profileNotesElement.value),
  });
  profileDisplayNameElement.value = '';
  profileEmailElement.value = '';
  profileNotesElement.value = '';
  await loadSessionSetup();
  setStatus(t('status.profileCreated'));
}

function renderSessionSetup(setup) {
  isRenderingSessionSetup = true;
  sessionSetupRowsElement.replaceChildren();
  const entries = setup.entries ?? [];
  const rows = entries.length === 0 ? defaultSetupRows() : entries;
  const rowsToRender = entries.length === 0 && setup.isConfigured ? [] : rows;
  for (const entry of rowsToRender) {
    appendSessionSetupRow(entry);
  }
  isRenderingSessionSetup = false;
}

function appendSessionSetupRow(entry) {
  const row = document.createElement('tr');
  row.className = 'sessionSetupRow';
  row.appendChild(inputCell('setupRigName', entry.rigName ?? ''));
  row.appendChild(inputCell('setupDisplayName', entry.displayName ?? ''));
  row.appendChild(profileSelectCell(entry.driverProfileId));
  const actions = document.createElement('td');
  const removeButton = document.createElement('button');
  removeButton.type = 'button';
  removeButton.textContent = t('setup.remove');
  removeButton.addEventListener('click', () => {
    row.remove();
    scheduleSessionSetupAutoSave(0);
  });
  actions.appendChild(removeButton);
  row.appendChild(actions);
  sessionSetupRowsElement.appendChild(row);
  row.querySelectorAll('input, select').forEach(element => {
    element.addEventListener('input', () => scheduleSessionSetupAutoSave(700));
    element.addEventListener('change', () => scheduleSessionSetupAutoSave(0));
  });
}

function inputCell(className, value) {
  const cell = document.createElement('td');
  const input = document.createElement('input');
  input.className = className;
  input.type = 'text';
  input.value = value;
  input.spellcheck = false;
  cell.appendChild(input);
  return cell;
}

function profileSelectCell(selectedProfileId) {
  const cell = document.createElement('td');
  const select = document.createElement('select');
  select.className = 'setupProfileId';
  const empty = document.createElement('option');
  empty.value = '';
  empty.textContent = t('setup.noProfile');
  select.appendChild(empty);
  for (const profile of driverProfiles) {
    const option = document.createElement('option');
    option.value = profile.driverProfileId;
    option.textContent = profile.displayName;
    option.selected = profile.driverProfileId === selectedProfileId;
    select.appendChild(option);
  }
  cell.appendChild(select);
  return cell;
}

function readSessionSetupRows() {
  return Array.from(sessionSetupRowsElement.querySelectorAll('.sessionSetupRow'))
    .map(row => ({
      rigName: row.querySelector('.setupRigName')?.value.trim() ?? '',
      displayName: row.querySelector('.setupDisplayName')?.value.trim() ?? '',
      driverProfileId: nullIfEmpty(row.querySelector('.setupProfileId')?.value ?? ''),
    }))
    .filter(entry => entry.rigName && entry.displayName);
}

function defaultSetupRows() {
  return ['Setup1', 'Setup2', 'Setup3', 'Setup4', 'Setup5'].map(rigName => ({ rigName, displayName: '', driverProfileId: null }));
}

function nextRigName() {
  return `Setup${sessionSetupRowsElement.querySelectorAll('.sessionSetupRow').length + 1}`;
}

function scheduleSessionSetupAutoSave(delayMilliseconds) {
  if (isRenderingSessionSetup) {
    return;
  }

  window.clearTimeout(sessionSetupSaveTimer);
  setStatus(delayMilliseconds === 0 ? t('status.setupSaving') : t('status.setupChanged'));
  sessionSetupSaveTimer = window.setTimeout(() => {
    saveSessionSetup(false).catch(showError);
  }, delayMilliseconds);
}

async function loadLeaderboards() {
  const [monthlyTrack, monthlyBoard] = await Promise.all([
    fetchJson('/api/leaderboards/monthly-track'),
    fetchJson('/api/leaderboards/best-laps?window=monthly&mode=per-driver&limit=10'),
  ]);
  renderMonthlyTrack(monthlyTrack);
  renderMonthlyBestLaps(monthlyBoard.rows ?? []);
}

async function loadKioskSettings() {
  const settings = await fetchJson('/api/admin/kiosk');
  kioskDisplayModeElement.value = settings.defaultDisplayMode ?? 'Monthly';
}

async function loadDriverTrackerSettings() {
  const settings = await fetchJson('/api/admin/driver-tracker');
  isPopulatingDriverTrackerSettings = true;
  try {
    driverTrackerClientRefreshHzElement.value = String(settings.clientRefreshHz ?? 30);
    driverTrackerGeometryRecordingLapsElement.value = String(settings.geometryRecordingLaps ?? 1);
  } finally {
    isPopulatingDriverTrackerSettings = false;
  }
}

async function loadDriverTrackerTracks({ includeCurrentTrackGeometry = true, forceSelectedGeometryRefresh = false } = {}) {
  if (driverTrackerTracksLoadPromise) {
    return driverTrackerTracksLoadPromise;
  }

  const sequence = ++driverTrackerTracksLoadSequence;
  const abortController = new AbortController();
  driverTrackerTracksAbortController?.abort();
  driverTrackerTracksAbortController = abortController;

  const loadPromise = (async () => {
    try {
      const catalogPromise = fetchJson('/api/admin/driver-tracker/tracks', { signal: abortController.signal });
      const currentGeometryPromise = includeCurrentTrackGeometry
        ? fetchCurrentTrackGeometrySafe(abortController.signal)
        : Promise.resolve(null);
      const [catalog, currentGeometry] = await Promise.all([catalogPromise, currentGeometryPromise]);

      if (sequence !== driverTrackerTracksLoadSequence) {
        return;
      }

      latestDriverTrackerCatalog = catalog.tracks ?? [];
      if (includeCurrentTrackGeometry) {
        latestCurrentTrackGeometry = currentGeometry;
      }

      const selectionChanged = syncSelectedDriverTrackerTrack();
      const selectedCatalog = findTrackByName(latestDriverTrackerCatalog, selectedDriverTrackerTrackName);
      const selectedCatalogToken = buildSelectedDriverTrackerCatalogToken(selectedCatalog);
      let selectedGeometryWasUpdated = false;

      if (includeCurrentTrackGeometry
        && currentGeometry?.trackName
        && namesEqual(currentGeometry.trackName, selectedDriverTrackerTrackName)) {
        selectedTrackGeometryAbortController?.abort();
        selectedTrackGeometryAbortController = null;
        selectedTrackGeometrySequence += 1;
        latestSelectedTrackGeometry = currentGeometry;
        selectedDriverTrackerCatalogToken = selectedCatalogToken;
        selectedGeometryWasUpdated = true;
      }

      const shouldRefreshSelectedGeometry = Boolean(selectedDriverTrackerTrackName)
        && !selectedGeometryWasUpdated
        && (forceSelectedGeometryRefresh
          || selectionChanged
          || selectedCatalogToken !== selectedDriverTrackerCatalogToken);

      if (shouldRefreshSelectedGeometry) {
        await loadSelectedDriverTrackerGeometry({
          renderAfterLoad: false,
          expectedCatalogToken: selectedCatalogToken,
        });
      }

      if (sequence !== driverTrackerTracksLoadSequence) {
        return;
      }

      renderDriverTrackerTracks(latestDriverTrackerCatalog);
      renderDriverTrackerGeometryPanel();
      renderStatusDashboard();
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        return;
      }

      throw error;
    } finally {
      if (driverTrackerTracksAbortController === abortController) {
        driverTrackerTracksAbortController = null;
      }
    }
  })();

  driverTrackerTracksLoadPromise = loadPromise;
  try {
    await loadPromise;
  } finally {
    if (driverTrackerTracksLoadPromise === loadPromise) {
      driverTrackerTracksLoadPromise = null;
    }
  }
}

async function refreshDriverTrackerTracksManually() {
  await refreshDriverTrackerTracksAfterCurrentLoad({
    includeCurrentTrackGeometry: true,
    forceSelectedGeometryRefresh: true,
  });
}

async function refreshDriverTrackerTracksAfterCurrentLoad(options) {
  if (driverTrackerTracksLoadPromise) {
    try {
      await driverTrackerTracksLoadPromise;
    } catch {
      // A fresh explicit refresh below gets its own chance after a failed background poll.
    }
  }

  await loadDriverTrackerTracks(options);
}

async function saveLocalizationChoice(language) {
  setLanguage(language);
  await putJson('/api/admin/localization', { defaultLanguage: language });
}

async function loadLanguageChoice() {
  const language = localStorage.getItem(languageStorageKey);
  if (language && translations[language]) {
    setLanguage(language);
    return;
  }

  try {
    const localization = await fetchJson('/api/admin/localization');
    if (localization?.defaultLanguage) {
      setLanguage(localization.defaultLanguage);
      return;
    }
  } catch {
    // fallback to local storage or default language silently
  }

  setLanguage('en');
}

async function saveKioskSettings() {
  const settings = await putJson('/api/admin/kiosk', { defaultDisplayMode: kioskDisplayModeElement.value });
  kioskDisplayModeElement.value = settings.defaultDisplayMode ?? 'Monthly';
  setStatus(t('status.kioskSaved'));
}

async function saveDriverTrackerSettings() {
  const sequence = ++driverTrackerSaveSequence;
  const clientRefreshHz = Number(driverTrackerClientRefreshHzElement.value);
  const geometryRecordingLaps = Number(driverTrackerGeometryRecordingLapsElement.value);
  setStatus(t('status.driverTrackerSaving'));
  const settings = await putJson('/api/admin/driver-tracker', { clientRefreshHz, geometryRecordingLaps });
  if (sequence !== driverTrackerSaveSequence) {
    return;
  }

  isPopulatingDriverTrackerSettings = true;
  try {
    driverTrackerClientRefreshHzElement.value = String(settings.clientRefreshHz ?? 30);
    driverTrackerGeometryRecordingLapsElement.value = String(settings.geometryRecordingLaps ?? 1);
  } finally {
    isPopulatingDriverTrackerSettings = false;
  }

  setStatus(t('status.driverTrackerSaved'));
}

function renderDriverTrackerTracks(tracks) {
  driverTrackerTrackRowsElement.replaceChildren();
  if (tracks.length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 8;
    cell.textContent = t('driverTracker.noTracks');
    row.appendChild(cell);
    driverTrackerTrackRowsElement.appendChild(row);
    return;
  }

  for (const track of tracks) {
    const trackName = track.trackName ?? '';
    const row = document.createElement('tr');
    const isSelected = namesEqual(trackName, selectedDriverTrackerTrackName);
    row.classList.toggle('selectedRow', isSelected);
    row.tabIndex = trackName ? 0 : -1;
    row.setAttribute('aria-selected', String(isSelected));
    row.addEventListener('keydown', event => {
      if ((event.target instanceof Element && event.target.closest('button'))
        || !trackName
        || (event.key !== 'Enter' && event.key !== ' ')) {
        return;
      }

      event.preventDefault();
      selectDriverTrackerTrack(trackName);
    });
    row.addEventListener('click', event => {
      if (event.target instanceof Element && event.target.closest('button')) {
        return;
      }

      if (!trackName) {
        return;
      }

      selectDriverTrackerTrack(trackName);
    });

    appendCell(row, track.trackName ?? '-');
    appendCell(row, driverTrackerStateLabel(deriveDriverTrackerState(track)));
    appendCell(row, formatDriverTrackerCoverage(track));
    appendCell(row, `${track.recordedLapCount ?? 0}/${track.targetCompletedLaps ?? 1}`);
    appendCell(row, formatDriverTrackerSamples(track));
    appendCell(row, formatDate(track.updatedUtc));
    appendCell(row, track.statusDetail ?? '-');
    const actions = [
      {
        label: t('driverTracker.improve'),
        title: t('driverTracker.improveTitle'),
        ariaLabel: t('driverTracker.improveAria', { track: trackName }),
        onClick: () => {
          if (!trackName) {
            return;
          }

          startDriverTrackerRecording(trackName, false).catch(showError);
        },
      },
      {
        label: t('driverTracker.restart'),
        title: t('driverTracker.restartTitle'),
        ariaLabel: t('driverTracker.restartAria', { track: trackName }),
        onClick: () => {
          if (!trackName) {
            return;
          }

          startDriverTrackerRecording(trackName, true).catch(showError);
        },
      },
    ];
    const hasOutlineData = Boolean(track.hasGeometry || track.isRecording)
      || Number(track.sampleCount ?? 0) > 0
      || Number(track.candidateSampleCount ?? 0) > 0;
    if (hasOutlineData) {
      actions.push({
        label: t('driverTracker.delete'),
        title: t('driverTracker.deleteTitle'),
        ariaLabel: t('driverTracker.deleteAria', { track: trackName }),
        danger: true,
        onClick: () => {
          if (!trackName) {
            return;
          }

          deleteDriverTrackerOutline(trackName).catch(showError);
        },
      });
    }
    appendActionsCell(row, actions);
    driverTrackerTrackRowsElement.appendChild(row);
  }
}

function formatDriverTrackerCoverage(track) {
  if (!track) {
    return '-';
  }

  const stored = `${Number(track.coveragePercent ?? 0).toFixed(1)}%`;
  const candidate = Number(track.candidateCoveragePercent ?? 0);
  return candidate > 0 && !track.hasGeometry
    ? `${stored} / ${candidate.toFixed(1)}% candidate`
    : stored;
}

function formatDriverTrackerSamples(track) {
  if (!track) {
    return '-';
  }

  const stored = Number(track.sampleCount ?? 0);
  const candidate = Number(track.candidateSampleCount ?? 0);
  return candidate > 0
    ? `${stored} / ${candidate} candidate`
    : String(stored);
}

function deriveDriverTrackerState(track) {
  if (!track) {
    return 'unavailable';
  }

  if (track.isRecording) {
    return 'recording';
  }

  if (track.hasGeometry) {
    return 'complete';
  }

  const hasPartialProgress = Number(track.coveragePercent ?? 0) > 0
    || Number(track.sampleCount ?? 0) > 0
    || Number(track.candidateCoveragePercent ?? 0) > 0
    || Number(track.candidateSampleCount ?? 0) > 0
    || Number(track.recordedLapCount ?? 0) > 0;

  return hasPartialProgress ? 'partial' : 'unavailable';
}

function deriveDriverTrackerStateForSelection(track, currentGeometry, selectedIsCurrentTrack) {
  if (!selectedIsCurrentTrack) {
    return deriveDriverTrackerState(track);
  }

  if (track?.isRecording) {
    return 'recording';
  }

  if (currentGeometry?.isAvailable || track?.hasGeometry) {
    return 'complete';
  }

  const hasPartialFromCurrent = Number(currentGeometry?.coveragePercent ?? 0) > 0
    || Number(currentGeometry?.sampleCount ?? 0) > 0
    || Boolean(currentGeometry?.isCompleteLap)
    || Number(track?.candidateCoveragePercent ?? 0) > 0
    || Number(track?.candidateSampleCount ?? 0) > 0;

  return hasPartialFromCurrent ? 'partial' : 'unavailable';
}

function driverTrackerStateLabel(state) {
  switch (state) {
    case 'recording':
      return t('driverTracker.recording');
    case 'complete':
      return t('driverTracker.complete');
    case 'partial':
      return t('driverTracker.partial');
    default:
      return t('driverTracker.unavailable');
  }
}

function namesEqual(left, right) {
  return typeof left === 'string'
    && typeof right === 'string'
    && left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0;
}

function sameTrackName(left, right) {
  if (!left && !right) {
    return true;
  }

  if (typeof left !== 'string' || typeof right !== 'string') {
    return false;
  }

  return namesEqual(left, right);
}

function buildSelectedDriverTrackerCatalogToken(track) {
  if (!track?.trackName) {
    return null;
  }

  return `${track.trackName}|${track.updatedUtc ?? ''}|${deriveDriverTrackerState(track)}`;
}

function findTrackByName(tracks, trackName) {
  if (!trackName) {
    return null;
  }

  return tracks.find(track => namesEqual(track.trackName ?? '', trackName)) ?? null;
}

function syncSelectedDriverTrackerTrack() {
  const previousSelection = selectedDriverTrackerTrackName;

  if (findTrackByName(latestDriverTrackerCatalog, selectedDriverTrackerTrackName)) {
    return false;
  }

  const currentTrackName = latestCurrentTrackGeometry?.trackName ?? null;
  if (currentTrackName && findTrackByName(latestDriverTrackerCatalog, currentTrackName)) {
    selectedDriverTrackerTrackName = currentTrackName;
  } else if (currentTrackName && latestDriverTrackerCatalog.length === 0) {
    selectedDriverTrackerTrackName = currentTrackName;
  } else {
    selectedDriverTrackerTrackName = latestDriverTrackerCatalog[0]?.trackName ?? null;
  }

  const selectionChanged = !sameTrackName(previousSelection, selectedDriverTrackerTrackName);
  if (selectionChanged) {
    latestSelectedTrackGeometry = null;
    selectedDriverTrackerCatalogToken = null;
  }

  return selectionChanged;
}

function selectDriverTrackerTrack(trackName) {
  if (!trackName || namesEqual(trackName, selectedDriverTrackerTrackName)) {
    return;
  }

  selectedDriverTrackerTrackName = trackName;
  latestSelectedTrackGeometry = null;
  selectedDriverTrackerCatalogToken = null;
  renderDriverTrackerTracks(latestDriverTrackerCatalog);
  renderDriverTrackerGeometryPanel({ isLoading: true });
  const selectedCatalog = findTrackByName(latestDriverTrackerCatalog, trackName);
  loadSelectedDriverTrackerGeometry({
    expectedCatalogToken: buildSelectedDriverTrackerCatalogToken(selectedCatalog),
  }).catch(showError);
}

async function loadSelectedDriverTrackerGeometry({ renderAfterLoad = true, expectedCatalogToken = null } = {}) {
  const trackName = selectedDriverTrackerTrackName;
  const sequence = ++selectedTrackGeometrySequence;
  selectedTrackGeometryAbortController?.abort();
  selectedTrackGeometryAbortController = null;
  if (!trackName) {
    latestSelectedTrackGeometry = null;
    selectedDriverTrackerCatalogToken = null;
    if (renderAfterLoad) {
      renderDriverTrackerGeometryPanel();
    }
    return;
  }

  const abortController = new AbortController();
  selectedTrackGeometryAbortController = abortController;
  let geometry;
  try {
    geometry = await fetchSelectedTrackGeometrySafe(trackName, abortController.signal);
  } catch (error) {
    if (selectedTrackGeometryAbortController === abortController) {
      selectedTrackGeometryAbortController = null;
    }
    throw error;
  }

  if (abortController.signal.aborted) {
    if (selectedTrackGeometryAbortController === abortController) {
      selectedTrackGeometryAbortController = null;
    }
    return;
  }

  if (sequence !== selectedTrackGeometrySequence || !namesEqual(trackName, selectedDriverTrackerTrackName)) {
    return;
  }

  selectedTrackGeometryAbortController = null;
  latestSelectedTrackGeometry = geometry;
  selectedDriverTrackerCatalogToken = expectedCatalogToken
    ?? buildSelectedDriverTrackerCatalogToken(findTrackByName(latestDriverTrackerCatalog, selectedDriverTrackerTrackName));
  if (renderAfterLoad) {
    renderDriverTrackerGeometryPanel();
  }
}

function bindDriverTrackerAutoSave() {
  const trackerInputs = [driverTrackerClientRefreshHzElement, driverTrackerGeometryRecordingLapsElement];
  trackerInputs.forEach(element => {
    element.addEventListener('input', () => scheduleDriverTrackerAutoSave(700));
    element.addEventListener('change', () => scheduleDriverTrackerAutoSave(0));
    element.addEventListener('blur', () => scheduleDriverTrackerAutoSave(0));
  });
}

function scheduleDriverTrackerAutoSave(delayMilliseconds) {
  if (isPopulatingDriverTrackerSettings) {
    return;
  }

  window.clearTimeout(driverTrackerSaveTimer);
  setStatus(delayMilliseconds === 0 ? t('status.driverTrackerSaving') : t('status.driverTrackerChanged'));
  driverTrackerSaveTimer = window.setTimeout(() => {
    saveDriverTrackerSettings().catch(showError);
  }, delayMilliseconds);
}

function renderDriverTrackerGeometryPanel({ isLoading = false } = {}) {
  const selectedTrackName = selectedDriverTrackerTrackName;
  const selectedCatalog = findTrackByName(latestDriverTrackerCatalog, selectedTrackName);
  const selectedGeometry = latestSelectedTrackGeometry;

  if (!selectedTrackName) {
    driverTrackerSelectionHintElement.textContent = t('driverTracker.noSelection');
    renderStatusFacts(driverTrackerGeometryFactsElement, []);
    driverTrackerOutlinePolylineElement.setAttribute('points', '');
    driverTrackerOutlineMessageElement.hidden = false;
    driverTrackerOutlineMessageElement.textContent = t('driverTracker.noSelection');
    return;
  }

  driverTrackerSelectionHintElement.textContent = isLoading
    ? t('driverTracker.selectionLoading', { track: selectedTrackName })
    : t('driverTracker.selectionLoaded', { track: selectedTrackName });

  const selectedState = deriveDriverTrackerStateForSelection(selectedCatalog, selectedGeometry, true);
  const hasDrawableGeometry = Boolean(selectedGeometry?.isAvailable);
  const effectiveCoverage = hasDrawableGeometry
    ? `${Number(selectedGeometry.coveragePercent ?? 0).toFixed(1)}%`
    : formatDriverTrackerCoverage(selectedCatalog);
  const lapProgress = selectedCatalog
    ? `${selectedCatalog.recordedLapCount ?? 0}/${selectedCatalog.targetCompletedLaps ?? 1}`
    : '-';
  const sampleCount = hasDrawableGeometry
    ? String(selectedGeometry.sampleCount ?? 0)
    : formatDriverTrackerSamples(selectedCatalog);
  const updatedUtc = selectedGeometry?.updatedUtc ?? selectedCatalog?.updatedUtc;
  const detail = selectedGeometry?.statusDetail ?? selectedCatalog?.statusDetail ?? '-';
  const source = selectedGeometry?.source ?? selectedCatalog?.source ?? '-';

  renderStatusFacts(driverTrackerGeometryFactsElement, [
    { label: t('driverTracker.activeTrack'), value: selectedTrackName },
    { label: t('driverTracker.state'), value: driverTrackerStateLabel(selectedState) },
    { label: t('driverTracker.coverage'), value: effectiveCoverage },
    { label: t('driverTracker.lapProgress'), value: lapProgress },
    { label: t('driverTracker.samples'), value: sampleCount },
    { label: t('driverTracker.updated'), value: formatDate(updatedUtc) },
    { label: t('driverTracker.source'), value: source },
    { label: t('driverTracker.detail'), value: detail },
  ]);

  if (selectedGeometry?.isAvailable && Array.isArray(selectedGeometry.points) && selectedGeometry.points.length > 1) {
    const outlinePoints = selectedGeometry.points
      .map(point => `${(Math.min(1, Math.max(0, Number(point.x ?? 0))) * 84 + 8).toFixed(2)},${(Math.min(1, Math.max(0, Number(point.y ?? 0))) * 84 + 8).toFixed(2)}`)
      .join(' ');
    driverTrackerOutlinePolylineElement.setAttribute('points', outlinePoints);
    driverTrackerOutlineMessageElement.hidden = true;
    driverTrackerOutlineMessageElement.textContent = '';
    return;
  }

  driverTrackerOutlinePolylineElement.setAttribute('points', '');
  driverTrackerOutlineMessageElement.hidden = false;
  driverTrackerOutlineMessageElement.textContent = isLoading
    ? t('driverTracker.outlineLoading')
    : (selectedGeometry?.statusDetail || t('driverTracker.outlineUnavailable'));
}

async function fetchCurrentTrackGeometrySafe(signal) {
  try {
    return await fetchJson('/api/track-geometry/current', { signal });
  } catch {
    return null;
  }
}

async function fetchSelectedTrackGeometrySafe(trackName, signal) {
  try {
    return await fetchJson(`/api/admin/driver-tracker/geometry?trackName=${encodeURIComponent(trackName)}`, { signal });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      return null;
    }
    throw error;
  }
}

async function deleteDriverTrackerOutline(trackName) {
  if (!trackName) {
    return;
  }

  if (!window.confirm(t('driverTracker.confirmDeleteOutline', { track: trackName }))) {
    return;
  }

  await deleteJson(`/api/admin/driver-tracker/geometry?trackName=${encodeURIComponent(trackName)}`);
  if (namesEqual(trackName, selectedDriverTrackerTrackName)) {
    latestSelectedTrackGeometry = null;
    selectedDriverTrackerCatalogToken = null;
  }

  setStatus(t('status.driverTrackerOutlineDeleted', { track: trackName }));
  await refreshDriverTrackerTracksAfterCurrentLoad({
    includeCurrentTrackGeometry: false,
    forceSelectedGeometryRefresh: true,
  });
}

async function startDriverTrackerRecording(trackName, resetExistingGeometry) {
  if (!trackName) {
    return;
  }

  if (resetExistingGeometry && !window.confirm(t('driverTracker.confirmStartOver', { track: trackName }))) {
    return;
  }

  selectedDriverTrackerTrackName = trackName;
  selectedDriverTrackerCatalogToken = null;
  const targetCompletedLaps = Number(driverTrackerGeometryRecordingLapsElement.value || 1);
  await postJson('/api/admin/driver-tracker/recordings', {
    trackName,
    targetCompletedLaps,
    resetExistingGeometry,
  });
  setStatus(t('status.driverTrackerRecordingStarted'));
  await refreshDriverTrackerTracksAfterCurrentLoad({
    includeCurrentTrackGeometry: false,
    forceSelectedGeometryRefresh: true,
  });
}

async function runRetentionCleanup() {
  const result = await postJson('/api/admin/persistence/retention/cleanup', {});
  retentionCleanupStatusElement.textContent = t('retention.cleanupResult', {
    detailedLapRecordsDeleted: result.detailedLapRecordsDeleted ?? 0,
    sessionSummariesDeleted: result.sessionSummariesDeleted ?? 0,
    trackBestRecordsDeleted: result.trackBestRecordsDeleted ?? 0,
    monthlyTrackPeriodsDeleted: result.monthlyTrackPeriodsDeleted ?? 0,
  });
  await loadSessions();
  await loadLeaderboards();
}

async function setMonthlyTrack() {
  const trackName = monthlyTrackNameElement.value.trim();
  const reason = monthlyTrackReasonElement.value.trim();
  const monthlyTrack = await putJson('/api/admin/leaderboards/monthly-track', { trackName, reason });
  renderMonthlyTrack(monthlyTrack);
  monthlyTrackReasonElement.value = '';
  await loadLeaderboards();
  setStatus(t('status.monthlyTrackStarted'));
}

async function resetMonthlyTrack() {
  const reason = monthlyTrackReasonElement.value.trim() || 'Monthly track reset';
  const monthlyTrack = await postJson('/api/admin/leaderboards/monthly-track/reset', { reason });
  renderMonthlyTrack(monthlyTrack);
  monthlyTrackReasonElement.value = '';
  await loadLeaderboards();
  setStatus(t('status.monthlyTrackReset'));
}

function renderMonthlyTrack(monthlyTrack) {
  if (!monthlyTrack?.isActive) {
    monthlyTrackStatusElement.textContent = t('monthlyTrack.noActive');
    monthlyTrackNameElement.value = '';
    return;
  }

  monthlyTrackStatusElement.textContent = `${monthlyTrack.trackName} since ${formatDate(monthlyTrack.startedUtc)}`;
  monthlyTrackNameElement.value = monthlyTrack.trackName ?? '';
}

function renderMonthlyBestLaps(rows) {
  monthlyBestLapsElement.replaceChildren();
  if (rows.length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 6;
    cell.textContent = t('monthlyBests.empty');
    row.appendChild(cell);
    monthlyBestLapsElement.appendChild(row);
    return;
  }

  for (const lap of rows) {
    const row = document.createElement('tr');
    appendCell(row, lap.rank);
    appendCell(row, lap.displayName);
    appendCell(row, lap.rigName);
    appendCell(row, lap.lapNumber);
    appendCell(row, formatSeconds(lap.lapSeconds));
    appendCell(row, formatDate(lap.observedUtc));
    monthlyBestLapsElement.appendChild(row);
  }
}

async function createAdmin() {
  await postJson('/api/admin/users', {
    username: newAdminUsernameElement.value.trim(),
    displayName: newAdminDisplayNameElement.value.trim(),
    password: newAdminPasswordElement.value,
  });
  newAdminPasswordElement.value = '';
  await loadUsers();
  setStatus(t('status.adminCreated'));
}

async function changePassword() {
  const username = encodeURIComponent(passwordUsernameElement.value.trim());
  await putJson(`/api/admin/users/${username}/password`, { newPassword: newPasswordElement.value });
  newPasswordElement.value = '';
  await loadUsers();
  setStatus(t('status.passwordChanged'));
}

async function loadAdminStatus() {
  await refreshOperationalStatus();
}

async function refreshOperationalStatus() {
  if (isStatusRefreshBusy) {
    return;
  }

  isStatusRefreshBusy = true;
  try {
    latestAdminStatus = await fetchJson('/api/admin/status');
    latestLiveStatusSnapshot = await fetchLiveSessionSnapshotForStatus();

    try {
      const trackerCatalog = await fetchJson('/api/admin/driver-tracker/tracks');
      latestDriverTrackerCatalog = trackerCatalog.tracks ?? [];
    } catch {
      // Keep the last known catalog when this optional status-side fetch fails.
    }

    rawStatusElement.textContent = JSON.stringify(latestAdminStatus, null, 2);
    syncSelectedDriverTrackerTrack();
    const trackerTabActive = document.getElementById('trackerTab')?.classList.contains('active');
    if (trackerTabActive) {
      renderDriverTrackerTracks(latestDriverTrackerCatalog);
      renderDriverTrackerGeometryPanel();
    }
    renderStatusDashboard();
  } finally {
    isStatusRefreshBusy = false;
  }
}

function startStatusPolling() {
  if (isStatusPolling) {
    return;
  }

  isStatusPolling = true;
  refreshOperationalStatus().catch(showError);
  statusPollingTimer = window.setInterval(() => {
    refreshOperationalStatus().catch(showError);
  }, 1000);
}

function stopStatusPolling() {
  if (!isStatusPolling) {
    return;
  }

  isStatusPolling = false;
  window.clearInterval(statusPollingTimer);
  statusPollingTimer = 0;
}

function shouldPollDriverTrackerTracks() {
  return !dashboardPanel.hidden
    && document.visibilityState === 'visible'
    && document.getElementById('trackerTab')?.classList.contains('active');
}

function startDriverTrackerPolling() {
  if (isDriverTrackerPolling || !shouldPollDriverTrackerTracks()) {
    return;
  }

  isDriverTrackerPolling = true;
  pollDriverTrackerTracksOnce();
  driverTrackerPollingTimer = window.setInterval(() => {
    pollDriverTrackerTracksOnce();
  }, 1000);
}

function abortDriverTrackerTracksLoad() {
  if (!driverTrackerTracksAbortController) {
    return;
  }

  driverTrackerTracksAbortController.abort();
  driverTrackerTracksAbortController = null;
  driverTrackerTracksLoadSequence += 1;
}

function stopDriverTrackerPolling({ abortInFlight = true } = {}) {
  if (!isDriverTrackerPolling && driverTrackerPollingTimer === 0) {
    if (abortInFlight) {
      abortDriverTrackerTracksLoad();
    }
    return;
  }

  isDriverTrackerPolling = false;
  window.clearInterval(driverTrackerPollingTimer);
  driverTrackerPollingTimer = 0;
  if (abortInFlight) {
    abortDriverTrackerTracksLoad();
  }
}

function pollDriverTrackerTracksOnce() {
  if (!shouldPollDriverTrackerTracks()) {
    stopDriverTrackerPolling();
    return;
  }

  loadDriverTrackerTracks({ includeCurrentTrackGeometry: false }).catch(showError);
}

function handleDocumentVisibilityChange() {
  if (document.visibilityState !== 'visible') {
    stopDriverTrackerPolling();
    return;
  }

  startDriverTrackerPolling();
}

function handleTabChange(tabId) {
  if (tabId === 'statusTab') {
    stopDriverTrackerPolling();
    if (statusDiagnosticDetailsElement.open) {
      stopStatusPolling();
      refreshOperationalStatus().catch(showError);
    } else {
      startStatusPolling();
    }
    return;
  }

  stopStatusPolling();

  if (tabId === 'trackerTab') {
    loadDriverTrackerTracks({
      includeCurrentTrackGeometry: true,
      forceSelectedGeometryRefresh: true,
    }).catch(showError);
    startDriverTrackerPolling();
    return;
  }

  stopDriverTrackerPolling();
}

async function fetchLiveSessionSnapshotForStatus() {
  try {
    return await fetchJson('/api/live-session/current');
  } catch {
    return null;
  }
}

function renderStatusDashboard() {
  const health = latestAdminStatus?.health;
  const persistence = latestAdminStatus?.persistence;
  const trackerSummary = buildTrackerOperationalSummary();
  const trackName = latestLiveStatusSnapshot?.session?.trackName ?? health?.currentTrackName;
  const trackPhase = latestLiveStatusSnapshot?.session?.phase;
  const sourceState = latestLiveStatusSnapshot?.status ?? health?.currentSourceStatus ?? t('statusPanel.notAvailable');
  const persistenceState = persistence
    ? (persistence.isEnabled ? t('statusPanel.persistenceEnabled') : t('statusPanel.persistenceDisabled'))
    : t('statusPanel.notAvailable');

  statusUpdatedAtElement.textContent = `${t('statusPanel.lastUpdated')}: ${formatDate(health?.timestampUtc)}`;
  statusServiceStateElement.textContent = health?.serviceState ?? t('statusPanel.notAvailable');
  statusSourceModeElement.textContent = health?.sourceMode ?? t('statusPanel.notAvailable');
  statusSourceStateElement.textContent = sourceState;
  statusCurrentTrackElement.textContent = trackName
    ? (trackPhase ? `${trackName} (${trackPhase})` : trackName)
    : t('statusPanel.noActiveSession');
  statusPersistenceStateElement.textContent = persistenceState;
  statusPersistenceProviderElement.textContent = persistence?.provider ?? t('statusPanel.notAvailable');
  statusPersistenceLocationElement.textContent = persistence?.displayLocation ?? t('statusPanel.notAvailable');
  statusResultsStateElement.textContent = health?.currentSessionAvailable
    ? t('statusPanel.snapshotReady')
    : t('statusPanel.snapshotWaiting');
  statusTrackerStateElement.textContent = driverTrackerStateLabel(trackerSummary.state);
  statusTrackerTrackElement.textContent = trackerSummary.trackName;
  statusTrackerCoverageElement.textContent = trackerSummary.coverage;
  statusTrackerDetailElement.textContent = trackerSummary.detail;
}

function buildTrackerOperationalSummary() {
  const tracks = latestDriverTrackerCatalog ?? [];
  if (tracks.length === 0) {
    return {
      state: 'unavailable',
      trackName: t('statusPanel.noActiveSession'),
      coverage: '-',
      detail: t('driverTracker.noTracks'),
    };
  }

  const counts = { unavailable: 0, recording: 0, partial: 0, complete: 0 };
  tracks.forEach(track => {
    counts[deriveDriverTrackerState(track)] += 1;
  });

  const overallState = counts.recording > 0
    ? 'recording'
    : counts.partial > 0
      ? 'partial'
      : counts.complete > 0
        ? 'complete'
        : 'unavailable';

  const liveTrackName = latestLiveStatusSnapshot?.session?.trackName ?? latestAdminStatus?.health?.currentTrackName;
  const focusedTrack = findTrackByName(tracks, liveTrackName)
    ?? tracks.find(track => deriveDriverTrackerState(track) === overallState)
    ?? tracks[0];
  const summaryDetail = t('statusPanel.trackerMix', {
    complete: counts.complete,
    partial: counts.partial,
    recording: counts.recording,
  });
  const detail = focusedTrack?.statusDetail
    ? `${focusedTrack.statusDetail} ${summaryDetail}`
    : summaryDetail;

  return {
    state: overallState,
    trackName: focusedTrack?.trackName ?? t('statusPanel.noActiveSession'),
    coverage: formatDriverTrackerCoverage(focusedTrack),
    detail,
  };
}

function renderFixturePathDiagnostic() {
  fixturePathDiagnosticElement.value = sourceFixturePathValue || t('advancedConfig.fixturePathUnset');
  if (!latestAdminStatus) {
    rawStatusElement.textContent = t('statusPanel.noStatus');
  }
}

function renderStatusFacts(container, facts) {
  container.replaceChildren();
  for (const fact of facts) {
    const item = document.createElement('div');
    item.className = 'statusFact';
    const label = document.createElement('span');
    label.textContent = fact.label;
    const value = document.createElement('strong');
    value.textContent = fact.value ?? '-';
    item.append(label, value);
    container.appendChild(item);
  }
}

function populateForm(configuration) {
  isPopulatingSourceForm = true;
  const sharedMemory = configuration.sharedMemory ?? {};
  sourceModeElement.value = configuration.mode ?? 'Fixture';
  sourceFixturePathValue = configuration.fixturePath ?? '';
  sourceDriverAliases = configuration.driverAliases ?? {};
  scoringMapNameElement.value = sharedMemory.scoringMapName ?? '';
  processIdElement.value = sharedMemory.processId ?? '';
  autoDiscoverElement.checked = sharedMemory.autoDiscover ?? true;
  processNamesElement.value = (sharedMemory.dedicatedServerProcessNames ?? []).join(', ');
  multipleMapPolicyElement.value = sharedMemory.multipleScoringMapPolicy ?? 'RequireExplicitSelection';
  scoringPollHzElement.value = sharedMemory.scoringPollHz ?? 10;
  telemetryEnabledElement.checked = sharedMemory.telemetry?.enabled ?? false;
  telemetryPollHzElement.value = sharedMemory.telemetry?.pollHz ?? 100;
  renderFixturePathDiagnostic();
  hasLoadedSourceConfiguration = true;
  isPopulatingSourceForm = false;
}

function readSourceConfigurationForm() {
  const processId = processIdElement.value ? Number.parseInt(processIdElement.value, 10) : null;
  return {
    mode: sourceModeElement.value,
    fixturePath: sourceFixturePathValue,
    driverAliases: sourceDriverAliases,
    sharedMemory: {
      scoringMapName: nullIfEmpty(scoringMapNameElement.value),
      processId: Number.isFinite(processId) ? processId : null,
      autoDiscover: autoDiscoverElement.checked,
      dedicatedServerProcessNames: processNamesElement.value
        .split(',')
        .map(value => value.trim())
        .filter(Boolean),
      multipleScoringMapPolicy: multipleMapPolicyElement.value,
      scoringPollHz: Number.parseFloat(scoringPollHzElement.value),
      telemetry: {
        enabled: telemetryEnabledElement.checked,
        pollHz: Number.parseFloat(telemetryPollHzElement.value),
      },
    },
  };
}

function renderDiscovery(discovery) {
  discoveryStatusElement.textContent = discovery?.status ?? t('discovery.noResult');
  discoveryStatusElement.className = discovery?.isAmbiguous ? 'warningText' : '';
  renderList(candidateMapsElement, discovery?.candidateMapNames ?? [], value => value);
  renderList(discoveredMapsElement, discovery?.discoveredCandidates ?? [], formatCandidate);
  renderList(ambiguousMapsElement, discovery?.ambiguousCandidates ?? [], formatCandidate);
}

function renderList(element, values, formatter) {
  element.replaceChildren();
  if (values.length === 0) {
    const item = document.createElement('li');
    item.textContent = '-';
    element.appendChild(item);
    return;
  }

  for (const value of values) {
    const item = document.createElement('li');
    item.textContent = formatter(value);
    element.appendChild(item);
  }
}

function formatCandidate(candidate) {
  const pid = candidate.processId === null || candidate.processId === undefined ? t('discovery.noPid') : `PID ${candidate.processId}`;
  const process = candidate.processName ? `, ${candidate.processName}` : '';
  return `${candidate.mapName} (${pid}${process}, ${candidate.discoverySource})`;
}

function showSetup() {
  stopStatusPolling();
  stopDriverTrackerPolling();
  setupPanel.hidden = false;
  loginPanel.hidden = true;
  dashboardPanel.hidden = true;
  logoutButton.hidden = true;
  setStatus(t('status.createFirstAdmin'));
}

function showLogin() {
  stopStatusPolling();
  stopDriverTrackerPolling();
  setupPanel.hidden = true;
  loginPanel.hidden = false;
  dashboardPanel.hidden = true;
  logoutButton.hidden = true;
  setStatus(t('status.adminLoginRequired'));
}

function showDashboard(session) {
  stopDriverTrackerPolling();
  setupPanel.hidden = true;
  loginPanel.hidden = true;
  dashboardPanel.hidden = false;
  logoutButton.hidden = false;
  setStatus(t('status.signedIn', { name: session.displayName ?? session.username }));
  passwordUsernameElement.value = session.username ?? '';
  showTab(localStorage.getItem(tabStorageKey) || 'sessionSetupTab', false);
}

function showTab(tabId, persist = true) {
  // Existing browsers may still remember the removed Source tab; preserve intent by opening Advanced.
  const requestedTab = tabId === 'sourceTab' ? 'advancedTab' : tabId;
  const target = document.getElementById(requestedTab) ? requestedTab : 'sessionSetupTab';
  if (persist || tabId === 'sourceTab') {
    localStorage.setItem(tabStorageKey, target);
  }
  document.querySelectorAll('[data-tab]').forEach(button => {
    button.classList.toggle('active', button.dataset.tab === target);
  });
  document.querySelectorAll('.adminTab').forEach(tab => {
    tab.hidden = tab.id !== target;
    tab.classList.toggle('active', tab.id === target);
  });

  handleTabChange(target);
}

async function fetchJson(path, { signal } = {}) {
  const response = await fetch(path, { cache: 'no-store', credentials: 'same-origin', signal });
  return readJsonResponse(response);
}

async function postJson(path, payload) {
  const response = await fetch(path, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  return readJsonResponse(response);
}

async function putJson(path, payload) {
  const response = await fetch(path, {
    method: 'PUT',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  return readJsonResponse(response);
}

async function deleteJson(path) {
  const response = await fetch(path, { method: 'DELETE', credentials: 'same-origin' });
  return readJsonResponse(response);
}

async function readJsonResponse(response) {
  if (response.status === 401) {
    showLogin();
    throw new Error('Admin login required.');
  }

  if (!response.ok) {
    let detail = `${response.status} ${response.statusText}`;
    try {
      const body = await response.json();
      if (typeof body?.errorCode === 'string' && hasTranslationKey(body.errorCode)) {
        detail = t(body.errorCode);
      } else {
        detail = body.error ?? detail;
      }
    } catch {
    }
    const requestError = new Error(detail);
    requestError.status = response.status;
    throw requestError;
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

function appendCell(row, value) {
  const cell = document.createElement('td');
  cell.textContent = value ?? '-';
  row.appendChild(cell);
}

function appendPlainCheckboxCell(row, checked) {
  const cell = document.createElement('td');
  const checkbox = document.createElement('input');
  checkbox.className = 'tableCheckbox';
  checkbox.type = 'checkbox';
  checkbox.checked = Boolean(checked);
  cell.appendChild(checkbox);
  row.appendChild(cell);
  return checkbox;
}

function appendTextInputCell(row, value) {
  const cell = document.createElement('td');
  const input = document.createElement('input');
  input.className = 'tableInput';
  input.type = 'text';
  input.value = value ?? '';
  input.spellcheck = false;
  cell.appendChild(input);
  row.appendChild(cell);
  return input;
}

function appendButtonCell(row, label, onClick) {
  const cell = document.createElement('td');
  const button = document.createElement('button');
  button.className = 'tableButton';
  button.type = 'button';
  button.textContent = label;
  button.addEventListener('click', onClick);
  cell.appendChild(button);
  row.appendChild(cell);
}

function appendActionsCell(row, actions) {
  const cell = document.createElement('td');
  const wrapper = document.createElement('div');
  wrapper.className = 'tableActions';
  for (const action of actions) {
    const button = document.createElement('button');
    button.className = action.danger ? 'tableButton dangerButton' : 'tableButton';
    button.type = 'button';
    button.textContent = action.label;
    if (action.title) {
      button.title = action.title;
    }
    button.setAttribute('aria-label', action.ariaLabel ?? action.label);
    button.addEventListener('click', action.onClick);
    wrapper.appendChild(button);
  }

  cell.appendChild(wrapper);
  row.appendChild(cell);
}

function formatDate(value) {
  if (!value) return '-';
  return new Date(value).toLocaleString();
}

function formatSeconds(value) {
  if (value === null || value === undefined || !Number.isFinite(value)) return '-';
  const minutes = Math.floor(value / 60);
  const seconds = (value % 60).toFixed(3).padStart(6, '0');
  return `${minutes}:${seconds}`;
}

function formatGap(value) {
  if (value === null || value === undefined || !Number.isFinite(value)) return '-';
  if (value === 0) return '0.000';
  const prefix = value > 0 ? '+' : '-';
  return `${prefix}${Math.abs(value).toFixed(3)}`;
}

function nullIfEmpty(value) {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function showError(error) {
  setStatus(error instanceof Error ? error.message : String(error), true);
}

function bindSourceAutoSave() {
  const immediateElements = [sourceModeElement, autoDiscoverElement, multipleMapPolicyElement, telemetryEnabledElement];
  const debouncedElements = [scoringMapNameElement, processIdElement, processNamesElement, scoringPollHzElement, telemetryPollHzElement];

  immediateElements.forEach(element => {
    element.addEventListener('change', () => scheduleSourceAutoSave(0));
  });

  debouncedElements.forEach(element => {
    element.addEventListener('input', () => scheduleSourceAutoSave(700));
    element.addEventListener('change', () => scheduleSourceAutoSave(0));
  });
}

function scheduleSourceAutoSave(delayMilliseconds) {
  if (isPopulatingSourceForm || !hasLoadedSourceConfiguration) {
    return;
  }

  window.clearTimeout(autoSaveTimer);
  sourceEditVersion += 1;
  setStatus(delayMilliseconds === 0 ? t('status.sourceSaving') : t('status.sourceChanged'));
  autoSaveTimer = window.setTimeout(() => {
    autoSaveTimer = 0;
    saveConfiguration().catch(showError);
  }, delayMilliseconds);
}

function setStatus(message, isWarning = false) {
  statusElement.textContent = message;
  statusElement.className = isWarning ? 'warningText' : '';
}
