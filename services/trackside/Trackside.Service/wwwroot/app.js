const statusElement = document.querySelector('#status');
const statusModeElement = document.querySelector('#statusMode');
const summaryElement = document.querySelector('#summary');
const bestSummaryElement = document.querySelector('#bestSummary');
const bestSectionElement = document.querySelector('#bestSection');
const bestTitleElement = document.querySelector('#bestTitle');
const bestLapsElement = document.querySelector('#bestLaps');
const driversElement = document.querySelector('#drivers');
const liveSectionElement = document.querySelector('#liveSection');
let currentView = 'monthly';
const displayModeToView = {
  Monthly: 'monthly',
  Weekly: 'weekly',
  Daily: 'daily',
  LastSession: 'last',
  Live: 'live',
};

document.querySelectorAll('[data-view]').forEach(button => {
  button.addEventListener('click', () => setView(button.dataset.view));
});

function formatSeconds(value) {
  if (value === null || value === undefined) return '-';
  const minutes = Math.floor(value / 60);
  const seconds = (value % 60).toFixed(3).padStart(6, '0');
  return `${minutes}:${seconds}`;
}

function formatGap(seconds, laps) {
  if (laps !== null && laps !== undefined && laps > 0) return `+${laps}L`;
  if (seconds === 0) return 'Leader';
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) return '-';
  return `+${seconds.toFixed(3)}`;
}

function formatPercent(value) {
  if (value === null || value === undefined || !Number.isFinite(value)) return '-';
  return `${value.toFixed(1)}%`;
}

