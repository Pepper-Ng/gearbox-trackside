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
const fixturePathElement = document.querySelector('#fixturePath');
const scoringMapNameElement = document.querySelector('#scoringMapName');
const processIdElement = document.querySelector('#processId');
const autoDiscoverElement = document.querySelector('#autoDiscover');
const processNamesElement = document.querySelector('#processNames');
const multipleMapPolicyElement = document.querySelector('#multipleMapPolicy');
const scoringPollHzElement = document.querySelector('#scoringPollHz');
const telemetryEnabledElement = document.querySelector('#telemetryEnabled');
const telemetryPollHzElement = document.querySelector('#telemetryPollHz');
const driverAliasesElement = document.querySelector('#driverAliases');
const refreshElement = document.querySelector('#refreshDiscovery');
const discoveryStatusElement = document.querySelector('#discoveryStatus');
const candidateMapsElement = document.querySelector('#candidateMaps');
const discoveredMapsElement = document.querySelector('#discoveredMaps');
const ambiguousMapsElement = document.querySelector('#ambiguousMaps');
const adminUsersElement = document.querySelector('#adminUsers');
const sessionsStatusElement = document.querySelector('#sessionsStatus');
const sessionRowsElement = document.querySelector('#sessionRows');
const selectedSessionTitleElement = document.querySelector('#selectedSessionTitle');
const sessionParticipantRowsElement = document.querySelector('#sessionParticipantRows');
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
const advancedStatusElement = document.querySelector('#advancedStatus');
const refreshStatusButton = document.querySelector('#refreshStatus');
let isPopulatingSourceForm = false;
let autoSaveTimer = 0;
let autoSaveSequence = 0;
let sessionSetupSaveTimer = 0;
let sessionSetupSaveSequence = 0;
let isRenderingSessionSetup = false;
let driverProfiles = [];
let selectedSessionId = null;

