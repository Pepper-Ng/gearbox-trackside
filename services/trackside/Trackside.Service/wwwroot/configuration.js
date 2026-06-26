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
const setMonthlyTrackButton = document.querySelector('#setMonthlyTrack');
const resetMonthlyTrackButton = document.querySelector('#resetMonthlyTrack');
const monthlyBestLapsElement = document.querySelector('#monthlyBestLaps');
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
    'tabs.leaderboards': 'Leaderboards',
    'tabs.admins': 'Admins',
    'tabs.status': 'Status',
    'status.checkingSession': 'Checking admin session...',
    'status.adminLoginRequired': 'Admin login required.',
    'status.createFirstAdmin': 'Create the first admin account.',
    'status.sourceChanged': 'Source configuration changed...',
    'status.sourceSaving': 'Saving source configuration...',
    'status.setupChanged': 'Prepared session setup changed...',
    'status.setupSaving': 'Saving prepared session setup...',
    'status.setupSaved': 'Prepared session setup saved.',
    'status.setupCleared': 'Prepared session setup cleared.',
    'status.profileCreated': 'Driver profile created.',
    'setup.title': 'Prepare Session',
    'setup.description': 'Prepared rig assignments stay active for future sessions until changed or cleared.',
    'setup.rig': 'Rig',
    'setup.screenName': 'Screen name',
    'setup.driverProfile': 'Driver profile',
    'setup.addRig': 'Add Rig',
    'setup.clear': 'Clear Setup',
    'setup.remove': 'Remove',
    'setup.noProfile': 'No profile',
    'profiles.title': 'Driver Profiles',
    'profiles.displayName': 'Display name',
    'profiles.email': 'Email',
    'profiles.notes': 'Notes',
    'profiles.create': 'Create Profile',
  },
  nl: {
    'admin.title': 'Beheer',
    'language.label': 'Taal',
    'nav.kiosk': 'Kiosk',
    'nav.logout': 'Uitloggen',
    'tabs.source': 'Bron',
    'tabs.sessionSetup': 'Sessie voorbereiden',
    'tabs.leaderboards': 'Klassementen',
    'tabs.admins': 'Beheerders',
    'tabs.status': 'Status',
    'status.checkingSession': 'Beheersessie controleren...',
    'status.adminLoginRequired': 'Beheerlogin vereist.',
    'status.createFirstAdmin': 'Maak het eerste beheeraccount aan.',
    'status.sourceChanged': 'Bronconfiguratie gewijzigd...',
    'status.sourceSaving': 'Bronconfiguratie opslaan...',
    'status.setupChanged': 'Sessievoorbereiding gewijzigd...',
    'status.setupSaving': 'Sessievoorbereiding opslaan...',
    'status.setupSaved': 'Sessievoorbereiding opgeslagen.',
    'status.setupCleared': 'Sessievoorbereiding gewist.',
    'status.profileCreated': 'Bestuurdersprofiel aangemaakt.',
    'setup.title': 'Sessie voorbereiden',
    'setup.description': 'Voorbereide rig-toewijzingen blijven actief voor volgende sessies totdat ze worden aangepast of gewist.',
    'setup.rig': 'Rig',
    'setup.screenName': 'Schermnaam',
    'setup.driverProfile': 'Bestuurdersprofiel',
    'setup.addRig': 'Rig toevoegen',
    'setup.clear': 'Setup wissen',
    'setup.remove': 'Verwijderen',
    'setup.noProfile': 'Geen profiel',
    'profiles.title': 'Bestuurdersprofielen',
    'profiles.displayName': 'Weergavenaam',
    'profiles.email': 'E-mail',
    'profiles.notes': 'Notities',
    'profiles.create': 'Profiel aanmaken',
  },
};