function formatDate(value) {
  if (!value) return '-';
  return new Date(value).toLocaleString([], { month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' });
}

function setMetric(container, label, value) {
  const item = document.createElement('div');
  item.className = 'metric';
  item.innerHTML = `<span>${label}</span><strong>${value ?? '-'}</strong>`;
  container.appendChild(item);
}

function render(snapshot) {
  const session = snapshot.session ?? {};
  statusElement.textContent = `${snapshot.source} - ${snapshot.status}`;
  summaryElement.replaceChildren();
  setMetric(summaryElement, 'Track', session.trackName);
  setMetric(summaryElement, 'Session', session.kind);
  setMetric(summaryElement, 'Phase', session.phase);
  setMetric(summaryElement, 'Flag', session.overallFlag);
  setMetric(summaryElement, 'Clock', formatSeconds(session.currentSessionSeconds));
  setMetric(summaryElement, 'Air / Track', `${session.airTemperatureCelsius ?? '-'}C / ${session.trackTemperatureCelsius ?? '-'}C`);

  driversElement.replaceChildren();
  for (const driver of snapshot.drivers ?? []) {
    const row = document.createElement('tr');
    if (driver.isOverallBestLap) row.className = 'bestLapRow';
    appendCell(row, driver.leaderboardRank || driver.position || '-', 'rankCell');
    appendDriverCell(row, driver);
    appendCell(row, driver.rigName ?? '-');
    appendCell(row, driver.vehicleName ?? '-');
    appendCell(row, driver.completedLaps ?? 0);
    appendCell(row, formatSeconds(driver.bestLapSeconds), driver.isOverallBestLap ? 'bestTime' : undefined);
    appendCell(row, formatSeconds(driver.currentLapSeconds));
    appendSectorCell(row, driver, 1);
    appendSectorCell(row, driver, 2);
    appendSectorCell(row, driver, 3);
    appendCell(row, formatGap(driver.gapToLeaderSeconds, driver.lapsBehindLeader));
    driversElement.appendChild(row);
  }
}

function renderBestLaps(board) {
  const title = board.window === 'monthly' ? 'Monthly Track' : `${board.window.charAt(0).toUpperCase()}${board.window.slice(1)} Bests`;
  statusElement.textContent = `${board.rows?.length ?? 0} counted timed laps`;
  bestTitleElement.textContent = title;
  bestSummaryElement.replaceChildren();
  setMetric(bestSummaryElement, 'Board', title);
  setMetric(bestSummaryElement, 'Track', board.trackName ?? (board.window === 'monthly' ? 'Not set' : 'All tracks'));
  setMetric(bestSummaryElement, 'Mode', board.mode === 'all-laps' ? 'All laps' : 'Per driver');
  setMetric(bestSummaryElement, 'Since', formatDate(board.fromUtc));
  setMetric(bestSummaryElement, 'Entries', board.rows?.length ?? 0);

  bestLapsElement.replaceChildren();
  if (!board.rows || board.rows.length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 7;
    cell.textContent = 'No counted timed laps yet.';
    row.appendChild(cell);
    bestLapsElement.appendChild(row);
    return;
  }

  for (const lap of board.rows) {
    const row = document.createElement('tr');
    if (lap.rank === 1) row.className = 'bestLapRow';
    appendCell(row, lap.rank, 'rankCell');
    appendCell(row, lap.displayName ?? '-');
    appendCell(row, lap.rigName ?? '-');
    appendCell(row, lap.vehicleName ?? '-');
    appendCell(row, lap.lapNumber ?? '-');
    appendCell(row, formatSeconds(lap.lapSeconds), lap.rank === 1 ? 'bestTime' : undefined);
    appendCell(row, formatDate(lap.observedUtc));
    bestLapsElement.appendChild(row);
  }
}

function renderLastSession(result) {
  statusElement.textContent = result.isAvailable ? `${result.rows?.length ?? 0} result rows` : 'No finished session yet';
  bestTitleElement.textContent = 'Last Session';
  bestSummaryElement.replaceChildren();
  setMetric(bestSummaryElement, 'Board', 'Last Session');
  setMetric(bestSummaryElement, 'Track', result.trackName ?? 'No finished session');
  setMetric(bestSummaryElement, 'Session', result.sessionKind ?? '-');
  setMetric(bestSummaryElement, 'Finished', formatDate(result.lastSeenUtc));
  setMetric(bestSummaryElement, 'Entries', result.rows?.length ?? 0);

  bestLapsElement.replaceChildren();
  if (!result.rows || result.rows.length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 7;
    cell.textContent = 'No finished session has been observed yet.';
    row.appendChild(cell);
    bestLapsElement.appendChild(row);
    return;
  }

  for (const resultRow of result.rows) {
    const row = document.createElement('tr');
    appendCell(row, resultRow.rank, 'rankCell');
    appendCell(row, resultRow.displayName ?? '-');
    appendCell(row, resultRow.rigName ?? '-');
    appendCell(row, resultRow.vehicleName ?? '-');
    appendCell(row, resultRow.completedLaps ?? 0);
    appendCell(row, formatSeconds(resultRow.bestLapSeconds));
    appendCell(row, '');
    bestLapsElement.appendChild(row);
  }
}

function setView(view) {
  currentView = view;
  document.querySelectorAll('[data-view]').forEach(button => {
    button.classList.toggle('active', button.dataset.view === view);
  });
  const isLive = view === 'live';
  statusModeElement.textContent = isLive ? 'Live Feed' : 'Leaderboard';
  statusModeElement.classList.toggle('live', isLive);
  summaryElement.hidden = !isLive;
  liveSectionElement.hidden = !isLive;
  bestSummaryElement.hidden = isLive;
  bestSectionElement.hidden = isLive;
  if (isLive) {
    loadSnapshot().catch(error => {
      statusElement.textContent = `Unable to load live snapshot: ${error}`;
    });
    return;
  }

  if (view === 'last') {
    loadLastSession().catch(error => {
      statusElement.textContent = `Unable to load last session: ${error}`;
    });
    return;
  }

  loadBestLaps(view).catch(error => {
    statusElement.textContent = `Unable to load best laps: ${error}`;
  });
}

function appendCell(row, value, className) {
  const cell = document.createElement('td');
  if (className) cell.className = className;
  cell.textContent = value ?? '-';
  row.appendChild(cell);
}

function appendDriverCell(row, driver) {
  const cell = document.createElement('td');
  const wrapper = document.createElement('div');
  wrapper.className = 'driverCell';
  const name = document.createElement('strong');
  name.textContent = driver.displayName ?? '-';
  const progress = document.createElement('span');
  progress.textContent = formatPercent(driver.trackPositionPercent);
  wrapper.append(name, progress);
  cell.appendChild(wrapper);
  row.appendChild(cell);
}

function appendSectorCell(row, driver, sectorNumber) {
  const sector = (driver.sectors ?? []).find(candidate => candidate.number === sectorNumber);
  const cell = document.createElement('td');
  cell.className = sector?.isOverallBest ? 'bestTime sectorCell' : 'sectorCell';
  const best = document.createElement('span');
  best.textContent = formatSeconds(sector?.bestSeconds);
  const current = document.createElement('small');
  current.textContent = formatSeconds(sector?.currentSeconds ?? sector?.lastSeconds);
  cell.append(best, current);
  row.appendChild(cell);
}

async function loadSnapshot() {
  const response = await fetch('/api/live-session/current', { cache: 'no-store' });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  render(await response.json());
}

async function loadBestLaps(view) {
  const response = await fetch(`/api/leaderboards/best-laps?window=${encodeURIComponent(view)}&mode=per-driver&limit=20`, { cache: 'no-store' });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  renderBestLaps(await response.json());
}

async function loadLastSession() {
  const response = await fetch('/api/leaderboards/last-session', { cache: 'no-store' });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  renderLastSession(await response.json());
}

async function loadClientConfiguration() {
  const response = await fetch('/api/configuration/client', { cache: 'no-store' });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return response.json();
}

loadClientConfiguration()
  .then(configuration => setView(displayModeToView[configuration.defaultDisplayMode] ?? 'monthly'))
  .catch(() => setView('monthly'));

window.setInterval(() => {
  if (currentView === 'live') {
    loadSnapshot().catch(error => {
      statusElement.textContent = `Unable to refresh live snapshot: ${error}`;
    });
    return;
  }

  if (currentView === 'last') {
    loadLastSession().catch(error => {
      statusElement.textContent = `Unable to refresh last session: ${error}`;
    });
    return;
  }

  loadBestLaps(currentView).catch(error => {
    statusElement.textContent = `Unable to refresh best laps: ${error}`;
  });
}, 1000);