const languageStorageKey = 'trackside.admin.language';
const tabStorageKey = 'trackside.admin.tab';
const translations = {
  en: {
    'admin.title': 'Admin',
    'language.label': 'Language',
    'nav.kiosk': 'Kiosk',
    'nav.logout': 'Logout',
    'tabs.source': 'Source',
    'tabs.sessionSetup': 'Session Setup',
    'tabs.sessions': 'Sessions',
    'tabs.leaderboards': 'Leaderboards',
    'tabs.admins': 'Admins',
    'tabs.status': 'Status',
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
    'source.mode': 'Source mode',
    'source.fixturePath': 'Fixture path',
    'source.scoringMapName': 'Exact scoring map',
    'source.processId': 'Dedicated server PID',
    'source.autoDiscover': 'Auto-discover memory maps',
    'source.processNames': 'Dedicated server process names',
    'source.multipleMapPolicy': 'Multiple map policy',
    'source.requireExplicitSelection': 'Require explicit selection',
    'source.useFirstDiscovered': 'Use first discovered',
    'source.scoringPollHz': 'Scoring poll Hz',
    'source.telemetryEnabled': 'Enable telemetry loop scaffold',
    'source.telemetryPollHz': 'Telemetry poll Hz',
    'source.driverAliasesJson': 'Driver aliases JSON',
    'source.refresh': 'Refresh',
    'discovery.title': 'Shared Memory Discovery',
    'discovery.noResult': 'No discovery result yet.',
    'discovery.noPid': 'no PID',
    'kioskDisplay.title': 'Kiosk Display',
    'kioskDisplay.defaultMode': 'Default display mode',
    'kioskDisplay.saveButton': 'Save Display Mode',
    'kioskDisplay.monthly': 'Monthly',
    'kioskDisplay.weekly': 'Weekly',
    'kioskDisplay.daily': 'Daily',
    'kioskDisplay.lastSession': 'Last Session',
    'kioskDisplay.live': 'Live',
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
    'sessions.title': 'Session History',
    'sessions.description': 'Stored sessions used for historical boards. This is not the live-session monitor.',
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
    'sessions.include': 'Include',
    'sessions.exclude': 'Exclude',
    'sessions.delete': 'Delete',
    'sessions.included': 'Included',
    'sessions.excluded': 'Excluded',
    'sessions.detailTitle': 'Session Detail',
    'sessions.includedMessage': 'Session included in historical boards.',
    'sessions.excludedMessage': 'Session excluded from historical boards.',
    'sessions.correction': 'Correction',
    'sessions.exclude': 'Exclude',
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
    'aria.sourceConfiguration': 'Source configuration',
    'aria.sharedMemoryDiscovery': 'Shared-memory discovery',
    'changePassword.title': 'Change Password',
    'advancedStatus.refreshButton': 'Refresh Status',
    'sessions.empty': 'No persisted sessions yet.',
    'sessions.noParticipants': 'No participants persisted for this session.',
    'participants.noCompletedLaps': 'No completed laps persisted for this participant.',
    'sessions.deleted': 'Stored session deleted.',
    'sessions.emptyDeleted': 'Deleted {count} empty stored sessions.',
    'sessions.confirmDelete': 'Delete this stored session from history?',
    'sessions.confirmDeleteEmpty': 'Delete stored sessions that have no participants, no completed laps, or no known track?',
  },
  nl: {
    'admin.title': 'Beheer',
    'language.label': 'Taal',
    'nav.kiosk': 'Kiosk',
    'nav.logout': 'Uitloggen',
    'tabs.source': 'Bron',
    'tabs.sessionSetup': 'Sessie voorbereiden',
    'tabs.sessions': 'Sessies',
    'tabs.leaderboards': 'Klassementen',
    'tabs.admins': 'Beheerders',
    'tabs.status': 'Status',
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
    'source.mode': 'Bronmodus',
    'source.fixturePath': 'Fixture-pad',
    'source.scoringMapName': 'Exacte scorekaart',
    'source.processId': 'PID van dedicated server',
    'source.autoDiscover': 'Memory-kaarten automatisch ontdekken',
    'source.processNames': 'Procesnamen dedicated server',
    'source.multipleMapPolicy': 'Meerdere kaartbeleid',
    'source.requireExplicitSelection': 'Vereis expliciete selectie',
    'source.useFirstDiscovered': 'Gebruik eerste ontdekte',
    'source.scoringPollHz': 'Scoring poll Hz',
    'source.telemetryEnabled': 'Telemetry loop inschakelen',
    'source.telemetryPollHz': 'Telemetry poll Hz',
    'source.driverAliasesJson': 'Driver-aliases JSON',
    'source.refresh': 'Vernieuwen',
    'discovery.title': 'Shared-memory ontdekking',
    'discovery.noResult': 'Nog geen ontdekresultaat.',
    'discovery.noPid': 'geen PID',
    'kioskDisplay.title': 'Kioskweergave',
    'kioskDisplay.defaultMode': 'Standaard weergavemodus',
    'kioskDisplay.saveButton': 'Weergavemodus opslaan',
    'kioskDisplay.monthly': 'Maandelijks',
    'kioskDisplay.weekly': 'Wekelijks',
    'kioskDisplay.daily': 'Dagelijks',
    'kioskDisplay.lastSession': 'Laatste sessie',
    'kioskDisplay.live': 'Live',
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
    'sessions.title': 'Sessiegeschiedenis',
    'sessions.description': 'Opgeslagen sessies worden gebruikt voor historische klassementen. Dit is niet de live-sessie monitor.',
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
    'sessions.include': 'Inclusief',
    'sessions.exclude': 'Uitsluiten',
    'sessions.delete': 'Verwijderen',
    'sessions.included': 'Inbegrepen',
    'sessions.excluded': 'Uitgesloten',
    'sessions.detailTitle': 'Sessie detail',
    'sessions.includedMessage': 'Sessie opgenomen in historische klassementen.',
    'sessions.excludedMessage': 'Sessie uitgesloten van historische klassementen.',
    'sessions.correction': 'Correctie',
    'sessions.exclude': 'Uitsluiten',
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
    'aria.sourceConfiguration': 'Bronconfiguratie',
    'aria.sharedMemoryDiscovery': 'Shared-memory ontdekking',
    'changePassword.title': 'Wachtwoord wijzigen',
    'advancedStatus.refreshButton': 'Status verversen',
    'sessions.empty': 'Nog geen opgeslagen sessies.',
    'sessions.noParticipants': 'Geen deelnemers opgeslagen voor deze sessie.',
    'participants.noCompletedLaps': 'Geen voltooide ronden opgeslagen voor deze deelnemer.',
    'sessions.deleted': 'Opgeslagen sessie verwijderd.',
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
}

setupButton.addEventListener('click', () => createFirstAdmin().catch(showError));
loginButton.addEventListener('click', () => login().catch(showError));
logoutButton.addEventListener('click', () => logout().catch(showError));
languageSelectElement.addEventListener('change', () => saveLocalizationChoice(languageSelectElement.value).catch(showError));
refreshElement.addEventListener('click', () => loadConfiguration().catch(showError));
refreshSessionsButton.addEventListener('click', () => loadSessions().catch(showError));
deleteEmptySessionsButton.addEventListener('click', () => deleteEmptyHistoricalSessions().catch(showError));
addSetupRowButton.addEventListener('click', () => {
  appendSessionSetupRow({ rigName: nextRigName(), displayName: '', driverProfileId: null });
  scheduleSessionSetupAutoSave(0);
});
clearSessionSetupButton.addEventListener('click', () => clearSessionSetup().catch(showError));
createProfileButton.addEventListener('click', () => createDriverProfile().catch(showError));
saveKioskDisplayModeButton.addEventListener('click', () => saveKioskSettings().catch(showError));
setMonthlyTrackButton.addEventListener('click', () => setMonthlyTrack().catch(showError));
resetMonthlyTrackButton.addEventListener('click', () => resetMonthlyTrack().catch(showError));
runRetentionCleanupButton.addEventListener('click', () => runRetentionCleanup().catch(showError));
createAdminButton.addEventListener('click', () => createAdmin().catch(showError));
changePasswordButton.addEventListener('click', () => changePassword().catch(showError));
refreshStatusButton.addEventListener('click', () => loadAdvancedStatus().catch(showError));
bindSourceAutoSave();
loadLanguageChoice().catch(showError);

document.querySelectorAll('[data-tab]').forEach(button => {
  button.addEventListener('click', () => showTab(button.dataset.tab));
});

loadSession().catch(showError);

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
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadSessions(), loadKioskSettings(), loadLeaderboards(), loadUsers(), loadAdvancedStatus()]);
}