setupButton.addEventListener('click', () => createFirstAdmin().catch(showError));
loginButton.addEventListener('click', () => login().catch(showError));
logoutButton.addEventListener('click', () => logout().catch(showError));
languageSelectElement.addEventListener('change', () => setLanguage(languageSelectElement.value));
refreshElement.addEventListener('click', () => loadConfiguration().catch(showError));
addSetupRowButton.addEventListener('click', () => {
  appendSessionSetupRow({ rigName: nextRigName(), displayName: '', driverProfileId: null });
  scheduleSessionSetupAutoSave(0);
});
clearSessionSetupButton.addEventListener('click', () => clearSessionSetup().catch(showError));
createProfileButton.addEventListener('click', () => createDriverProfile().catch(showError));
setMonthlyTrackButton.addEventListener('click', () => setMonthlyTrack().catch(showError));
resetMonthlyTrackButton.addEventListener('click', () => resetMonthlyTrack().catch(showError));
createAdminButton.addEventListener('click', () => createAdmin().catch(showError));
changePasswordButton.addEventListener('click', () => changePassword().catch(showError));
refreshStatusButton.addEventListener('click', () => loadAdvancedStatus().catch(showError));
bindSourceAutoSave();
setLanguage(localStorage.getItem(languageStorageKey) || 'en');

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
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadLeaderboards(), loadUsers(), loadAdvancedStatus()]);
}

async function createFirstAdmin() {
  const session = await postJson('/api/admin/bootstrap', {
    username: setupUsernameElement.value.trim(),
    displayName: setupDisplayNameElement.value.trim(),
    password: setupPasswordElement.value,
  });
  showDashboard(session);
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadLeaderboards(), loadUsers(), loadAdvancedStatus()]);
}

async function login() {
  const session = await postJson('/api/admin/session', {
    username: loginUsernameElement.value.trim(),
    password: loginPasswordElement.value,
  });
  showDashboard(session);
  await Promise.all([loadConfiguration(), loadSessionSetup(), loadLeaderboards(), loadUsers(), loadAdvancedStatus()]);
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

async function setMonthlyTrack() {
  const trackName = monthlyTrackNameElement.value.trim();
  const reason = monthlyTrackReasonElement.value.trim();
  const monthlyTrack = await putJson('/api/admin/leaderboards/monthly-track', { trackName, reason });
  renderMonthlyTrack(monthlyTrack);
  monthlyTrackReasonElement.value = '';
  await loadLeaderboards();
  statusElement.textContent = 'Monthly track started with fresh stats.';
}

async function resetMonthlyTrack() {
  const reason = monthlyTrackReasonElement.value.trim() || 'Monthly track reset';
  const monthlyTrack = await postJson('/api/admin/leaderboards/monthly-track/reset', { reason });
  renderMonthlyTrack(monthlyTrack);
  monthlyTrackReasonElement.value = '';
  await loadLeaderboards();
  statusElement.textContent = 'Monthly track stats reset.';
}

function renderMonthlyTrack(monthlyTrack) {
  if (!monthlyTrack?.isActive) {
    monthlyTrackStatusElement.textContent = 'No active monthly track.';
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
    cell.textContent = 'No counted timed laps yet.';
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
  statusElement.textContent = 'Admin user created.';
}

async function changePassword() {
  const username = encodeURIComponent(passwordUsernameElement.value.trim());
  await putJson(`/api/admin/users/${username}/password`, { newPassword: newPasswordElement.value });
  newPasswordElement.value = '';
  await loadUsers();
  statusElement.textContent = 'Admin password changed.';
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
  discoveryStatusElement.textContent = discovery?.status ?? 'No discovery result.';
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
  const pid = candidate.processId === null || candidate.processId === undefined ? 'no PID' : `PID ${candidate.processId}`;
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
  setStatus(`Signed in as ${session.displayName ?? session.username}`);
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

function setLanguage(language) {
  const nextLanguage = translations[language] ? language : 'en';
  localStorage.setItem(languageStorageKey, nextLanguage);
  languageSelectElement.value = nextLanguage;
  document.documentElement.lang = nextLanguage;
  document.querySelectorAll('[data-i18n]').forEach(element => {
    element.textContent = t(element.dataset.i18n);
  });
  sessionSetupRowsElement.querySelectorAll('.setupProfileId option[value=""]').forEach(option => {
    option.textContent = t('setup.noProfile');
  });
  sessionSetupRowsElement.querySelectorAll('.sessionSetupRow button').forEach(button => {
    button.textContent = t('setup.remove');
  });
}

function t(key) {
  return translations[languageSelectElement.value]?.[key] ?? translations.en[key] ?? key;
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

  return response.json();
}

function appendCell(row, value) {
  const cell = document.createElement('td');
  cell.textContent = value ?? '-';
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
