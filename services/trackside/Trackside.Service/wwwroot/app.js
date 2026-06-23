const statusElement = document.querySelector('#status');
const summaryElement = document.querySelector('#summary');
const driversElement = document.querySelector('#drivers');

function formatSeconds(value) {
  if (value === null || value === undefined) return '-';
  const minutes = Math.floor(value / 60);
  const seconds = (value % 60).toFixed(3).padStart(6, '0');
  return `${minutes}:${seconds}`;
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
    row.innerHTML = `
      <td>${driver.position ?? '-'}</td>
      <td>${driver.displayName ?? '-'}</td>
      <td>${driver.rigName ?? '-'}</td>
      <td>${formatSeconds(driver.bestLapSeconds)}</td>
      <td>${driver.gapToLeaderSeconds ? '+' + driver.gapToLeaderSeconds.toFixed(3) : '-'}</td>`;
    driversElement.appendChild(row);
  }
}

async function loadSnapshot() {
  const response = await fetch('/api/live-session/current', { cache: 'no-store' });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  render(await response.json());
}

loadSnapshot().catch(error => {
  statusElement.textContent = `Unable to load fixture snapshot: ${error}`;
});