async function createFirstAdmin() {
  const session = await postJson('/api/admin/bootstrap', {
    username: setupUsernameElement.value.trim(),
    displayName: setupDisplayNameElement.value.trim(),
    password: setupPasswordElement.value,
  });
  showDashboard(session);
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadSessions(), loadKioskSettings(), loadLeaderboards(), loadUsers(), loadAdvancedStatus()]);
}

async function login() {
  const session = await postJson('/api/admin/session', {
    username: loginUsernameElement.value.trim(),
    password: loginPasswordElement.value,
  });
  showDashboard(session);
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadSessions(), loadKioskSettings(), loadLeaderboards(), loadUsers(), loadAdvancedStatus()]);
}

async function logout() {
  await fetch('/api/admin/session', { method: 'DELETE', credentials: 'same-origin' });
  showLogin();
}

async function loadConfiguration() {
  const configuration = await fetchJson('/api/configuration/source');
  populateForm(configuration);
  renderDiscovery(configuration.discovery);
  setStatus(`Loaded admin configuration from ${configuration.writableConfigurationPath}`);
}

async function saveConfiguration() {
  const sequence = ++autoSaveSequence;
  const payload = readSourceConfigurationForm();
  setStatus(t('status.sourceSaving'));
  const saved = await putJson('/api/configuration/source', payload);
  if (sequence !== autoSaveSequence) {
    return;
  }

  populateForm(saved);
  renderDiscovery(saved.discovery);
  setStatus(`Saved admin configuration to ${saved.writableConfigurationPath}`);
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
  const sessions = await fetchJson('/api/admin/sessions?limit=50');
  renderSessions(sessions);
  if (sessions.length === 0) {
    selectedSessionId = null;
    selectedSessionTitleElement.textContent = t('sessions.detailTitle');
    renderSessionParticipants([]);
    sessionsStatusElement.textContent = t('sessions.empty');
    return;
  }

  const selectedStillExists = sessions.some(session => session.sessionId === selectedSessionId);
  const nextSessionId = selectedStillExists ? selectedSessionId : sessions[0].sessionId;
  await loadSessionDetail(nextSessionId);
  sessionsStatusElement.textContent = `${sessions.length} stored sessions loaded for historical boards.`;
}

