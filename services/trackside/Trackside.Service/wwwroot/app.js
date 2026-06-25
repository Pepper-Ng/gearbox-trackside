const statusElement = document.querySelector('#status');
const summaryElement = document.querySelector('#summary');
const driversElement = document.querySelector('#drivers');

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

function setMetric(label, value) {
  const item = document.createElement('div');
  item.className = 'metric';
  item.innerHTML = `<span>${label}</span><strong>${value ?? '-'}</strong>`;
  summaryElement.appendChild(item);
}

function render(snapshot) {
  const session = snapshot.session ?? {};
  statusElement.textContent = `${snapshot.source} - ${snapshot.status}`;
  summaryElement.replaceChildren();
  setMetric('Track', session.trackName);
  setMetric('Session', session.kind);
  setMetric('Phase', session.phase);
  setMetric('Flag', session.overallFlag);
  setMetric('Clock', formatSeconds(session.currentSessionSeconds));
  setMetric('Air / Track', `${session.airTemperatureCelsius ?? '-'}C / ${session.trackTemperatureCelsius ?? '-'}C`);

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

loadSnapshot().catch(error => {
  statusElement.textContent = `Unable to load fixture snapshot: ${error}`;
});

window.setInterval(() => {
  loadSnapshot().catch(error => {
    statusElement.textContent = `Unable to refresh live snapshot: ${error}`;
  });
}, 1000);