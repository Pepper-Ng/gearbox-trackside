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

setupButton.addEventListener('click', () => createFirstAdmin().catch(showError));
loginButton.addEventListener('click', () => login().catch(showError));
logoutButton.addEventListener('click', () => logout().catch(showError));
refreshElement.addEventListener('click', () => loadConfiguration().catch(showError));
createAdminButton.addEventListener('click', () => createAdmin().catch(showError));
changePasswordButton.addEventListener('click', () => changePassword().catch(showError));
refreshStatusButton.addEventListener('click', () => loadAdvancedStatus().catch(showError));
bindSourceAutoSave();

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
  await Promise.all([loadConfiguration(), loadUsers(), loadAdvancedStatus()]);
}

async function createFirstAdmin() {
  const session = await postJson('/api/admin/bootstrap', {
    username: setupUsernameElement.value.trim(),
    displayName: setupDisplayNameElement.value.trim(),
    password: setupPasswordElement.value,
  });
  showDashboard(session);
  await Promise.all([loadConfiguration(), loadUsers(), loadAdvancedStatus()]);
}

async function login() {
  const session = await postJson('/api/admin/session', {
    username: loginUsernameElement.value.trim(),
    password: loginPasswordElement.value,
  });
  showDashboard(session);
  await Promise.all([loadConfiguration(), loadUsers(), loadAdvancedStatus()]);
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
  setStatus('Saving source configuration...');
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
  statusElement.textContent = 'Create the first admin account.';
}

function showLogin() {
  setupPanel.hidden = true;
  loginPanel.hidden = false;
  dashboardPanel.hidden = true;
  logoutButton.hidden = true;
  statusElement.textContent = 'Admin login required.';
}

function showDashboard(session) {
  setupPanel.hidden = true;
  loginPanel.hidden = true;
  dashboardPanel.hidden = false;
  logoutButton.hidden = false;
  statusElement.textContent = `Signed in as ${session.displayName ?? session.username}`;
  passwordUsernameElement.value = session.username ?? '';
}

function showTab(tabId) {
  document.querySelectorAll('[data-tab]').forEach(button => {
    button.classList.toggle('active', button.dataset.tab === tabId);
  });
  document.querySelectorAll('.adminTab').forEach(tab => {
    tab.hidden = tab.id !== tabId;
    tab.classList.toggle('active', tab.id === tabId);
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
  setStatus(delayMilliseconds === 0 ? 'Saving source configuration...' : 'Source configuration changed...');
  autoSaveTimer = window.setTimeout(() => {
    saveConfiguration().catch(showError);
  }, delayMilliseconds);
}

function setStatus(message, isWarning = false) {
  statusElement.textContent = message;
  statusElement.className = isWarning ? 'warningText' : '';
}