async function loadSessionDetail(sessionId) {
  const session = await fetchJson(`/api/admin/sessions/${encodeURIComponent(sessionId)}`);
  selectedSessionId = session.sessionId;
  selectedSessionTitleElement.textContent = `${session.trackName} - ${session.sessionKind} - ${formatDate(session.lastSeenUtc)}`;
  renderSessionParticipants(session.participants ?? []);
  highlightSelectedSessionRow();
  setStatus(`Viewing stored session from ${formatDate(session.lastSeenUtc)}.`);
}

async function setSessionCountForHistory(sessionId, countForHistory) {
  const session = await putJson(`/api/admin/sessions/${encodeURIComponent(sessionId)}/history`, { countForHistory });
  selectedSessionId = session.sessionId;
  setStatus(countForHistory ? t('sessions.includedMessage') : t('sessions.excludedMessage'));
  await loadSessions();
  await loadLeaderboards();
}

async function deleteHistoricalSession(sessionId) {
  if (!window.confirm(t('sessions.confirmDelete'))) {
    return;
  }

  await deleteJson(`/api/admin/sessions/${encodeURIComponent(sessionId)}`);
  if (selectedSessionId === sessionId) {
    selectedSessionId = null;
  }

  setStatus(t('sessions.deleted'));
  await loadSessions();
  await loadLeaderboards();
}

async function deleteEmptyHistoricalSessions() {
  if (!window.confirm(t('sessions.confirmDeleteEmpty'))) {
    return;
  }

  const result = await deleteJson('/api/admin/sessions/empty');
  selectedSessionId = null;
  setStatus(t('sessions.emptyDeleted').replace('{count}', result.deletedCount ?? 0));
  await loadSessions();
  await loadLeaderboards();
}

function renderSessions(sessions) {
  sessionRowsElement.replaceChildren();
  if (sessions.length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 9;
    cell.textContent = t('sessions.empty');
    row.appendChild(cell);
    sessionRowsElement.appendChild(row);
    return;
  }

  for (const session of sessions) {
    const row = document.createElement('tr');
    row.dataset.sessionId = session.sessionId;
    appendCell(row, session.countForHistory ? t('sessions.included') : t('sessions.excluded'));
    appendCell(row, session.trackName);
    appendCell(row, session.sessionKind);
    appendCell(row, session.sessionPhase);
    appendCell(row, formatDate(session.lastSeenUtc));
    appendCell(row, session.participantCount);
    appendCell(row, `${session.validTimedLapCount}/${session.lapCount}`);
    appendCell(row, formatSeconds(session.bestLapSeconds));
    appendActionsCell(row, [
      { label: t('sessions.view'), onClick: () => loadSessionDetail(session.sessionId).catch(showError) },
      {
        label: session.countForHistory ? t('sessions.exclude') : t('sessions.include'),
        onClick: () => setSessionCountForHistory(session.sessionId, !session.countForHistory).catch(showError),
      },
      { label: t('sessions.delete'), onClick: () => deleteHistoricalSession(session.sessionId).catch(showError), danger: true },
    ]);
    sessionRowsElement.appendChild(row);
  }

  highlightSelectedSessionRow();
}

