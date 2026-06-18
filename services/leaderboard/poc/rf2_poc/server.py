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
          if path in ("/", "/poc") or path.endswith("/poc"):
                self._send_html(poc_html(poll_seconds=poll_seconds))
                return
          if path == "/api/snapshot" or path.endswith("/api/snapshot"):
                self._send_json(read_snapshot_safely(source))
                return
          if path == "/api/health" or path.endswith("/api/health"):
                self._send_json({"ok": True})
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
        }


def poc_html(poll_seconds: float) -> str:
    poll_ms = max(250, int(poll_seconds * 1000))
    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>rFactor 2 Trackside PoC</title>
  <style>
    :root {{ color-scheme: light dark; font-family: Segoe UI, Arial, sans-serif; }}
    body {{ margin: 24px; background: Canvas; color: CanvasText; }}
    h1 {{ margin: 0 0 8px; font-size: 26px; }}
    .meta {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px 16px; margin: 16px 0; }}
    .meta div {{ padding: 8px 0; border-bottom: 1px solid color-mix(in srgb, CanvasText 20%, transparent); }}
    table {{ width: 100%; border-collapse: collapse; margin-top: 18px; font-size: 14px; }}
    th, td {{ padding: 8px 10px; border-bottom: 1px solid color-mix(in srgb, CanvasText 18%, transparent); text-align: left; white-space: nowrap; }}
    th {{ font-size: 12px; text-transform: uppercase; letter-spacing: .04em; }}
    .status {{ margin-top: 8px; opacity: .75; }}
    .empty {{ padding: 20px 0; opacity: .75; }}
  </style>
</head>
<body>
  <h1>rFactor 2 Trackside PoC</h1>
  <div class="status" id="status">Waiting for data...</div>
  <section class="meta" id="session"></section>
  <table>
    <thead>
      <tr>
        <th>Place</th>
        <th>Driver</th>
        <th>Vehicle</th>
        <th>Laps</th>
        <th>Best lap</th>
        <th>Last lap</th>
        <th>Current lap</th>
        <th>Sector</th>
        <th>Behind leader</th>
        <th>Source ID</th>
      </tr>
    </thead>
    <tbody id="drivers"><tr><td class="empty" colspan="10">No driver data yet.</td></tr></tbody>
  </table>
  <script>
    const statusEl = document.getElementById('status');
    const sessionEl = document.getElementById('session');
    const driversEl = document.getElementById('drivers');

    function fmtTime(value) {{
      if (value === null || value === undefined || value === '') return '-';
      const number = Number(value);
      if (!Number.isFinite(number)) return '-';
      const minutes = Math.floor(number / 60);
      const seconds = (number % 60).toFixed(3).padStart(6, '0');
      return `${{minutes}}:${{seconds}}`;
    }}

    function fmt(value) {{
      if (value === null || value === undefined || value === '') return '-';
      return String(value);
    }}

    function setText(parent, label, value) {{
      const div = document.createElement('div');
      const strong = document.createElement('strong');
      strong.textContent = `${{label}}: `;
      div.appendChild(strong);
      div.appendChild(document.createTextNode(fmt(value)));
      parent.appendChild(div);
    }}

    function render(snapshot) {{
      const session = snapshot.session || {{}};
      statusEl.textContent = `source=${{fmt(snapshot.source)}} status=${{fmt(snapshot.status)}} updates=${{fmt(snapshot.update_counter)}} timestamp=${{snapshot.timestamp ? new Date(snapshot.timestamp * 1000).toLocaleTimeString() : '-'}}`;

      sessionEl.replaceChildren();
      setText(sessionEl, 'Track', session.track);
      setText(sessionEl, 'Session', session.session_type || session.session_code);
      setText(sessionEl, 'Vehicles', session.vehicle_count ?? (snapshot.drivers || []).length);
      setText(sessionEl, 'Current time', fmtTime(session.current_time));
      setText(sessionEl, 'End time', fmtTime(session.end_time));
      setText(sessionEl, 'Ambient temp', session.ambient_temp);
      setText(sessionEl, 'Track temp', session.track_temp);
      setText(sessionEl, 'Rain', session.raining);

      driversEl.replaceChildren();
      const drivers = snapshot.drivers || [];
      if (!drivers.length) {{
        const row = document.createElement('tr');
        const cell = document.createElement('td');
        cell.className = 'empty';
        cell.colSpan = 10;
        cell.textContent = 'No driver data in this snapshot.';
        row.appendChild(cell);
        driversEl.appendChild(row);
        return;
      }}

      for (const driver of drivers) {{
        const row = document.createElement('tr');
        const cells = [
          driver.place,
          driver.driver_name,
          driver.vehicle_name,
          driver.laps,
          fmtTime(driver.best_lap_time),
          fmtTime(driver.last_lap_time),
          fmtTime(driver.current_lap_time),
          driver.sector,
          fmtTime(driver.time_behind_leader),
          driver.id,
        ];
        for (const value of cells) {{
          const cell = document.createElement('td');
          cell.textContent = fmt(value);
          row.appendChild(cell);
        }}
        driversEl.appendChild(row);
      }}
    }}

    async function loadSnapshot() {{
      try {{
        const response = await fetch('/api/snapshot', {{ cache: 'no-store' }});
        render(await response.json());
      }} catch (error) {{
        statusEl.textContent = `error=${{error}}`;
      }}
    }}

    loadSnapshot();
    setInterval(loadSnapshot, {poll_ms});
  </script>
</body>
</html>"""