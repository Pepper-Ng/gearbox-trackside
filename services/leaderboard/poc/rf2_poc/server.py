from __future__ import annotations

import json
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any
from urllib.parse import urlparse

from .sources import ScoringSource


def run_server(source: ScoringSource, host: str, port: int, poll_seconds: float) -> None:
    handler = make_handler(source=source, poll_seconds=poll_seconds)
    server = ThreadingHTTPServer((host, port), handler)
    url = f"http://{host}:{port}/poc"
    print(f"rFactor 2 Trackside PoC running at {url}")
    print("Press Ctrl+C to stop.")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("Stopping PoC server.")
    finally:
        server.server_close()


def make_handler(source: ScoringSource, poll_seconds: float) -> type[BaseHTTPRequestHandler]:
    class PocRequestHandler(BaseHTTPRequestHandler):
        def do_GET(self) -> None:
            path = normalize_path(self.path)
            if path == "/api/snapshot" or path.endswith("/api/snapshot"):
                self._send_json(read_snapshot_safely(source))
                return
            if path == "/api/history" or path.endswith("/api/history"):
                self._send_json(read_history_safely(source))
                return
            if path == "/api/health" or path.endswith("/api/health"):
                self._send_json({"ok": True})
                return
            if path in ("/", "/poc") or path.endswith("/poc"):
              self._send_html(poc_html(poll_seconds=poll_seconds))
              return
            if path == "/history" or path.endswith("/history"):
              self._send_html(history_html(poll_seconds=poll_seconds))
              return
            self.send_error(HTTPStatus.NOT_FOUND, f"Not found: {self.path!r} normalized={path!r}")

        def log_message(self, format: str, *args: Any) -> None:
            print(f"{self.address_string()} - {format % args}")

        def _send_html(self, body: str) -> None:
            data = body.encode("utf-8")
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def _send_json(self, payload: dict[str, Any]) -> None:
            data = json.dumps(payload, indent=2).encode("utf-8")
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

    return PocRequestHandler


def normalize_path(raw_path: str) -> str:
    parsed_path = urlparse(raw_path).path or raw_path
    normalized = parsed_path.strip()
    if normalized != "/" and normalized.endswith("/"):
        normalized = normalized.rstrip("/")
    return normalized or "/"


def read_snapshot_safely(source: ScoringSource) -> dict[str, Any]:
    try:
        return source.read()
    except Exception as exc:
        return {
            "source": "error",
            "status": str(exc),
            "timestamp": None,
            "update_counter": None,
            "session": {},
            "drivers": [],
            "telemetry": {"status": "error", "scope": "unavailable", "vehicles": []},
            "field_coverage": [],
            "highlights": {},
            "history": read_history_safely(source),
        }


def read_history_safely(source: ScoringSource) -> dict[str, Any]:
    try:
        return source.history()
    except Exception as exc:
        return {
            "error": str(exc),
            "current_session": None,
            "completed_sessions": [],
            "completed_session_count": 0,
        }


def history_html(poll_seconds: float) -> str:
    return dashboard_html(poll_seconds=poll_seconds, initial_view="history")


def poc_html(poll_seconds: float) -> str:
    return dashboard_html(poll_seconds=poll_seconds, initial_view="dashboard")