function renderSessionParticipants(participants) {
  sessionParticipantRowsElement.replaceChildren();
  if (participants.length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 11;
    cell.textContent = t('sessions.noParticipants');
    row.appendChild(cell);
    sessionParticipantRowsElement.appendChild(row);
    return;
  }

  for (const participant of participants) {
    const row = document.createElement('tr');
    appendCell(row, participant.rank);
    appendCell(row, participant.effectiveDisplayName || participant.displayName);
    appendCell(row, participant.rigName);
    appendCell(row, participant.vehicleName);
    appendCell(row, participant.completedLaps);
    appendCell(row, formatSeconds(participant.bestLapSeconds));
    appendCell(row, formatSeconds(participant.lastLapSeconds));
    appendCell(row, `${participant.validTimedLapCount}/${participant.lapCount}`);
    const correctionInput = appendTextInputCell(row, participant.displayNameOverride ?? '');
    const excludeCheckbox = appendPlainCheckboxCell(row, participant.excludedFromHistory);
    appendButtonCell(row, 'Save', () => saveParticipantCorrection(participant.participantId, correctionInput.value, excludeCheckbox.checked).catch(showError));
    sessionParticipantRowsElement.appendChild(row);

    const lapsRow = document.createElement('tr');
    const lapsCell = document.createElement('td');
    lapsCell.colSpan = 11;
    lapsCell.appendChild(renderLapCorrectionTable(participant));
    lapsRow.appendChild(lapsCell);
    sessionParticipantRowsElement.appendChild(lapsRow);
  }
}

function renderLapCorrectionTable(participant) {
  const table = document.createElement('table');
  table.className = 'lapCorrectionTable';
  const head = document.createElement('thead');
  const headRow = document.createElement('tr');
  ['Lap', 'Time', 'Flag', 'Counts', 'Correction', 'Invalid', 'Reason', ''].forEach(label => {
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
    appendCell(row, lap.validLapFlag ?? '-');
    appendCell(row, lap.countsForTiming ? 'Yes' : 'No');
    const correctionInput = appendTextInputCell(row, lap.lapSecondsOverride ?? '');
    const invalidCheckbox = appendPlainCheckboxCell(row, lap.staffInvalidated);
    const reasonInput = appendTextInputCell(row, lap.correctionReason ?? '');
    appendButtonCell(row, 'Save', () => saveLapCorrection(lap.lapId, correctionInput.value, invalidCheckbox.checked, reasonInput.value).catch(showError));
    body.appendChild(row);
  }

  if ((participant.laps ?? []).length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 8;
    cell.textContent = 'No completed laps persisted for this participant.';
    row.appendChild(cell);
    body.appendChild(row);
  }

  table.appendChild(body);
  return table;
}

async function saveParticipantCorrection(participantId, displayNameOverride, excludedFromHistory) {
  if (!selectedSessionId) return;
  const session = await putJson(`/api/admin/sessions/${encodeURIComponent(selectedSessionId)}/participants/${participantId}/correction`, {
    displayNameOverride: nullIfEmpty(displayNameOverride),
    excludedFromHistory,
    reason: excludedFromHistory ? 'Staff excluded participant' : null,
  });
  renderSessionParticipants(session.participants ?? []);
  await loadLeaderboards();
  setStatus('Participant correction saved.');
}

async function saveLapCorrection(lapId, lapSecondsOverride, staffInvalidated, reason) {
  if (!selectedSessionId) return;
  const parsedOverride = parseLapSecondsInput(lapSecondsOverride);
  const session = await putJson(`/api/admin/sessions/${encodeURIComponent(selectedSessionId)}/laps/${lapId}/correction`, {
    lapSecondsOverride: parsedOverride,
    staffInvalidated,
    reason: nullIfEmpty(reason) ?? (staffInvalidated ? 'Staff invalidated lap' : null),
  });
  renderSessionParticipants(session.participants ?? []);
  await loadLeaderboards();
  setStatus('Lap correction saved.');
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

function highlightSelectedSessionRow() {
  sessionRowsElement.querySelectorAll('tr').forEach(row => {
    row.classList.toggle('selectedRow', row.dataset.sessionId === selectedSessionId);
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

async function loadAdvancedStatus() {
  const status = await fetchJson('/api/admin/status');
  advancedStatusElement.textContent = JSON.stringify(status, null, 2);
}

function populateForm(configuration) {
  isPopulatingSourceForm = true;
  const sharedMemory = configuration.sharedMemory ?? {};
  sourceModeElement.value = configuration.mode ?? 'Fixture';
  fixturePathElement.value = configuration.fixturePath ?? '';
  scoringMapNameElement.value = sharedMemory.scoringMapName ?? '';
  processIdElement.value = sharedMemory.processId ?? '';
  autoDiscoverElement.checked = sharedMemory.autoDiscover ?? true;
  processNamesElement.value = (sharedMemory.dedicatedServerProcessNames ?? []).join(', ');
  multipleMapPolicyElement.value = sharedMemory.multipleScoringMapPolicy ?? 'RequireExplicitSelection';
  scoringPollHzElement.value = sharedMemory.scoringPollHz ?? 10;
  telemetryEnabledElement.checked = sharedMemory.telemetry?.enabled ?? false;
  telemetryPollHzElement.value = sharedMemory.telemetry?.pollHz ?? 100;
  driverAliasesElement.value = JSON.stringify(configuration.driverAliases ?? {}, null, 2);
  isPopulatingSourceForm = false;
}

function readSourceConfigurationForm() {
  const aliases = JSON.parse(driverAliasesElement.value || '{}');
  const processId = processIdElement.value ? Number.parseInt(processIdElement.value, 10) : null;
  return {
    mode: sourceModeElement.value,
    fixturePath: fixturePathElement.value.trim(),
    driverAliases: aliases,
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
  setupPanel.hidden = false;
  loginPanel.hidden = true;
  dashboardPanel.hidden = true;
  logoutButton.hidden = true;
  setStatus(t('status.createFirstAdmin'));
}

function showLogin() {
  setupPanel.hidden = true;
  loginPanel.hidden = false;
  dashboardPanel.hidden = true;
  logoutButton.hidden = true;
  setStatus(t('status.adminLoginRequired'));
}

function showDashboard(session) {
  setupPanel.hidden = true;
  loginPanel.hidden = true;
  dashboardPanel.hidden = false;
  logoutButton.hidden = false;
  setStatus(t('status.signedIn', { name: session.displayName ?? session.username }));
  passwordUsernameElement.value = session.username ?? '';
  showTab(localStorage.getItem(tabStorageKey) || 'sourceTab', false);
}

function showTab(tabId, persist = true) {
  const target = document.getElementById(tabId) ? tabId : 'sourceTab';
  if (persist) {
    localStorage.setItem(tabStorageKey, target);
  }
  document.querySelectorAll('[data-tab]').forEach(button => {
    button.classList.toggle('active', button.dataset.tab === target);
  });
  document.querySelectorAll('.adminTab').forEach(tab => {
    tab.hidden = tab.id !== target;
    tab.classList.toggle('active', tab.id === target);
  });
}

async function fetchJson(path) {
  const response = await fetch(path, { cache: 'no-store', credentials: 'same-origin' });
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
      detail = body.error ?? detail;
    } catch {
    }
    throw new Error(detail);
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

function nullIfEmpty(value) {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function showError(error) {
  setStatus(error instanceof Error ? error.message : String(error), true);
}

function bindSourceAutoSave() {
  const immediateElements = [sourceModeElement, autoDiscoverElement, multipleMapPolicyElement, telemetryEnabledElement];
  const debouncedElements = [fixturePathElement, scoringMapNameElement, processIdElement, processNamesElement, scoringPollHzElement, telemetryPollHzElement, driverAliasesElement];

  immediateElements.forEach(element => {
    element.addEventListener('change', () => scheduleSourceAutoSave(0));
  });

  debouncedElements.forEach(element => {
    element.addEventListener('input', () => scheduleSourceAutoSave(700));
    element.addEventListener('change', () => scheduleSourceAutoSave(0));
  });
}

function scheduleSourceAutoSave(delayMilliseconds) {
  if (isPopulatingSourceForm) {
    return;
  }

  window.clearTimeout(autoSaveTimer);
  setStatus(delayMilliseconds === 0 ? t('status.sourceSaving') : t('status.sourceChanged'));
  autoSaveTimer = window.setTimeout(() => {
    saveConfiguration().catch(showError);
  }, delayMilliseconds);
}

function setStatus(message, isWarning = false) {
  statusElement.textContent = message;
  statusElement.className = isWarning ? 'warningText' : '';
}