def dashboard_html(poll_seconds: float, initial_view: str) -> str:
    poll_ms = max(250, int(poll_seconds * 1000))
    html = """<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>rFactor 2 Trackside PoC</title>
  <style>
    :root { color-scheme: light dark; font-family: Segoe UI, Arial, sans-serif; }
    body { margin: 22px; background: Canvas; color: CanvasText; }
    header { display: flex; flex-wrap: wrap; align-items: end; justify-content: space-between; gap: 12px; }
    h1 { margin: 0; font-size: 25px; }
    h2 { margin: 24px 0 8px; font-size: 18px; }
    nav a { color: LinkText; margin-left: 14px; }
    .status { margin-top: 8px; opacity: .78; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 8px 16px; margin: 16px 0; }
    .metric { padding: 8px 0; border-bottom: 1px solid color-mix(in srgb, CanvasText 18%, transparent); }
    .metric strong { display: block; font-size: 12px; text-transform: uppercase; opacity: .72; }
    table { width: 100%; border-collapse: collapse; margin-top: 10px; font-size: 13px; }
    th, td { padding: 7px 8px; border-bottom: 1px solid color-mix(in srgb, CanvasText 16%, transparent); text-align: left; white-space: nowrap; vertical-align: top; }
    th { font-size: 11px; text-transform: uppercase; letter-spacing: .04em; opacity: .78; }
    .scroll { overflow-x: auto; }
    .empty { padding: 18px 0; opacity: .72; }
    .ok { color: color-mix(in srgb, CanvasText 80%, #238636); }
    .missing { color: color-mix(in srgb, CanvasText 55%, #b54708); }
    select { min-width: 220px; padding: 4px 6px; }
    pre { white-space: pre-wrap; overflow-x: auto; padding: 10px; background: color-mix(in srgb, CanvasText 7%, transparent); }
    .history-session { margin: 14px 0 22px; border-top: 1px solid color-mix(in srgb, CanvasText 20%, transparent); padding-top: 12px; }
  </style>
</head>
<body data-initial-view="__INITIAL_VIEW__">
  <header>
    <div>
      <h1>rFactor 2 Trackside PoC</h1>
      <div class="status" id="status">Waiting for data...</div>
    </div>
    <nav><a href="/poc">Dashboard</a><a href="/history">History</a><a href="/api/snapshot">Snapshot JSON</a><a href="/api/history">History JSON</a></nav>
  </header>

  <main>
    <section id="dashboard-view">
      <section class="grid" id="session"></section>
      <h2>Coverage</h2>
      <div class="scroll"><table><thead><tr><th>Field</th><th>Source</th><th>Available</th><th>Detail</th></tr></thead><tbody id="coverage"></tbody></table></div>
      <h2>Highlights</h2>
      <section class="grid" id="highlights"></section>
      <h2>Leaderboard And Timing</h2>
      <div class="scroll"><table><thead><tr>
        <th>Place</th><th>Driver</th><th>Vehicle</th><th>Laps</th><th>Best lap</th><th>Best S1</th><th>Best S2</th><th>Best S3</th>
        <th>Last lap</th><th>Last S1</th><th>Last S2</th><th>Last S3</th><th>Current</th><th>Track %</th><th>XYZ</th><th>Gap</th><th>Finish</th>
        <th>Throttle</th><th>Brake</th><th>Steer</th><th>Gear</th><th>G</th><th>Speed</th><th>ID</th>
      </tr></thead><tbody id="drivers"><tr><td class="empty" colspan="24">No driver data yet.</td></tr></tbody></table></div>
      <h2>Telemetry Focus</h2>
      <select id="focusSelect"></select>
      <pre id="focusTelemetry">No telemetry row selected.</pre>
    </section>

    <section id="history-view">
      <h2>Current Recorded Session</h2>
      <div id="currentHistory" class="history-session"></div>
      <h2>Completed Sessions</h2>
      <div id="completedHistory"></div>
    </section>
  </main>

  <script>
    const statusEl = document.getElementById('status');
    const sessionEl = document.getElementById('session');
    const coverageEl = document.getElementById('coverage');
    const highlightsEl = document.getElementById('highlights');
    const driversEl = document.getElementById('drivers');
    const focusSelect = document.getElementById('focusSelect');
    const focusTelemetry = document.getElementById('focusTelemetry');
    const currentHistory = document.getElementById('currentHistory');
    const completedHistory = document.getElementById('completedHistory');
    let lastSnapshot = null;

    function fmt(value) {
      if (value === null || value === undefined || value === '') return '-';
      if (typeof value === 'number') return Number.isInteger(value) ? String(value) : String(Number(value.toFixed(3)));
      return String(value);
    }

    function fmtTime(value) {
      if (value === null || value === undefined || value === '') return '-';
      const number = Number(value);
      if (!Number.isFinite(number)) return '-';
      const minutes = Math.floor(number / 60);
      const seconds = (number % 60).toFixed(3).padStart(6, '0');
      return `${minutes}:${seconds}`;
    }

    function fmtVec(value) {
      if (!value) return '-';
      return `${fmt(value.x)}, ${fmt(value.y)}, ${fmt(value.z)}`;
    }

    function fmtMetric(metric, formatter = fmtTime) {
      if (!metric) return '-';
      return `${formatter(metric.value)} (${metric.driver_name || metric.driver_id})`;
    }

    function setMetric(parent, label, value) {
      const div = document.createElement('div');
      div.className = 'metric';
      const strong = document.createElement('strong');
      strong.textContent = label;
      div.appendChild(strong);
      div.appendChild(document.createTextNode(fmt(value)));
      parent.appendChild(div);
    }

    function td(value) {
      const cell = document.createElement('td');
      cell.textContent = fmt(value);
      return cell;
    }

    function render(snapshot) {
      lastSnapshot = snapshot;
      const session = snapshot.session || {};
      const telemetry = snapshot.telemetry || {};
      statusEl.textContent = `source=${fmt(snapshot.source)} status=${fmt(snapshot.status)} updates=${fmt(snapshot.update_counter)} telemetry=${fmt(telemetry.status)} scope=${fmt(telemetry.scope)} timestamp=${snapshot.timestamp ? new Date(snapshot.timestamp * 1000).toLocaleTimeString() : '-'}`;

      sessionEl.replaceChildren();
      setMetric(sessionEl, 'Track', session.track);
      setMetric(sessionEl, 'Session', session.session_type || session.session_code);
      setMetric(sessionEl, 'Phase', session.game_phase_name || session.game_phase);
      setMetric(sessionEl, 'Vehicles', `${fmt(session.vehicle_count)} / raw ${fmt(session.raw_vehicle_count)}`);
      setMetric(sessionEl, 'Scoring map', snapshot.memory_map);
      setMetric(sessionEl, 'Telemetry map', telemetry.memory_map);
      setMetric(sessionEl, 'Decode offsets', `S ${fmt(snapshot.decode_offset)} / T ${fmt(telemetry.decode_offset)}`);
      setMetric(sessionEl, 'Current time', fmtTime(session.current_time));
      setMetric(sessionEl, 'End time', fmtTime(session.end_time));
      setMetric(sessionEl, 'Lap distance', session.lap_distance);
      setMetric(sessionEl, 'Weather', `air ${fmt(session.ambient_temp)} track ${fmt(session.track_temp)} rain ${fmt(session.raining)}`);
      setMetric(sessionEl, 'Wetness', `${fmt(session.min_path_wetness)} / ${fmt(session.avg_path_wetness)} / ${fmt(session.max_path_wetness)}`);
      setMetric(sessionEl, 'Wind', fmtVec(session.wind));
      setMetric(sessionEl, 'Server', session.server_name);

      renderCoverage(snapshot.field_coverage || []);
      renderHighlights(snapshot.highlights || {});
      renderDrivers(snapshot.drivers || []);
      renderFocus(snapshot.drivers || []);
      renderHistory(snapshot.history || {});
    }

    function renderCoverage(items) {
      coverageEl.replaceChildren();
      for (const item of items) {
        const row = document.createElement('tr');
        row.appendChild(td(item.label));
        row.appendChild(td(item.source));
        const available = td(item.available ? 'yes' : 'no');
        available.className = item.available ? 'ok' : 'missing';
        row.appendChild(available);
        row.appendChild(td(item.detail));
        coverageEl.appendChild(row);
      }
    }

    function renderHighlights(highlights) {
      highlightsEl.replaceChildren();
      setMetric(highlightsEl, 'Fastest lap', fmtMetric(highlights.fastest_lap));
      setMetric(highlightsEl, 'Fastest S1', fmtMetric(highlights.fastest_sector_1));
      setMetric(highlightsEl, 'Fastest S2 split', fmtMetric(highlights.fastest_sector_2_split));
      setMetric(highlightsEl, 'Fastest S3 from best lap', fmtMetric(highlights.fastest_best_lap_sector_3));
      setMetric(highlightsEl, 'Highest speed', fmtMetric(highlights.highest_speed, fmt));
    }

    function renderDrivers(drivers) {
      driversEl.replaceChildren();
      if (!drivers.length) {
        const row = document.createElement('tr');
        const cell = td('No driver data in this snapshot.');
        cell.className = 'empty';
        cell.colSpan = 24;
        row.appendChild(cell);
        driversEl.appendChild(row);
        return;
      }
      for (const driver of drivers) {
        const telemetry = driver.telemetry || {};
        const g = telemetry.g_force ? telemetry.g_force.magnitude : null;
        const row = document.createElement('tr');
        [
          driver.place, driver.driver_name, driver.vehicle_name, driver.laps, fmtTime(driver.best_lap_time), fmtTime(driver.best_sector_1),
          fmtTime(driver.best_sector_2_split), fmtTime(driver.best_lap_sector_3), fmtTime(driver.last_lap_time), fmtTime(driver.last_sector_1),
          fmtTime(driver.last_sector_2_split), fmtTime(driver.last_sector_3), fmtTime(driver.current_lap_time), driver.track_position_percent,
          fmtVec(driver.position), fmtTime(driver.time_behind_leader), driver.finish_status_name, telemetry.throttle_percent, telemetry.brake_percent,
          telemetry.steering_percent, telemetry.gear_label, g, telemetry.speed_kph || driver.speed_kph, driver.id,
        ].forEach(value => row.appendChild(td(value)));
        driversEl.appendChild(row);
      }
    }

    function renderFocus(drivers) {
      const selected = focusSelect.value;
      focusSelect.replaceChildren();
      const empty = document.createElement('option');
      empty.value = '';
      empty.textContent = 'Select driver';
      focusSelect.appendChild(empty);
      for (const driver of drivers) {
        const option = document.createElement('option');
        option.value = String(driver.id);
        option.textContent = `${driver.place || '-'} ${driver.driver_name || driver.id}`;
        focusSelect.appendChild(option);
      }
      if (selected && drivers.some(driver => String(driver.id) === selected)) focusSelect.value = selected;
      const focused = drivers.find(driver => String(driver.id) === focusSelect.value) || drivers.find(driver => driver.telemetry_available) || drivers[0];
      if (focused) {
        focusSelect.value = String(focused.id);
        focusTelemetry.textContent = JSON.stringify({ driver: focused.driver_name, scoring: focused, telemetry: focused.telemetry }, null, 2);
      } else {
        focusTelemetry.textContent = 'No driver rows available.';
      }
    }

    function renderHistory(history) {
      renderSessionHistory(currentHistory, history.current_session, true);
      completedHistory.replaceChildren();
      const sessions = history.completed_sessions || [];
      if (!sessions.length) {
        completedHistory.textContent = 'No completed sessions recorded in this PoC process yet.';
        return;
      }
      for (const session of sessions) {
        const wrapper = document.createElement('div');
        wrapper.className = 'history-session';
        renderSessionHistory(wrapper, session, false);
        completedHistory.appendChild(wrapper);
      }
    }

    function renderSessionHistory(parent, session, isCurrent) {
      parent.replaceChildren();
      if (!session) {
        parent.textContent = isCurrent ? 'No current session recorded yet.' : 'No session data.';
        return;
      }
      const title = document.createElement('div');
      title.textContent = `${fmt(session.track)} ${fmt(session.session_type)} finalized=${fmt(session.finalized)} reason=${fmt(session.completion_reason)}`;
      parent.appendChild(title);
      const table = document.createElement('table');
      table.innerHTML = '<thead><tr><th>Place</th><th>Driver</th><th>Laps</th><th>Best lap</th><th>Observed laps</th><th>Finish</th></tr></thead>';
      const body = document.createElement('tbody');
      for (const driver of session.drivers || []) {
        const row = document.createElement('tr');
        row.appendChild(td(driver.last_place));
        row.appendChild(td(driver.driver_name));
        row.appendChild(td(driver.laps));
        row.appendChild(td(fmtTime(driver.best_lap_time)));
        row.appendChild(td((driver.lap_history || []).map(lap => `${lap.lap_number}:${fmtTime(lap.lap_time)}`).join('  ')));
        row.appendChild(td(driver.finish_status_name));
        body.appendChild(row);
      }
      table.appendChild(body);
      parent.appendChild(table);
    }

    async function loadAll() {
      try {
        const response = await fetch('/api/snapshot', { cache: 'no-store' });
        render(await response.json());
      } catch (error) {
        statusEl.textContent = `error=${error}`;
      }
    }

    focusSelect.addEventListener('change', () => { if (lastSnapshot) renderFocus(lastSnapshot.drivers || []); });
    loadAll();
    setInterval(loadAll, __POLL_MS__);
  </script>
</body>
</html>"""
    return html.replace("__POLL_MS__", str(poll_ms)).replace("__INITIAL_VIEW__", initial_view)
